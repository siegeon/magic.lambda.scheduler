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
    public class EveryEntityRepeatJob : RepeatJob
    {
        /// <summary>
        /// Repetition pattern for job.
        /// </summary>
        public enum RepetitionPattern
        {
            /// <summary>
            /// Every n second.
            /// </summary>
            seconds,

            /// <summary>
            /// Every n minute.
            /// </summary>
            minutes,

            /// <summary>
            /// Every n hour.
            /// </summary>
            hours,

            /// <summary>
            /// Every n day.
            /// </summary>
            days
        };

        readonly private RepetitionPattern _repetition;
        readonly private long _repetitionValue;

        /// <summary>
        /// Creates a new job that repeat every n days/hours/minutes/seconds.
        /// </summary>
        /// <param name="name">Name of new job.</param>
        /// <param name="description">Description of job.</param>
        /// <param name="lambda">Lambda object to evaluate as job is being executed.</param>
        /// <param name="repetition">Repetition pattern.</param>
        /// <param name="repetitionValue">Number of entities declared through repetition pattern.</param>
        public EveryEntityRepeatJob(
            string name, 
            string description, 
            Node lambda,
            RepetitionPattern repetition,
            long repetitionValue)
            : base(name, description, lambda)
        {
            // Sanity checking and decorating instance.
            _repetition = repetition;
            _repetitionValue = repetitionValue;
        }

        #region [ -- Overridden abstract base class methods -- ]

        /// <summary>
        /// Returns a node representation of the job.
        /// </summary>
        /// <returns>The node representing the job, as supplied when job was created.</returns>
        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("repeat", _repetition.ToString(), new Node[] { new Node("value", _repetitionValue) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        /// <summary>
        /// Calculates the task's next due date.
        /// </summary>
        protected override void CalculateNextDue()
        {
            switch (_repetition)
            {
                case RepetitionPattern.seconds:
                    Due = DateTime.Now.AddSeconds(_repetitionValue);
                    break;

                case RepetitionPattern.minutes:
                    Due = DateTime.Now.AddMinutes(_repetitionValue);
                    break;

                case RepetitionPattern.hours:
                    Due = DateTime.Now.AddHours(_repetitionValue);
                    break;

                case RepetitionPattern.days:
                    Due = DateTime.Now.AddDays(_repetitionValue);
                    break;

                default:
                    throw new ApplicationException("Oops, you've made it into an impossible code branch!");
            }
        }

        #endregion
    }
}
