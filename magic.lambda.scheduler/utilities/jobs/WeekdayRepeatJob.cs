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
    public class WeekdayRepeatJob : RepeatJob
    {
        readonly DayOfWeek _weekday;
        readonly int _hours;
        readonly int _minutes;

        /// <summary>
        /// Constructor creating a job that is to be executed once every given weekday,
        /// at some specified time of the day.
        /// </summary>
        /// <param name="name">The name for your task.</param>
        /// <param name="description">Description for your task.</param>
        /// <param name="lambda">Actual lambda object to be evaluated when task is due.</param>
        /// <param name="weekday">Which day of the week the job should be executed</param>
        /// <param name="hours">At what hour during the day the job should be executed.</param>
        /// <param name="minutes">At what minute, within its hours, the job should be executed.</param>
        public WeekdayRepeatJob(
            string name, 
            string description, 
            Node lambda,
            DayOfWeek weekday,
            int hours,
            int minutes)
            : base(name, description, lambda)
        {
            _weekday = weekday;
            _hours = hours;
            _minutes = minutes;
        }
        #region [ -- Overridden abstract base class methods -- ]


        internal override DateTime CalculateNextDue()
        {
            /*
             * Iterating forwards in time, until we reach a time and weekday matching
             * the specified pattern.
             * 
             * Notice, this implies that no tasks created for example for the current day,
             * on an earlier hour than Now, will be evaluated before a week from now.
             */
            var when = DateTime.Now.ToUniversalTime().Date.AddHours(_hours).AddMinutes(_minutes);
            while (when.AddMilliseconds(250) < DateTime.Now || _weekday != when.DayOfWeek)
            {
                when = when.AddDays(1);
            }
            return when;
        }

        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("repeat", _weekday.ToString(), new Node[] { new Node("time", _hours.ToString("D2") + ":" + _minutes.ToString("D2")) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        #endregion
    }
}
