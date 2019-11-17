/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading;
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
        readonly IServiceProvider _services;
        readonly Tasks _tasks;
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
            _tasks = new Tasks(tasksFile ?? throw new ArgumentNullException(nameof(tasksFile)));
        }

        /// <summary>
        /// Starts your scheduler. You must invoke this method in order to
        /// start your scheduler.
        /// </summary>
        public void Start()
        {
            _running = true;
            CreateTimer();
        }

        /// <summary>
        /// Stops your scheduler, such that no more tasks will be evaluated.
        /// </summary>
        public void Stop()
        {
            _running = false;
            _timer?.Dispose();
            _timer = null;
        }

        #region [ -- Interface implementations -- ]

        public void Dispose()
        {
            _running = false;
            _timer?.Dispose();
            _timer = null; // In case instance is disposed twice.
        }

        #endregion

        #region [ -- Private helper methods -- ]

        void CreateTimer()
        {
            /*
             * Checking if scheduler has been explicitly stopped.
             */
            if (!_running)
                return; // Scheduler has been explicitly stopped.

            // Disposing old timer.
            _timer?.Dispose();
            _timer = null; // To avoid disposing timer twice.

            // Retrieving next task.
            var next = _tasks.NextTask();
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
                    (state) => CreateTimer(),
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

        async void ExecuteNextTask(object state)
        {
            /*
             * Verifying that scheduler has not been explicitly stopped.
             */
            if (!_running)
                return;

            /*
             * Verifying that we have an upcoming task, and if
             * not returning early.
             */
            var next = _tasks.NextTask();
            if (next == null)
                return;

            /*
             * We can never allow for exceptions to propagate out of this
             * method due to "async void" signature.
             */
            try
            {
                /*
                 * Making sure we reorder list of tasks before we evaluate task,
                 * in case of exceptions.
                 */
                if (next.Repeats)
                {
                    /*
                     * Task repeats, making sure we calculate next due date, for
                     * then to reorder task list.
                     */
                    next.CalculateDue();
                    _tasks.Sort();
                }
                else
                {
                    // Task does not repeat.
                    _tasks.DeleteTask(next);
                }

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

            // Re-creating our timer.
            CreateTimer();
        }

        #endregion
    }
}
