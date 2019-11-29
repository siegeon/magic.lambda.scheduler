﻿/*
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
    /// and its associated lambda object to be evaluated when task is to be evaluated.
    /// </summary>
    public abstract class Job : IDisposable
    {
        readonly IServiceProvider _provider;
        readonly ILogger _logger;
        bool _disposed;
        Timer _timer;

        /// <summary>
        /// Protected constructor to avoid direct instantiation, but
        /// forcing using factory create method instead.
        /// </summary>
        /// <param name="services">Necessary to resolve ISignaler during task evaluation.</param>
        /// <param name="logger">Necessary in case an exception occurs during task evaluation.</param>
        /// <param name="name">The name for your task.</param>
        /// <param name="description">Description for your task.</param>
        /// <param name="lambda">Actual lambda object to be evaluated when task is due.</param>
        protected Job(
            IServiceProvider services,
            ILogger logger,
            string name, 
            string description, 
            Node lambda)
        {
            _provider = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Name = name;
            Description = description;
            Lambda = lambda.Clone();
        }

        /// <summary>
        /// Name of task.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Description of task.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Actual lambda object to be evaluated as task is due.
        /// </summary>
        public Node Lambda { get; private set; }

        /// <summary>
        /// The due date for the next time the task should be evaluated.
        /// </summary>
        public DateTime Due { get; internal set; }

        /// <summary>
        /// Returns true if this is a repetetive task, implying the task is declared
        /// to be evaluated multiple times.
        /// </summary>
        public abstract bool Repeats { get; }

        /// <summary>
        /// Creates a new Job according to the declaration found in the specified Node instance.
        /// </summary>
        /// <param name="services">Service provider that is necessary to later resolve ISignaler as the task is to be evaluated.</param>
        /// <param name="logger">Logger necessary in case task execution triggers an unhandled exception.</param>
        /// <param name="taskNode">Declaration of task.</param>
        /// <returns></returns>
        public static Job CreateJob(
            IServiceProvider services,
            ILogger logger,
            Node taskNode)
        {
            // Figuring out what type of job caller requests.
            var repetitionPattern = taskNode.Children.Where(x => x.Name == "repeat" || x.Name == "when");
            if (repetitionPattern.Count() != 1)
                throw new ArgumentException("A task must have exactly one [repeat] or [when] argument.");

            // Finding common arguments for job.
            var name = taskNode.GetEx<string>() ?? throw new ArgumentException("No name give to task");
            var description = taskNode.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>();
            var lambda = taskNode.Children.FirstOrDefault(x => x.Name == ".lambda") ?? throw new ArgumentException($"No [.lambda] given to task named '{name}'");

            // Creating actual job instance.
            Job result;
            switch (repetitionPattern.First().Name)
            {
                case "repeat":

                    result = RepeatJob.CreateJob(
                        services,
                        logger,
                        name,
                        description,
                        lambda,
                        repetitionPattern.First().GetEx<string>(),
                        taskNode);
                    break;

                case "when":

                    result = new WhenJob(
                        services,
                        logger,
                        name,
                        description,
                        lambda,
                        repetitionPattern.First().GetEx<DateTime>());
                    break;

                default:
                    throw new ApplicationException("You have reached a place in your code which should have been impossible to reach!");
            }
            result.Due = result.CalculateNextDue();
            return result;
        }

        /*
         * Creates the Timer timeout, that invokes the specified Action at the time the task should be evaluated.
         */
        internal void EnsureTimer(Func<Job, Task> callback)
        {
            var now = DateTime.Now;
            var nextDue =
                Math.Max(
                    250,
                    Math.Min((Due - now).TotalMilliseconds, new TimeSpan(45, 0, 0, 0).TotalMilliseconds));
            _timer = new Timer(async (state) => await callback(this), null, (int)nextDue, Timeout.Infinite);
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
        internal abstract DateTime CalculateNextDue();

        #region [ -- Interface implementations -- ]

        /// <summary>
        /// Will dispose the Timer for the task.
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
                _timer.Dispose();

            _disposed = true;
        }

        #endregion
    }
}
