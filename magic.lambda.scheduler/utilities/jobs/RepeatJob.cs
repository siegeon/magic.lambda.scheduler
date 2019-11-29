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
    /*
     * Class wrapping a single task, with its repetition pattern, or due date,
     * and its associated lambda object to be evaluated when task is to be evaluated.
     */
    internal abstract class RepeatJob : Job
    {
        public RepeatJob(
            string name, 
            string description, 
            Node lambda)
            : base(name, description, lambda)
        { }

        public static RepeatJob CreateJob(
            string name, 
            string description, 
            Node lambda,
            string repetition,
            Node rootTaskNode)
        {
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
                    return new WeekdayRepeatJob(
                        name,
                        description,
                        lambda,
                        (DayOfWeek)Enum.Parse(typeof(DayOfWeek),repetition),
                        hoursWeekday,
                        minutesWeekday);

                case "seconds":
                case "minutes":
                case "hours":
                case "days":

                    return new EveryEntityRepeatJob(
                        name, 
                        description, 
                        lambda, 
                        (EveryEntityRepeatJob.RepetitionPattern)Enum.Parse(typeof(EveryEntityRepeatJob.RepetitionPattern), repetition), 
                        rootTaskNode.Children
                            .FirstOrDefault(x => x.Name == "repeat")?
                            .Children
                                .FirstOrDefault(x => x.Name == "value")?.GetEx<long>() ?? 
                                throw new ArgumentException($"No [value] supplied to '{repetition}' task during creation."));

                case "last-day-of-month":

                    GetTime(rootTaskNode, out int hoursLastDay, out int minutesLastDay);
                    return new LastDayOfMonthJob(
                        name,
                        description,
                        lambda,
                        hoursLastDay,
                        minutesLastDay);

                default:

                    // Checking if repetition value is an integer between 1 and 28.
                    if (int.TryParse(repetition, out int dayOfMonth))
                    {
                        GetTime(rootTaskNode, out int hours, out int minutes);
                        return new EveryXDayOfMonth(
                            name,
                            description,
                            lambda,
                            dayOfMonth,
                            hours,
                            minutes);
                    }
                    else
                    {
                        throw new ArgumentException($"I don't know how to create a repeating job with a repeat pattern of '{repetition}'. Did you intend a day of month? If so, value must be between 1 and 28.");
                    }
            }
        }

        public override bool Repeats => true;

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
