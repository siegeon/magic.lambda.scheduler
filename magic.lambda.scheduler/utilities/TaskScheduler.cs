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
    /// 
    /// You are also responsible to make sure all operations on instance is synchronized.
    /// </summary>
    public sealed class TaskScheduler : IDisposable
    {
        readonly IServiceProvider _services;
        readonly SemaphoreSlim _waiter;
        readonly TaskList _tasks;
        readonly Timer _timer;

        /// <summary>
        /// Creates a new background service, responsible for scheduling and
        /// evaluating tasks that have been scheduled for future evaluation.
        /// </summary>
        /// <param name="services">Service provider to resolve ISignaler and
        /// ILogger if necessary.</param>
        /// <param name="tasksFile">The path to your tasks file,
        /// declaring what tasks your application has scheduled for future
        /// evaluation.</param>
        /// <param name="autoStart">If true, will start service immediately.</param>
        /// <param name="maxSimultaneousTasks">Maximum number of simultaneous tasks.</param>
        public TaskScheduler(IServiceProvider services, string tasksFile, bool autoStart = false, int maxSimultaneousTasks = 4)
        {
            // Sanity checking invocation and decorating instance.
            if (maxSimultaneousTasks < 1 || maxSimultaneousTasks > 64)
                throw new ArgumentException("Max simultaneous tasks must be a positive integer between 1 and 64");

            _services = services ?? throw new ArgumentNullException(nameof(services));
            _tasks = new TaskList(tasksFile ?? throw new ArgumentNullException(nameof(tasksFile)));
            _waiter = new SemaphoreSlim(maxSimultaneousTasks);
            _timer = new Timer(ExecuteNextTask);

            // Starting scheduler if we should.
            if (autoStart)
                Start();
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
            Running = true;
            EnsureTimer();
        }

        /// <summary>
        /// Stops your scheduler, such that no more tasks will be evaluated.
        /// </summary>
        public void Stop()
        {
            Running = false;
        }

        /// <summary>
        /// Creates a new task.
        ///
        /// Notice, will delete any previously created tasks with the same name.
        /// </summary>
        /// <param name="node">Node declaring your task.</param>
        public void AddTask(Node node)
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

        /// <summary>
        /// Returns a previously created task to caller.
        /// </summary>
        /// <param name="name">Name of task you wish to retrieve.</param>
        /// <returns>A node representing your task.</returns>
        public Node GetTask(string name)
        {
            // Getting task with specified name.
            var task = _tasks.GetTask(name);

            // Checking if named task exists.
            if (task == null)
                return null;

            // Creating and returning our result.
            var result = new Node(task.Name, null, task.RootNode.Clone().Children.ToList());

            // Making sure we also return upcoming due date as [due] node.
            result.Add(new Node("due", task.Due));
            return result;
        }

        /// <summary>
        /// Deletes an existing task from your task manager.
        /// </summary>
        /// <param name="name">Name of task to delete.</param>
        public void DeleteTask(string name)
        {
            _tasks.DeleteTask(name);

            /*
             * Need to "retouch" our timer in case task is our first due
             * task in our list of tasks.
             */
            EnsureTimer();
        }

        /// <summary>
        /// Lists all tasks in task manager, in order of evaluation, such that
        /// the first task in queue will be the first task returned.
        /// </summary>
        /// <returns>All tasks listed in chronological order of evaluation.</returns>
        public IEnumerable<string> ListTasks()
        {
            return _tasks.List().Select(x => x.Name).ToList();
        }

        #region [ -- Interface implementations -- ]

        /// <summary>
        /// IDisposable implementation.
        /// </summary>
        public void Dispose()
        {
            Running = false;
            _timer?.Dispose();
            _waiter?.Dispose();
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
                return;

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
             * been queued up but not being able to evaluate at their due dates
             * for some reasons.
             */
            var nextDue = 
                Math.Max(
                    250, 
                    Math.Min((next.Due - now).TotalMilliseconds, new TimeSpan(45, 0, 0, 0).TotalMilliseconds));

            /*
             * Creating our timer, such that it kicks in at next task's due date,
             * or (max) 45 days from now.
             * 
             * Notice, if next task is not due when timer kicks in, the timer will simply
             * be re-created, and nothing else will occur.
             */
            _timer.Change((long)nextDue, Timeout.Infinite);
        }

        /*
         * Executes the next upcoming tasks, if any.
         */
        async void ExecuteNextTask(object state)
        {
            // Ensuring no more than "max threads" are allowed in at the same time.
            await _waiter.WaitAsync();
            try
            {
                /*
                 * Retrieving next due task and preparing it for evaluation.
                 * 
                 * Notice, if no tasks are due, we return early.
                 */
                var current = PrepareTaskForEvaluation();
                if (current == null)
                    return;

                // Making sure we're able to log exceptions.
                try
                {
                    // Retrieving task and its lambda object, and evaluating it.
                    var lambda = current.Lambda.Clone();
                    var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
                    await signaler.SignalAsync("wait.eval", lambda);
                }
                catch (Exception err)
                {
                    // Making sure we log exception using preferred ILogger instance.
                    var logger = _services.GetService(typeof(ILogger)) as ILogger;
                    logger?.LogError(current.Name, err);
                }
            }
            finally
            {
                _waiter.Release();
            }
        }

        /*
         * Prepares the next task for evaluation, and returns it to caller.
         * 
         * Will return null if no tasks are due.
         */
        ScheduledTask PrepareTaskForEvaluation()
        {
            /*
             * Notice, worst case scenario, multiple threads might have overlapping due dates,
             * at which point we could in theory get a race condition during execution of tasks,
             * having multiple threads trying to modify _tasks simultaneously.
             * 
             * To avoid this problem, we make sure all access is synchronized to this method.
             */
            return SynchronizeScheduler.ReadWrite(() =>
            {
                // Verifying that we're still running.
                if (!Running)
                    return null;

                /*
                 * Retrieving next task and checking if it's due, and calculating its next
                 * due date, before we reorder tasks again to sort them according to their due dates.
                 */
                var next = _tasks.NextDueTask();
                if (next == null)
                    return null;

                // Verifying this task actually is due.
                if (next.Due.AddMilliseconds(-100) > DateTime.Now)
                {
                    // First task is still not due, hence re-creating timer and returning null.
                    EnsureTimer();
                    return null;
                }

                // Calculating task's next due date, and reordering tasks afterwards.
                if (next.Repeats)
                {
                    // Task is repeating, hence calculating its next due date, and reordering our tasks.
                    next.CalculateDue();
                    _tasks.Sort();
                }
                else
                {
                    // Task is only supposed to be evaluated once, hence deleting it from list of tasks.
                    _tasks.DeleteTask(next.Name);
                }
                EnsureTimer();
                return next;
            });
        }

        #endregion
    }
}
