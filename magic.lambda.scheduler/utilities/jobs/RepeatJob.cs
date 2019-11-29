/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;
using magic.node.extensions;

namespace magic.lambda.scheduler.utilities.jobs
{
    /// <summary>
    /// Class wrapping a single task, with its repetition pattern, or due date,
    /// and its associated lambda object to be evaluated when task is to be evaluated.
    /// </summary>
    public abstract class RepeatJob : Job
    {
        /// <summary>
        /// Constructor creating a job that is to be executed multiple times, according to some sort
        /// of repetition pattern.
        /// </summary>
        /// <param name="name">The name for your task.</param>
        /// <param name="description">Description for your task.</param>
        /// <param name="lambda">Actual lambda object to be evaluated when task is due.</param>
        public RepeatJob(
            string name, 
            string description, 
            Node lambda)
            : base(name, description, lambda)
        { }

        /// <summary>
        /// Returns true if job is repeating, which this particular type of job will always be doing.
        /// </summary>
        public override bool Repeats => true;

        /// <summary>
        /// Virtual constructor method, creating a job that should be repeated according
        /// to some repetition pattern.
        /// </summary>
        /// <param name="name">The name for your task.</param>
        /// <param name="description">Description for your task.</param>
        /// <param name="lambda">Actual lambda object to be evaluated when task is due.</param>
        /// <param name="repetition">String representation of the job's repetition pattern.</param>
        /// <param name="rootTaskNode">Root node for job declaration, necessary to further parametrize
        /// constructors down in the food chain.</param>
        /// <returns>A new RepeatJob of some sort.</returns>
        public static RepeatJob CreateJob(
            string name, 
            string description, 
            Node lambda,
            string repetition,
            Node rootTaskNode)
        {
            RepeatJob job;
            switch (repetition)
            {
                case "Sunday":
                case "Monday":
                case "Tuesday":
                case "Wednesday":
                case "Thursday":
                case "Friday":
                case "Saturday":

                    GetTime(rootTaskNode, out int hoursWeekday, out int minutesWeekday);
                    job = new WeekdayRepeatJob(
                        name,
                        description,
                        lambda,
                        (DayOfWeek)Enum.Parse(typeof(DayOfWeek),repetition),
                        hoursWeekday,
                        minutesWeekday);
                    break;

                case "seconds":
                case "minutes":
                case "hours":
                case "days":

                    job = new EveryEntityRepeatJob(
                        name, 
                        description, 
                        lambda, 
                        (EveryEntityRepeatJob.RepetitionPattern)Enum.Parse(typeof(EveryEntityRepeatJob.RepetitionPattern), repetition), 
                        rootTaskNode.Children
                            .FirstOrDefault(x => x.Name == "repeat")?
                            .Children
                                .FirstOrDefault(x => x.Name == "value")?.GetEx<long>() ?? 
                                throw new ArgumentException($"No [value] supplied to '{repetition}' task during creation."));
                    break;

                case "last-day-of-month":

                    GetTime(rootTaskNode, out int hoursLastDay, out int minutesLastDay);
                    job = new LastDayOfMonthJob(
                        name,
                        description,
                        lambda,
                        hoursLastDay,
                        minutesLastDay);
                    break;

                default:

                    if (!int.TryParse(repetition, out int dayOfMonth) || dayOfMonth < 1 || dayOfMonth > 28)
                        throw new ArgumentException($"I don't know how to create a repeating job with a repeat pattern of '{repetition}'. Did you intend a day of month? If so, value must be between 1 and 28.");

                    GetTime(rootTaskNode, out int hours, out int minutes);
                    job = new EveryXDayOfMonth(
                        name,
                        description,
                        lambda,
                        dayOfMonth,
                        hours,
                        minutes);
                    break;
            }
            job.CalculateNextDue();
            return job;
        }

        #region [ -- Private helper methods -- ]

        static void GetTime(Node rootTaskNode, out int hours, out int minutes)
        {
            var timeEntities = rootTaskNode.Children
                .First(x => x.Name == "repeat").Children
                    .FirstOrDefault(x => x.Name == "time")?.GetEx<string>()?.Split(':') ??
                throw new ArgumentException("No [time] value supplied when trying to create a task.");
            if (timeEntities.Length != 2)
                throw new ArgumentException("[time] must be declared as HH:mm.");
            hours = int.Parse(timeEntities[0]);
            minutes = int.Parse(timeEntities[1]);
        }

        #endregion
    }
}
