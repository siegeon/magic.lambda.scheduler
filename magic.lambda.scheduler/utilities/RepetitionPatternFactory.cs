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
    public sealed class RepetitionPatternFactory
    {
        readonly IPattern _pattern;

        /// <summary>
        /// Creates a new instance of the class.
        /// </summary>
        /// <param name="pattern">Repetition pattern to use.</param>
        public RepetitionPatternFactory(string pattern)
        {
            var entities = pattern.Split('.');
            switch(entities.Length)
            {
                case 2:
                    _pattern = new IntervalPattern(int.Parse(entities[0]), entities[1]);
                    break;
                case 4:
                    var weekdays = entities[0] == "**" ?
                        null :
                        entities[0].Split('|')
                            .Select(x => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), x, true));
                    _pattern = new WeekdayPattern(
                        weekdays,
                        int.Parse(entities[1]),
                        int.Parse(entities[2]),
                        int.Parse(entities[3]));
                    break;
                case 5:
                    var months = entities[0] == "**" ? null : entities[0].Split('|').Select(x => int.Parse(x));
                    var days = entities[1] == "**" ? null : entities[1].Split('|').Select(x => int.Parse(x));
                    _pattern = new MonthPattern(
                        months,
                        days,
                        int.Parse(entities[2]),
                        int.Parse(entities[3]),
                        int.Parse(entities[4]));
                    break;
                default:
                    throw new ArgumentException($"'{pattern}' is not a recognized repetition pattern.");
            }
        }

        /// <summary>
        /// Returns the repetition pattern this instance is encapsulating.
        /// </summary>
        /// <value>Actual pattern.</value>
        public string Pattern { get => _pattern.Value; }

        /// <summary>
        /// Calculates the next due date according to the repetition pattern specified during creation.
        /// </summary>
        /// <returns>Next execution date and time for instance.</returns>
        public DateTime Next()
        {
            return _pattern.Next();
        }
    }
}
