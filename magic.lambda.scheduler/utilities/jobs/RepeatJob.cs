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
    /// Class wrapping a single repeating job, with its repetition pattern,
    /// and its associated lambda object to be executed when the job is due.
    /// </summary>
    public abstract class RepeatJob : Job
    {
        /// <summary>
        /// Constructor creating a job that is to be executed multiple times, according to some sort
        /// of repetition pattern.
        /// </summary>
        /// <param name="name">The name of your job.</param>
        /// <param name="description">Description for your job.</param>
        /// <param name="lambda">Actual lambda object to be executed when job is due.</param>
        protected RepeatJob(
            string name, 
            string description, 
            Node lambda)
            : base(name, description, lambda)
        { }

        /// <summary>
        /// Returns true if job is repeating.
        /// 
        /// Notice, this type of job will always return true.
        /// </summary>
        public override bool Repeats => true;

        /// <summary>
        /// Factory constructor method, creating a job that should be repeated according
        /// to some repetition pattern.
        /// </summary>
        /// <param name="name">The name of your job.</param>
        /// <param name="description">Description for your job.</param>
        /// <param name="lambda">Actual lambda object to be evaluated when job is due.</param>
        /// <param name="repetition">String representation of the job's repetition pattern.</param>
        /// <param name="rootJobNode">Root node for job declaration, necessary to further parametrize
        /// constructors downwards in the food chain.</param>
        /// <returns>A new RepeatJob of some sort.</returns>
        public static RepeatJob CreateJob(
            string name, 
            string description, 
            Node lambda,
            string repetition,
            Node rootJobNode)
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

                    GetTime(rootJobNode, out int hoursWeekday, out int minutesWeekday);
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
                        (EveryEntityRepeatJob.RepetitionEntityType)Enum.Parse(typeof(EveryEntityRepeatJob.RepetitionEntityType), repetition), 
                        rootJobNode.Children
                            .FirstOrDefault(x => x.Name == "repeat")?
                            .Children
                                .FirstOrDefault(x => x.Name == "value")?.GetEx<long>() ?? 
                                throw new ArgumentException($"No [value] supplied to '{repetition}' job during creation."));
                    break;

                case "last-day-of-month":

                    GetTime(rootJobNode, out int hoursLastDay, out int minutesLastDay);
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

                    GetTime(rootJobNode, out int hours, out int minutes);
                    job = new EveryXDayOfMonth(
                        name,
                        description,
                        lambda,
                        dayOfMonth,
                        hours,
                        minutes);
                    break;
            }

            // Calculating next due date, but not starting the job, before it's added to some scheduler of some sort.
            return job;
        }

        #region [ -- Private helper methods -- ]

        /*
         * Parses a string in "HH:mm" format, returning hours and minutes to caller as out parameters.
         */
        static void GetTime(Node rootJobNode, out int hours, out int minutes)
        {
            var timeEntities = rootJobNode.Children
                .First(x => x.Name == "repeat").Children
                    .FirstOrDefault(x => x.Name == "time")?.GetEx<string>()?.Split(':') ??
                throw new ArgumentException("No [time] value supplied when trying to create a job.");

            if (timeEntities.Length != 2)
                throw new ArgumentException("[time] must be declared as HH:mm.");

            hours = int.Parse(timeEntities[0]);
            minutes = int.Parse(timeEntities[1]);
        }

        #endregion
    }
}
