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
    internal class EveryXDayOfMonth : RepeatJob
    {
        readonly int _dayOfMonth;
        readonly int _hours;
        readonly int _minutes;

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

        internal override DateTime CalculateNextDue()
        {
            /*
             * Figuring out when to evaluate the task next time.
             * Notice, we never allow for creating tasks in the past, hence if due date has passed,
             * we simply add one month to its due date.
             */
            var nextDate = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                _dayOfMonth,
                _hours,
                _minutes,
                0,
                DateTimeKind.Utc);
            if (nextDate.AddMilliseconds(250) < DateTime.Now)
                return nextDate.AddMonths(1);
            else
                return nextDate;
        }

        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("repeat", _dayOfMonth, new Node[] { new Node("time", _hours.ToString("D2") + ":" + _minutes.ToString("D2")) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        #endregion
    }
}
