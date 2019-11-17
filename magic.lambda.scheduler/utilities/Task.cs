/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;
using magic.node.extensions;

namespace magic.lambda.scheduler.utilities
{
    internal class Task : IComparable
    {
        readonly Node _original;

        public Task(Node taskNode)
        {
            // Necessary to calculate *next* evaluation time.
            _original = taskNode.Clone();
            Name = taskNode.Name;
            Lambda = taskNode.Children
                .FirstOrDefault(x => x.Name == ".lambda")?.Clone() ??
                throw new ApplicationException($"No [.lambda] found in task named {Name}");
            CalculateDue();
        }

        public string Name { get; private set; }

        public Node Lambda { get; private set; }

        public DateTime Due { get; private set; }

        public bool Repeats { get; private set; }

        public void CalculateDue()
        {
            // Checking if task repeats, and if not, returning false to caller.
            if (!Repeats)
                return;

            // Figuring out patter, if it's a single task evaluated once, or a repeating pattern.
            var due = _original.Children.Where(x => x.Name == "when" || x.Name == "repeat");
            if (due.Count() != 1)
                throw new ApplicationException($"All tasks must have either a [when] or an [repeat] argument, and exactly one, not both");

            var dueNode = due.First();
            if (dueNode.Name == "when")
            {
                // [when] task, evaluated once, and then discarded afterwards.
                Repeats = false;
                Due = dueNode.GetEx<DateTime>();
            }
            else
            {
                // [repeat] task, evaluated according to some sort of interval.
                Repeats = true;
                var repeat = dueNode.GetEx<string>();
                switch (repeat)
                {
                    case "Sunday":
                    case "Monday":
                    case "Tuesday":
                    case "Wednesday":
                    case "Thursday":
                    case "Friday":
                    case "Saturday":

                        {
                            var timeEntities = dueNode.Children
                                .FirstOrDefault(x => x.Name == "time")?
                                .GetEx<string>()?
                                .Split(':') ?? throw new ApplicationException($"No [time] found in task named {Name}");
                            if (timeEntities.Length != 2)
                                throw new ApplicationException($"Syntax error in [time] in task named {Name}");
                            var hour = int.Parse(timeEntities[0]);
                            var minutes = int.Parse(timeEntities[1]);
                            var weekday = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), repeat);
                            var when = DateTime.Now.Date.AddHours(hour).AddMinutes(minutes);
                            while (when < DateTime.Now && weekday != DateTime.Now.DayOfWeek)
                            {
                                when = when.AddDays(1);
                                weekday = (DayOfWeek)(((int)weekday + 1) % 7);
                            }
                            Due = when;
                        }
                        break;

                    case "seconds":

                        var seconds = dueNode.Children.FirstOrDefault(x => x.Name == "value")?
                            .GetEx<long>() ??
                            throw new ApplicationException($"Syntax error in task named '{Name}', no [value] node found beneath [{dueNode.Name}]"));
                        if (seconds < 5)
                            throw new ArgumentException($"You cannot create a task that repeats more often than every 5 seconds. Task name was '{Name}'");
                        Due = DateTime.Now.AddSeconds(seconds);
                        break;

                    case "minutes":

                        Due = DateTime.Now.AddMinutes(dueNode.Children
                            .FirstOrDefault(x => x.Name == "value")?
                            .GetEx<long>() ??
                            throw new ApplicationException($"Syntax error in task named '{Name}', no [value] node found beneath [{dueNode.Name}]"));
                        break;

                    case "hours":

                        Due = DateTime.Now.AddHours(dueNode.Children
                            .FirstOrDefault(x => x.Name == "value")?
                            .GetEx<long>() ??
                            throw new ApplicationException($"Syntax error in task named '{Name}', no [value] node found beneath [{dueNode.Name}]"));
                        break;

                    case "last-day-of-month":

                        {
                            var timeEntities = dueNode.Children
                                .FirstOrDefault(x => x.Name == "time")?
                                .GetEx<string>()?
                                .Split(':') ?? throw new ApplicationException($"No [time] found in task named {Name}");
                            if (timeEntities.Length != 2)
                                throw new ApplicationException($"Syntax error in [time] in task named {Name}");
                            Due = new DateTime(
                                DateTime.Now.Year,
                                DateTime.Now.Month,
                                DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month),
                                int.Parse(timeEntities[0]),
                                int.Parse(timeEntities[1]),
                                0,
                                DateTimeKind.Utc);
                        }
                        break;

                    default:

                        {
                            var dayOfMonth = int.Parse(repeat);
                            if (dayOfMonth > 28 || dayOfMonth < 1)
                                throw new ApplicationException($"Unknown 'day of month' value in task named '{Name}'. Day of month value was '{dayOfMonth}' and it must be between 1 and 28");
                            var timeEntities = dueNode.Children
                                .FirstOrDefault(x => x.Name == "time")?
                                .GetEx<string>()?
                                .Split(':') ?? throw new ApplicationException($"No [time] found in task named {Name}");
                            if (timeEntities.Length != 2)
                                throw new ApplicationException($"Syntax error in [time] in task named {Name}");
                            var hour = int.Parse(timeEntities[0]);
                            var minutes = int.Parse(timeEntities[1]);
                            var nextDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, dayOfMonth, hour, minutes, 0);
                            if (nextDate < DateTime.Now)
                                Due = nextDate.AddMonths(1);
                            else
                                Due = nextDate;
                        }
                        break;
                }
            }
        }

        public int CompareTo(object obj)
        {
            if (obj is Task rhs)
                return Due.CompareTo(rhs);
            throw new ArgumentException($"You tried to compare a Task to an object of type {obj?.GetType().Name ?? "???"}");
        }
    }
}
