/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using magic.node;
using magic.signals.contracts;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// The background service responsible for scheduling and evaluating tasks.
    ///
    /// Notice, you should make sure you resolve this as a singleton if you are
    /// using an IoC container. Also notice that no tasks will evaluate before
    /// you explicitly somehow invoke Start on your instance.
    /// </summary>
    public sealed class TaskScheduler : IDisposable
    {
        readonly object _locker = new object();
        readonly IServiceProvider _services;
        readonly TaskManager _tasks;
        Timer _timer;

        /// <summary>
        /// Creates a new background service, responsible for scheduling and
        /// evaluating tasks that have been scheduled for future evaluation.
        /// </summary>
        /// <param name="services">Service provider to resolve ISignaler and
        /// ILogger if necessary.</param>
        /// <param name="tasksFile">The path to your tasks file,
        /// declaring what tasks your application has scheduled for future
        /// evaluation.</param>
        public TaskScheduler(IServiceProvider services, string tasksFile)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _tasks = new TaskManager(tasksFile ?? throw new ArgumentNullException(nameof(tasksFile)));
        }

        /// <summary>
        /// Returns true if scheduler is running.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Starts your scheduler. You must invoke this method in order to
        /// start your scheduler.
        /// </summary>
        public void Start()
        {
            lock (_locker)
            {
                Running = true;
                EnsureTimer();
            }
        }

        /// <summary>
        /// Stops your scheduler, such that no more tasks will be evaluated.
        /// </summary>
        public void Stop()
        {
            lock (_locker)
            {
                Running = false;
                _timer?.Dispose();
                _timer = null;
            }
        }

        /// <summary>
        /// Creates a new task.
        ///
        /// Notice, will delete any previously created tasks with the same name.
        /// </summary>
        /// <param name="node">Node declaring your task.</param>
        public void AddTask(Node node)
        {
            lock (_locker)
            {
                /*
                 *  Removing any other tasks with the same name before
                 *  proceeding with add.
                 */
                _tasks.AddTask(node);

                /*
                 * Need to "retouch" our timer in case task is our first due
                 * task in our list of tasks.
                 */
                EnsureTimer();
            }
        }

        /// <summary>
        /// Returns a previously created task to caller.
        /// </summary>
        /// <param name="name">Name of task you wish to retrieve.</param>
        /// <returns>A node representing your task.</returns>
        public Node GetTask(string name)
        {
            lock (_locker)
            {
                // Getting task with specified name.
                var task = _tasks.GetTask(name);

                // Checking if named task exists.
                if (task == null)
                    return null;

                // Creating and returning our result.
                return new Node(task.Name, null, task.RootNode.Clone().Children.ToList());
            }
        }

        /// <summary>
        /// Deletes an existing task from your task manager.
        /// </summary>
        /// <param name="name">Name of task to delete.</param>
        public void DeleteTask(string name)
        {
            lock (_locker)
            {
                _tasks.DeleteTask(name);

                /*
                 * Need to "retouch" our timer in case task is our first due
                 * task in our list of tasks.
                 */
                EnsureTimer();
            }
        }

        /// <summary>
        /// Lists all tasks in task manager, in order of evaluation, such that
        /// the first task in queue will be the first task returned.
        /// </summary>
        /// <returns>All tasks listed in chronological order of evaluation.</returns>
        public IEnumerable<string> ListTasks()
        {
            lock (_locker)
            {
                return _tasks.List().Select(x => x.Name).ToList();
            }
        }

        #region [ -- Interface implementations -- ]

        /// <summary>
        /// IDisposable implementation.
        /// </summary>
        public void Dispose()
        {
            lock (_locker)
            {
                if (_timer == null)
                    return; // Nothing to dispose here.

                Running = false;
                _timer.Dispose();
                _timer = null; // In case instance is disposed twice.
            }
        }

        #endregion

        #region [ -- Private helper methods -- ]

        /*
         * Creates the timer such that it is invoked when the due date for the
         * next upcoming task is due.
         *
         * Notice, assumes the "_locker" has been locked during invocation of
         * the method.
         */
        void EnsureTimer()
        {
            /*
             * Checking if timer actually is running, and if not,
             * returning early.
             */
            if (!Running)
                return; // Scheduler has been explicitly stopped.

            /*
             * Disposing old timer if there exists one.
             */
            _timer?.Dispose();
            _timer = null; // To avoid disposing timer twice.

            // Retrieving next task if there are any.
            var next = _tasks.NextDueTask();
            if (next == null)
                return; // No more tasks, hence not creating timer.

            /*
             * Checking if task is overdue, at which point we evaluate it
             * immediately.
             */
            var now = DateTime.Now;

            /*
             * Figuring out when to evaluate next task.
             *
             * Notice, if task is over due, we evaluate it 250 milliseconds from
             * now, to make sure we evaluate tasks that for some reasons have
             * been queued up not being able to evaluate for some reasons.
             */
            var nextDue = next.Due < now ?
                new TimeSpan(0, 0, 0, 0, 250) :
                next.Due - now;

            /*
             * Checking if due date for next task is too high for our timer.
             * 
             * Notice, the timeout can be maximum 49 days for a
             * System.Threading.Timer, hence we must check to see if the due
             * date is too high for our timer to be successfully created, and
             * if it is, we simply "recursively" invoke self 45 days from now,
             * to re-create timer 45 days from now.
             */
            if (nextDue.TotalDays > 45)
            {
                // Next task due date is too high for our timer.
                _timer = new Timer(
                    PostponeTask,
                    null,
                    new TimeSpan(45, 0, 0, 0),
                    new TimeSpan(0, 0, 0, 0, -1));
            }
            else
            {
                _timer = new Timer(
                    ExecuteNextTask,
                    null,
                    nextDue,
                    new TimeSpan(0, 0, 0, 0, -1));
            }
        }

        /*
         * Invoked when next upcoming task was due in more than 45 days.
         */
        void PostponeTask(object state)
        {
            lock(_locker)
            {
                EnsureTimer();
            }
        }

        /*
         * Executes the next upcoming tasks, if any.
         */
        void ExecuteNextTask(object state)
        {
            /*
             * Verifying that we have an upcoming task, and if
             * not returning early.
             */
            Task current;
            lock (_locker)
            {
                current = _tasks.NextDueTask();
            }
            if (current == null)
                return; // No more tasks in scheduler.

            /*
             * Verifying this task actually is due.
             */
            if (current.Due.AddMilliseconds(-10) > DateTime.Now)
            {
                /*
                 * Top task in list of tasks is not due now, hence we recreate
                 * our timer, and return early.
                 */
                lock (_locker)
                {
                    EnsureTimer();
                }
                return; // Returning early, nothing more to do here.
            }

            /*
             * Making sure we reorder list of tasks before we evaluate task,
             * in case of exceptions, in addition to allowing us to restart
             * timer, to have multiple tasks executing consecutively on
             * different threads if necessary due to overlapping dues dates.
             */
            if (current.Repeats)
            {
                /*
                 * Task repeats, making sure we calculate next due date, for
                 * then to reorder task list, making sure we synchronize
                 * access to tasks as we do.
                 */
                current.CalculateDue();
                lock (_locker)
                {
                    _tasks.Sort();
                    EnsureTimer();
                }
            }
            else
            {
                // Task does not repeat.
                lock (_locker)
                {
                    _tasks.DeleteTask(current.Name);
                    EnsureTimer();
                }
            }

            /*
             * Making sure we're able to log exceptions.
             */
            try
            {
                /*
                 * Retrieving task's lambda object, and evaluating it.
                 */
                var lambda = current.Lambda.Clone();
                var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
                signaler.SignalAsync("wait.eval", lambda).Wait();
            }
            catch(Exception err)
            {
                // Making sure we log exception using preferred ILogger instance.
                var logger = _services.GetService(typeof(ILogger)) as ILogger;
                logger.LogError(current.Name, err);
            }
        }

        #endregion
    }
}
