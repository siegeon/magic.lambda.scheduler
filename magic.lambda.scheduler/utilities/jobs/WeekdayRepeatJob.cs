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
    /// Class wrapping a single job, with a weekly repetition pattern,
    /// and its associated lambda object to be executed when job is to be executed.
    /// </summary>
    public class WeekdayRepeatJob : RepeatJob
    {
        readonly DayOfWeek _weekday;
        readonly int _hours;
        readonly int _minutes;

        /// <summary>
        /// Constructor creating a job that is to be executed once every specified weekday,
        /// at some specified time of the day.
        /// </summary>
        /// <param name="name">The name of your job.</param>
        /// <param name="description">Description of your job.</param>
        /// <param name="lambda">Actual lambda object to be executed when job is due.</param>
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

        /// <summary>
        /// Returns the node representation of the job.
        /// </summary>
        /// <returns>A node representing the declaration of the job as when created.</returns>
        public override Node GetNode()
        {
            var result = new Node(Name);
            if (!string.IsNullOrEmpty(Description))
                result.Add(new Node("description", Description));
            result.Add(new Node("repeat", _weekday.ToString(), new Node[] { new Node("time", _hours.ToString("D2") + ":" + _minutes.ToString("D2")) }));
            result.Add(new Node(".lambda", null, Lambda.Children.Select(x => x.Clone())));
            return result;
        }

        #region [ -- Overridden abstract base class methods -- ]

        /// <summary>
        /// Calculates the next due date for the job.
        /// </summary>
        protected override void CalculateNextDue()
        {
            var when = DateTime.Now.ToUniversalTime().Date.AddHours(_hours).AddMinutes(_minutes);
            while (when.AddMilliseconds(250) < DateTime.Now || _weekday != when.DayOfWeek)
            {
                when = when.AddDays(1);
            }
            Due = when;
        }

        #endregion
    }
}
