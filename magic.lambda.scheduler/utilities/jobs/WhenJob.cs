/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;

namespace magic.lambda.scheduler.utilities.jobs
{
    /// <summary>
    /// Class wrapping a single task, with its repetition pattern, or due date,
    /// and its associated lambda object to be evaluated when task is to be evaluated.
    /// </summary>
    public class WhenJob : Job
    {
        /// <summary>
        /// Constructor creating a job that is to be executed only once, and then discarded.
        /// </summary>
        /// <param name="services">Necessary to resolve ISignaler during task evaluation.</param>
        /// <param name="logger">Necessary in case an exception occurs during task evaluation.</param>
        /// <param name="name">The name for your task.</param>
        /// <param name="description">Description for your task.</param>
        /// <param name="lambda">Actual lambda object to be evaluated when task is due.</param>
        /// <param name="when">Date of when job should be executed.</param>
        public WhenJob(
            IServiceProvider services,
            ILogger logger,
            string name, 
            string description, 
            Node lambda,
            DateTime when)
            : base(services, logger, name, description, lambda)
        {
            Due = when;
        }

        /// <summary>
        /// Returns whether or not job should be repeated, which is always false
        /// for this type of job.
        /// </summary>
        public override bool Repeats => false;

        #region [ -- Overridden abstract base class methods -- ]

        internal override DateTime CalculateNextDue()
        {
            return Due;
        }

        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("when", Due));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        #endregion
    }
}
