/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.lambda.scheduler.utilities.patterns;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// Class encapsulating a repetition pattern, helping you to find the next DateTime
    /// to execute a task, according to a given pattern.
    /// </summary>
    public static class PatternFactory
    {
        /// <summary>
        /// Creates a new instance of the class.
        /// </summary>
        /// <param name="pattern">Repetition pattern to use.</param>
        /// <returns>An instance of an IPattern.</returns>
        public static IPattern Create(string pattern)
        {
            var entities = pattern.Split('.');
            switch(entities.Length)
            {
                case 2:
                    return new IntervalPattern(int.Parse(entities[0]), entities[1]);
                case 4:
                    var weekdays = entities[0] == "**" ?
                        null :
                        entities[0].Split('|')
                            .Select(x => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), x, true));
                    return new WeekdayPattern(
                        weekdays,
                        int.Parse(entities[1]),
                        int.Parse(entities[2]),
                        int.Parse(entities[3]));
                case 5:
                    var months = entities[0] == "**" ? null : entities[0].Split('|').Select(x => int.Parse(x));
                    var days = entities[1] == "**" ? null : entities[1].Split('|').Select(x => int.Parse(x));
                    return new MonthPattern(
                        months,
                        days,
                        int.Parse(entities[2]),
                        int.Parse(entities[3]),
                        int.Parse(entities[4]));
                default:
                    throw new ArgumentException($"'{pattern}' is not a recognized repetition pattern.");
            }
        }
    }
}
