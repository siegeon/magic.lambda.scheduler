﻿/*
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

        public RepetitionPattern(string pattern)
        {
            _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            var entities = _pattern.Split('.');
            if (entities.Length != 6)
                throw new ArgumentException("A repetition pattern must contain 6 entities separated by '.'");
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
                _weekdays = days.Select(x => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), x)).ToArray();
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

        public DateTime Next()
        {
            // Finding Now, and removing milliseconds from it.
            DateTime result = DateTime.Now;
            result = new DateTime(result.Year, result.Month, result.Day, result.Hour, result.Minute, result.Second, 0);
            if (_weekdays != null)
            {
                // Weekday pattern.
                result = new DateTime(result.Year, result.Month, result.Day, _hour.Value, _minute.Value, _seconds.Value);
                while(true)
                {
                    if (result > DateTime.Now && _weekdays.Any(x => x == result.DayOfWeek))
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
                    if (result > DateTime.Now &&
                        (_month == null || _month.Any(x => result.Month == x)) &&
                        _day.Any(x => result.Day == x))
                        return result;
                    result = result.AddDays(1);
                }
            }
        }
    }
}
