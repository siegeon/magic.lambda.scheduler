/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// Class encapsulating a repetition pattern, helping you to find the next DateTime
    /// to execute a task, according to a given pattern.
    /// </summary>
    public sealed class RepetitionPattern
    {
        readonly string _pattern;
        readonly int[] _month;
        readonly int[] _day;
        readonly int? _hour;
        readonly int? _minute;
        readonly int? _second;
        readonly DayOfWeek[] _weekdays;
        readonly string _entity;
        readonly int _interval;

        /// <summary>
        /// Creates a new instance of the class.
        /// </summary>
        /// <param name="pattern">Repetition pattern to use.</param>
        public RepetitionPattern(string pattern)
        {
            _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            var entities = _pattern.Split('.');
            if (entities.Length == 2)
            {
                // Interval type of repetition pattern.
                var result = CreateIntervalPattern(entities);
                _entity = result.Entity;
                _interval = result.Interval;
            }
            else
            {
                // Every n'th type of repetition pattern.
                var result = CreateTimeStampPattern(entities);
                _month = result.Months;
                _day = result.Days;
                _hour = result.Hour;
                _minute = result.Minute;
                _second = result.Second;
                _weekdays = result.Weekdays;
            }
        }

        /// <summary>
        /// Returns the repetition pattern this instance is encapsulating.
        /// </summary>
        /// <value>Actual pattern.</value>
        public string Pattern { get => _pattern; }

        /// <summary>
        /// Calculates the next due date according to the repetition pattern specified during creation.
        /// </summary>
        /// <returns>Next execution date and time for instance.</returns>
        public DateTime Next()
        {
            // Finding Now, and removing milliseconds from it.
            DateTime result = DateTime.UtcNow;
            result = new DateTime(result.Year, result.Month, result.Day, result.Hour, result.Minute, result.Second, 0);
            if (_entity == null)
            {
                return CalculateNextDateTime(result);
            }
            else
            {
                return CalculateNextIntervalDate(result);
            }
        }

        #region [ -- Private helper methods -- ]

        /*
         * Creates an interval type of repetition pattern, such as "5.seconds", etc.
         */
        (string Entity, int Interval) CreateIntervalPattern(string[] entities)
        {
            // Interval type of repetition pattern.
            switch(entities[1])
            {
                case "seconds":
                case "minutes":
                case "hours":
                case "days":
                case "weeks":
                case "months":
                    break;
                default:
                    throw new ArgumentException("You can only use seconds, minutes, hours, days, weeks and months in an interval type of repetition pattern.");
            }
            return (entities[1], int.Parse(entities[0]));
        }

        /*
         * Creates a timestamp type of repetition pattern, such as "**.5|15.23.59.11.**", etc.
         */
        (int[] Months, int[] Days, int? Hour, int? Minute, int? Second, DayOfWeek[] Weekdays) CreateTimeStampPattern(string[] entities)
        {
            // Every n'th type of repetition pattern.
            if (entities.Length != 6)
                throw new ArgumentException("A repetition pattern must contain 6 or 2 entities separated by '.'");

            int[] months = null;
            int[] days = null;
            int? hour = null;
            int? minute = null;
            int? second = null;
            DayOfWeek[] weekdays = null;

            if (entities[0] != "**")
                months = entities[0].Split('|').Select(x => int.Parse(x)).ToArray();

            if (entities[1] != "**")
                days = entities[1].Split('|').Select(x => int.Parse(x)).ToArray();

            if (entities[2] != "**")
                hour = int.Parse(entities[2]);

            if (entities[3] != "**")
                minute = int.Parse(entities[3]);

            if (entities[4] != "**")
                second = int.Parse(entities[4]);

            if (entities[5] != "**")
            {
                // Weekday repetition pattern.
                if (months != null || days != null)
                    throw new ArgumentException("You cannot combine weekdays repetition pattern with month and day pattern");
                if (hour == null || minute == null || second == null)
                    throw new ArgumentException("A weekday pattern must be combined with hour, minutes and seconds");
                weekdays = entities[5]
                    .Split('|')
                    .Select(x => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), x, true)).ToArray();
            }
            else
            {
                // Month/Day repetition pattern.
                if (hour == null || minute == null || second == null)
                    throw new ArgumentException("A month/day pattern must be combined with hour, minutes and seconds");
                if (days == null)
                    throw new ArgumentException("A month/day pattern must have at least a day value");
            }
            return (months, days, hour, minute, second, weekdays);
        }

        DateTime CalculateNextDateTime(DateTime result)
        {
            // Every n'th repetition pattern.
            if (_weekdays != null)
            {
                // Weekday pattern.
                result = new DateTime(result.Year, result.Month, result.Day, _hour.Value, _minute.Value, _second.Value);
                while (true)
                {
                    if (result > DateTime.UtcNow && _weekdays.Any(x => x == result.DayOfWeek))
                        return result;
                    result = result.AddDays(1);
                }
            }
            else
            {
                // Month/Day pattern.
                result = new DateTime(result.Year, result.Month, result.Day, _hour.Value, _minute.Value, _second.Value);
                while (true)
                {
                    if (result > DateTime.UtcNow &&
                        (_month == null || _month.Any(x => result.Month == x)) &&
                        _day.Any(x => result.Day == x))
                        return result;
                    result = result.AddDays(1);
                }
            }
        }

        DateTime CalculateNextIntervalDate(DateTime result)
        {
            switch (_entity)
            {
                case "seconds":
                    return result.AddSeconds(_interval);
                case "minutes":
                    return result.AddMinutes(_interval);
                case "hours":
                    return result.AddHours(_interval);
                case "days":
                    return result.AddDays(_interval);
                case "weeks":
                    return result.AddDays(_interval * 7);
                case "months":
                    return result.AddMonths(_interval);
                default:
                    throw new ArgumentException("You cannot possibly have reached this code!");
            }
        }

        #endregion
    }
}
