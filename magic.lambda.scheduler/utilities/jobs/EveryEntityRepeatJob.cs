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
    internal class EveryEntityRepeatJob : RepeatJob
    {
        public enum RepetitionPattern
        {
            seconds,
            minutes,
            hours,
            days
        };

        readonly private RepetitionPattern _entity;
        readonly private long _entityValue;

        public EveryEntityRepeatJob(
            IServiceProvider services,
            ILogger logger,
            string name, 
            string description, 
            Node lambda,
            RepetitionPattern entity,
            long entityValue)
            : base(services, logger, name, description, lambda)
        {
            // Sanity checking and decorating instance.
            _entity = entity;
            _entityValue = entityValue;
        }

        #region [ -- Overridden abstract base class methods -- ]

        internal override DateTime CalculateNextDue()
        {
            switch (_entity)
            {
                case RepetitionPattern.seconds:
                    return DateTime.Now.AddSeconds(_entityValue);

                case RepetitionPattern.minutes:
                    return DateTime.Now.AddMinutes(_entityValue);

                case RepetitionPattern.hours:
                    return DateTime.Now.AddHours(_entityValue);

                case RepetitionPattern.days:
                    return DateTime.Now.AddDays(_entityValue);
            }
            throw new ApplicationException("Oops, you've made it into an impossible code branch!");
        }

        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("repeat", _entity.ToString(), new Node[] { new Node("value", _entityValue) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        #endregion
    }
}
