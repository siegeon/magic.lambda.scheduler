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
    public class LastDayOfMonthJob : RepeatJob
    {
        readonly int _hours;
        readonly int _minutes;

        /// <summary>
        /// Creates a new job that only executes on the very last day of the month, at some specific
        /// hour and minute during the day.
        /// </summary>
        /// <param name="name">Name of job.</param>
        /// <param name="description">Description for job.</param>
        /// <param name="lambda">Lambd aobject to be executed when job is due.</param>
        /// <param name="hours">At which hour during the day the job should execute.</param>
        /// <param name="minutes">At which minute, within the hour, the job should execute.</param>
        public LastDayOfMonthJob(
            string name, 
            string description, 
            Node lambda,
            int hours,
            int minutes)
            : base(name, description, lambda)
        {
            if (hours < 0 || hours > 23)
                throw new ArgumentException($"{nameof(hours)} must be between 0 and 23");
            if (minutes < 0 || minutes > 59)
                throw new ArgumentException($"{nameof(hours)} must be between 0 and 59");
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
            result.Add(new Node("repeat", "last-day-of-month", new Node[] { new Node("time", _hours.ToString("D2") + ":" + _minutes.ToString("D2")) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        /// <summary>
        /// Calculates the next due date for the job.
        /// </summary>
        protected override void CalculateNextDue()
        {
            var candidate = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month),
                _hours,
                _minutes,
                0,
                DateTimeKind.Utc);

            if (candidate < DateTime.Now)
            {
                // Shifting date one month ahead, since candidate due date has passed.
                var year = DateTime.Now.Month == 12 ? DateTime.Now.Year + 1 : DateTime.Now.Year;
                var month = DateTime.Now.Month == 12 ? 1 : DateTime.Now.Month + 1;
                candidate = new DateTime(
                    year,
                    month,
                    DateTime.DaysInMonth(year, month),
                    _hours,
                    _minutes,
                    0,
                    DateTimeKind.Utc);
            }
            Due = candidate;
        }

        #endregion
    }
}
