/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;

namespace magic.lambda.scheduler.utilities.jobs
{
    /*
     * Class wrapping a single task, with its repetition pattern, or due date,
     * and its associated lambda object to be evaluated when task is to be evaluated.
     */
    internal class WhenJob : Job
    {
        public WhenJob(
            string name, 
            string description, 
            Node lambda,
            DateTime when)
            : base(name, description, lambda)
        {
            Due = when;
        }

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
