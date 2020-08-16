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
        readonly int? _seconds;
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
                _entity = entities[1];
                _interval = int.Parse(entities[0]);
            }
            else
            {
                // Every n'th type of repetition pattern.
                if (entities.Length != 6)
                    throw new ArgumentException("A repetition pattern must contain 6 or 2 entities separated by '.'");
                if (entities[0] != "**")
                    _month = entities[0].Split('|').Select(x => int.Parse(x)).ToArray();
                if (entities[1] != "**")
                    _day = entities[1].Split('|').Select(x => int.Parse(x)).ToArray();
                if (entities[2] != "**")
                    _hour = int.Parse(entities[2]);
                if (entities[3] != "**")
                    _minute = int.Parse(entities[3]);
                if (entities[4] != "**")
                    _seconds = int.Parse(entities[4]);
                if (entities[5] != "**")
                {
                    // Weekday repetition pattern.
                    var days = entities[5].Split('|');
                    _weekdays = days.Select(x => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), x, true)).ToArray();
                    if (_month != null || _day != null)
                        throw new ArgumentException("You cannot combine weekdays repetition pattern with month and day pattern");
                    if (_hour == null || _minute == null || _seconds == null)
                        throw new ArgumentException("A weekday pattern must be combined with hour, minutes and seconds");
                }
                else
                {
                    // Month/Day repetition pattern.
                    if (_hour == null || _minute == null || _seconds == null)
                        throw new ArgumentException("A month/day pattern must be combined with hour, minutes and seconds");
                    if (_day == null)
                        throw new ArgumentException("A month/day pattern must have at least a day value");
                }
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
                // Every n'th repetition pattern.
                if (_weekdays != null)
                {
                    // Weekday pattern.
                    result = new DateTime(result.Year, result.Month, result.Day, _hour.Value, _minute.Value, _seconds.Value);
                    while(true)
                    {
                        if (result > DateTime.UtcNow && _weekdays.Any(x => x == result.DayOfWeek))
                            return result;
                        result = result.AddDays(1);
                    }
                }
                else
                {
                    // Month/Day pattern.
                    result = new DateTime(result.Year, result.Month, result.Day, _hour.Value, _minute.Value, _seconds.Value);
                    while(true)
                    {
                        if (result > DateTime.UtcNow &&
                            (_month == null || _month.Any(x => result.Month == x)) &&
                            _day.Any(x => result.Day == x))
                            return result;
                        result = result.AddDays(1);
                    }
                }
            }
            else
            {
                switch(_entity)
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
        }
    }
}
