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
    internal class LastDayOfMonthJob : RepeatJob
    {
        readonly int _hours;
        readonly int _minutes;

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

        internal override DateTime CalculateNextDue()
        {
            /*
             * Constructing the next due date for the task, which will be the current
             * month, and on its last day, assuming the time has not passed - At which
             * point we'll have to wait to evaluate the task.
             */
            var candidate = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month),
                _hours,
                _minutes,
                0,
                DateTimeKind.Utc);

            /*
             * Checking if time is in the past, at which point we postpone it one
             * additional month into the future.
             */
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

            // Returning due date.
            return candidate;
        }

        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("repeat", "last-day-of-month", new Node[] { new Node("time", _hours.ToString("D2") + ":" + _minutes.ToString("D2")) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        #endregion
    }
}
