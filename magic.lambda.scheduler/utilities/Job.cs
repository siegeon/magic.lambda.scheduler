/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using magic.node;
using magic.node.extensions;
using magic.lambda.scheduler.utilities.jobs;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// Class wrapping a single job, with its repetition pattern, or due date,
    /// and its associated lambda object to be executed when the job is due.
    /// </summary>
    public abstract class Job : IDisposable
    {
        bool _disposed;
        Timer _timer;

        /// <summary>
        /// Protected constructor to avoid direct instantiation, but
        /// forcing instantiation through factory create method instead.
        /// </summary>
        /// <param name="name">The name of your job.</param>
        /// <param name="description">Description for your job.</param>
        /// <param name="lambda">Actual lambda object to be evaluated when job is due.</param>
        protected Job(
            string name, 
            string description, 
            Node lambda)
        {
            Name = name;
            Description = description;
            Lambda = lambda.Clone();
        }

        /// <summary>
        /// Name of job.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Description of job.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Actual lambda object to be executed when job is due.
        /// </summary>
        public Node Lambda { get; private set; }

        /// <summary>
        /// The due date for the next time the job should be executed.
        /// </summary>
        public DateTime Due { get; internal set; }

        /// <summary>
        /// Returns true if this is a repetetive job, implying the job is declared
        /// to be executed multiple times on an interval.
        /// </summary>
        public abstract bool Repeats { get; }

        /// <summary>
        /// Creates a new job according to the declaration found in the specified node.
        /// </summary>
        /// <param name="jobNode">Declaration of job.</param>
        /// <param name="fromPersistentStorage">If true, will fetch the name of the job from the
        /// name of the node instead of from its value.</param>
        /// <returns>Newly created job.</returns>
        public static Job CreateJob(Node jobNode, bool fromPersistentStorage = false)
        {
            // Figuring out what type of job caller requests.
            var repetitionPattern = jobNode.Children.Where(x => x.Name == "repeat" || x.Name == "when");
            if (repetitionPattern.Count() != 1)
                throw new ArgumentException("A job must have exactly one [repeat] or [when] argument.");

            // Finding common arguments for job.
            var name = fromPersistentStorage ? 
                jobNode.Name : 
                jobNode.GetEx<string>() ?? 
                throw new ArgumentException("No name give to job");

            var description = jobNode.Children
                .FirstOrDefault(x => x.Name == "description")?.GetEx<string>();

            var lambda = jobNode.Children
                .FirstOrDefault(x => x.Name == ".lambda") ?? 
                throw new ArgumentException($"No [.lambda] given to job named '{name}'");

            // Creating actual job instance.
            Job result;
            switch (repetitionPattern.First().Name)
            {
                case "repeat":

                    result = RepeatJob.CreateJob(
                        name,
                        description,
                        lambda,
                        repetitionPattern.First().GetEx<string>(),
                        jobNode);
                    break;

                case "when":

                    result = new WhenJob(
                        name,
                        description,
                        lambda,
                        repetitionPattern.First().GetEx<DateTime>());
                    break;

                default:
                    throw new ApplicationException("You have reached a place in your code which should have been impossible to reach!");
            }

            // Notice, we do not actually start a job, before it's added to a scheduler of some sort.
            return result;
        }

        /*
         * Creates the Timer, and its timeout timeout,
         * that will invoke the specified Action at the time the job should be executed.
         */
        internal void Start(Func<Job, Task> callback)
        {
            _timer?.Dispose();
            var now = DateTime.Now;
            var nextDue =
                Math.Max(
                    250,
                    Math.Min((Due - now).TotalMilliseconds, new TimeSpan(45, 0, 0, 0).TotalMilliseconds));
            _timer = new Timer(async (state) => await callback(this), null, (long)nextDue, Timeout.Infinite);
        }

        /*
         * Stops the job from being executed.
         */
        internal void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /*
         * Refreshes the job by calculating the job's next due date some time
         * into the future.
         */
        internal void RefreshDueDate()
        {
            CalculateNextDue();
        }

        /// <summary>
        /// Returns the node representation for this particular instance, such that
        /// it can be serialized to disc, etc.
        /// </summary>
        /// <returns></returns>
        public abstract Node GetNode();

        /*
         * Calculates next due date.
         */
        /// <summary>
        /// Calculates the next due date for the job.
        /// </summary>
        protected abstract void CalculateNextDue();

        #region [ -- Interface implementations -- ]

        /// <summary>
        /// Will dispose the Timer for the job.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposable pattern implementation.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
                _timer?.Dispose();
            _disposed = true;
        }

        #endregion
    }
}
