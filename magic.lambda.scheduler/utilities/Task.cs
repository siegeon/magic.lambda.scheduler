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
        internal readonly Node _original;

        public Task(Node taskNode)
        {
            if (!taskNode.Children.Any(x => x.Name == ".lambda"))
                throw new ArgumentException($"No [.lambda] supplied to task named {taskNode.GetEx<string>()}");

            _original = taskNode.Clone();
            Name = taskNode.Name;
            CalculateDue(true);
        }

        public string Name { get; private set; }

        public Node Lambda => _original.Children.First(x => x.Name == ".lambda");

        public DateTime Due { get; private set; }

        public bool Repeats { get; private set; }

        public void CalculateDue(bool first = false)
        {
            // Checking if task repeats, and if not, returning false to caller.
            if (!first && !Repeats)
                return;

            // Figuring out patter, if it's a single task evaluated once, or a repeating pattern.
            var due = _original.Children.Where(x => x.Name == "when" || x.Name == "repeat");
            if (due.Count() != 1)
                throw new ArgumentException($"All tasks must have either a [when] or an [repeat] argument, and exactly one, not both");

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
                        } break;

                    case "seconds":

                        {
                            var seconds = dueNode.Children.FirstOrDefault(x => x.Name == "value")?
                                .GetEx<long>() ??
                                throw new ApplicationException($"Syntax error in task named '{Name}', no [value] node found beneath [{dueNode.Name}]");
                            if (seconds < 5)
                                throw new ArgumentException($"You cannot create a task that repeats more often than every 5 seconds. Task name was '{Name}'");
                            Due = DateTime.Now.AddSeconds(seconds);
                        } break;

                    case "minutes":

                        {
                            var minutes = dueNode.Children.FirstOrDefault(x => x.Name == "value")?
                                .GetEx<long>() ??
                                throw new ApplicationException($"Syntax error in task named '{Name}', no [value] node found beneath [{dueNode.Name}]");
                            if (minutes < 1)
                                throw new ArgumentException($"Minutes for task named '{Name}' must be a positive integer");
                            Due = DateTime.Now.AddMinutes(minutes);
                        } break;

                    case "hours":

                        {
                            var hours = dueNode.Children.FirstOrDefault(x => x.Name == "value")?
                                .GetEx<long>() ??
                                throw new ApplicationException($"Syntax error in task named '{Name}', no [value] node found beneath [{dueNode.Name}]");
                            if (hours < 1)
                                throw new ArgumentException($"Hours for task named '{Name}' must be a positive integer");
                            Due = DateTime.Now.AddHours(hours);
                        } break;

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
                        } break;

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
                        } break;
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
