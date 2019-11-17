/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// The background service responsible for scheduling and evaluating tasks.
    ///
    /// Notice, you should make sure you resolve this as a singleton if you are
    /// using an IoC container.
    /// </summary>
    public sealed class TaskScheduler : IDisposable
    {
        readonly object _locker = new object();
        readonly IServiceProvider _services;
        readonly Synchronizer<TaskManager> _tasks;
        Timer _timer;
        bool _running;

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
            _tasks = new Synchronizer<TaskManager>(new TaskManager(tasksFile ?? throw new ArgumentNullException(nameof(tasksFile))));
        }

        /// <summary>
        /// Starts your scheduler. You must invoke this method in order to
        /// start your scheduler.
        /// </summary>
        public void Start()
        {
            lock (_locker)
            {
                if (_running)
                    return; // Already started.

                _running = true;
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
                if (!_running)
                    return; // Already stopped.

                _running = false;
                _timer?.Dispose();
                _timer = null;
            }
        }

        /// <summary>
        /// Creates a new task, and or edits an existing.
        /// </summary>
        /// <param name="node">Node declaring your task.</param>
        public void AddTask(Node node)
        {
            var taskName = node.GetEx<string>();
            _tasks.Write(tasks =>
            {
                /*
                 *  Removing any other tasks with the same name before
                 *  proceeding with add.
                 */
                tasks.AddTask(node);
            });

            /*
             * Need to "retouch" our timer in case task is our first due
             * task in our list of tasks.
             */
            EnsureTimer();
        }

        /// <summary>
        /// Deletes an existing task from your task manager.
        /// </summary>
        /// <param name="name">Name of task to delete.</param>
        public void DeleteTask(string name)
        {
            _tasks.Write(tasks => tasks.DeleteTask(name));
        }

        /// <summary>
        /// Lists all tasks in task manager, in order of evaluation, such that
        /// the first task in queue will be the first task returned.
        /// </summary>
        /// <returns>All tasks listed in chronological order of evaluation.</returns>
        public IEnumerable<string> ListTasks()
        {
            return _tasks.Read(tasks => tasks.List().Select(x => x.Name).ToList());
        }

        #region [ -- Interface implementations -- ]

        public void Dispose()
        {
            lock (_locker)
            {
                if (_timer == null)
                    return; // Nothing to dispose here.

                _running = false;
                _timer.Dispose();
                _timer = null; // In case instance is disposed twice.
            }
        }

        #endregion

        #region [ -- Private helper methods -- ]

        void EnsureTimer()
        {
            /*
             * Checking if timer actually is running, and if not, returning
             * early.
             *
             * Also disposing old timer if existing, to make sure we get a new
             * timer kicking in as our next upcoming task is due.
             */
            lock (_locker)
            {
                /*
                 * Checking if scheduler has been explicitly stopped.
                 */
                if (!_running)
                    return; // Scheduler has been explicitly stopped.

                // Disposing old timer if there exists one.
                _timer?.Dispose();
                _timer = null; // To avoid disposing timer twice.
            }

            // Calculating next task's due date.
            _tasks.Read((tasks) =>
            {
                // Retrieving next task.
                var next = tasks.NextTask();
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
                        (state) => EnsureTimer(),
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
            });
        }

        async void ExecuteNextTask(object state)
        {
            /*
             * Verifying that scheduler has not been explicitly stopped.
             */
            lock (_locker)
            {
                if (!_running)
                    return;
            }

            /*
             * Verifying that we have an upcoming task, and if
             * not returning early.
             */
            var next = _tasks.Read(tasks => tasks.NextTask());
            if (next == null)
                return; // No more tasks in scheduler.

            /*
             * Verifying this task actually is due, and that the task we had a
             * timeout for was not removed before it was set to be evaluated.
             */
            if (next.Due.AddMilliseconds(-1) > DateTime.Now)
            {
                /*
                 * Top task in list of tasks is not due now, hence we recreate
                 * our timer, and return early.
                 *
                 * Notice, there are no reasons to reorder or manipulate our
                 * task list, or remove tasks, etc.
                 */
                EnsureTimer();
            }

            /*
             * We can never allow for exceptions to propagate out of this
             * method due to "async void" signature.
             */
            try
            {
                /*
                 * Making sure we reorder list of tasks before we evaluate task,
                 * in case of exceptions, in addition to allowing us to restart
                 * timer, to have multiple tasks executing consecutively on
                 * different threads if necessary due to overlapping dues dates.
                 */
                if (next.Repeats)
                {
                    /*
                     * Task repeats, making sure we calculate next due date, for
                     * then to reorder task list, making sure we synchronize
                     * access to tasks as we do.
                     */
                    _tasks.Write(tasks =>
                    {
                        next.CalculateDue();
                        tasks.Sort();
                    });
                }
                else
                {
                    // Task does not repeat.
                    _tasks.Write(tasks => tasks.DeleteTask(next));
                }

                /*
                 * Recreating our timer before we evaluate task, in case
                 * multiple tasks have overlapping due dates, which will allow
                 * us to use multiple threads to evaluate multiple tasks
                 * simultaneously.
                 */
                EnsureTimer();

                /*
                 * Retrieving task's lambda object, and evaluating it.
                 */
                var lambda = next.Lambda.Clone();
                var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
                await signaler.SignalAsync("wait.eval", lambda);
            }
            catch(Exception err)
            {
                // Making sure we log exception using preferred ILogger instance.
                var logger = _services.GetService(typeof(ILogger)) as ILogger;
                logger.LogError(next.Name, err);
            }
        }

        #endregion
    }
}
