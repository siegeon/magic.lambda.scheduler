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
    public class EveryXDayOfMonth : RepeatJob
    {
        readonly int _dayOfMonth;
        readonly int _hours;
        readonly int _minutes;

        /// <summary>
        /// Creates a new job that repeats every n'th day of the month.
        /// </summary>
        /// <param name="name">Name of new job.</param>
        /// <param name="description">Description of job.</param>
        /// <param name="lambda">Lambda object to be executed whne job is due.</param>
        /// <param name="dayOfMonth">Which day of the month job should execute. Integer value between 1 and 28.</param>
        /// <param name="hours">At which time of the day the job should execute. Integer value between 0 and 23.</param>
        /// <param name="minutes">At which minute within the hour the job should execute. Integer value between 0 and 59.</param>
        public EveryXDayOfMonth(
            string name, 
            string description, 
            Node lambda,
            int dayOfMonth,
            int hours,
            int minutes)
            : base(name, description, lambda)
        {
            if (dayOfMonth < 0 || dayOfMonth > 28)
                throw new ArgumentException($"{nameof(dayOfMonth)} must be between 0 and 28");
            if (hours < 0 || hours > 23)
                throw new ArgumentException($"{nameof(hours)} must be between 0 and 23");
            if (minutes < 0 || minutes > 59)
                throw new ArgumentException($"{nameof(hours)} must be between 0 and 59");
            _dayOfMonth = dayOfMonth;
            _hours = hours;
            _minutes = minutes;
        }

        #region [ -- Overridden abstract base class methods -- ]

        /// <summary>
        /// Returns a node representation of the job.
        /// </summary>
        /// <returns>Node representing the job as when cerated.</returns>
        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("repeat", _dayOfMonth, new Node[] { new Node("time", _hours.ToString("D2") + ":" + _minutes.ToString("D2")) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        /// <summary>
        /// Calculates the next due date for the job.
        /// </summary>
        protected override void CalculateNextDue()
        {
            var nextDate = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                _dayOfMonth,
                _hours,
                _minutes,
                0,
                DateTimeKind.Utc);
            if (nextDate.AddMilliseconds(250) < DateTime.Now)
                Due = nextDate.AddMonths(1);
            else
                Due = nextDate;
        }

        #endregion
    }
}
