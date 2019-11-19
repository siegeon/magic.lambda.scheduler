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
    /*
     * Class wrapping a single task, with its repetition pattern, or due date,
     * and its associated lambda object to be evaluated when task is to be evaluated.
     */
    internal class ScheduledTask : IComparable
    {
        /*
         * Creates a new task for the scheduler.
         * Notice, expects a structurally acceptable Node object, accurately describing
         * the task.
         */
        public ScheduledTask(Node taskNode)
        {
            if (!taskNode.Children.Any(x => x.Name == ".lambda"))
                throw new ArgumentException($"No [.lambda] supplied to task named {taskNode.GetEx<string>()}");

            RootNode = taskNode.Clone();
            Name = taskNode.GetEx<string>() ?? throw new ArgumentException("No name given to task");
            Description = taskNode.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>() ?? "";
            CalculateDue();
        }

        /*
         * Name of task.
         */
        public string Name { get; private set; }

        /*
         * Description of task.
         */
        public string Description { get; set; }

        /*
         * Actual lambda object, which should be evaluedt as task is evaluated.
         */
        public Node Lambda => RootNode.Children.First(x => x.Name == ".lambda");

        /*
         * Contains the entire original root node used to create task, including
         * repetition pattern, due date, and [.lambda] node.
         */
        public Node RootNode { get; private set; }

        /*
         * Calculated due date for the next time the task should be evaluated.
         */
        public DateTime Due { get; private set; }

        /*
         * Returns true if this is a repetetive task, implying the task is declared
         * to be evaluated multiple times.
         */
        public bool Repeats { get; private set; }

        /*
         * Calculates task's next due date.
         */
        public void CalculateDue()
        {
            // Figuring out patter, if it's a single task evaluated once, or a repeating pattern.
            var due = RootNode.Children.Where(x => x.Name == "when" || x.Name == "repeat");
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
                        Due = CreateWeekdayDueDate(Name, dueNode, (DayOfWeek)Enum.Parse(typeof(DayOfWeek), repeat));
                        break;

                    case "seconds":
                        Due = CreateEveryXDueDate(Name, dueNode, (value) => DateTime.Now.AddSeconds(value));
                        break;

                    case "minutes":
                        Due = CreateEveryXDueDate(Name, dueNode, (value) => DateTime.Now.AddMinutes(value));
                        break;

                    case "hours":
                        Due = CreateEveryXDueDate(Name, dueNode, (value) => DateTime.Now.AddHours(value));
                        break;

                    case "days":
                        Due = CreateEveryXDueDate(Name, dueNode, (value) => DateTime.Now.AddDays(value));
                        break;

                    case "last-day-of-month":
                        Due = CreateLastDayOfMonthDueDate(Name, dueNode);
                        break;

                    default:
                        Due = CreateDayOfMonthDueDate(Name, dueNode, int.Parse(repeat));
                        break;
                }
            }
        }

        #region [ -- Interface implementations -- ]

        /*
         * Necessary to make it possible to sort tasks according to their due dates,
         * such that tasks intended to be evaluated first, comes before other tasks in the
         * TaskList instance containing all tasks.
         */
        public int CompareTo(object obj)
        {
            if (obj is ScheduledTask rhs)
                return Due.CompareTo(rhs.Due);
            throw new ArgumentException($"You tried to compare a Task to an object of type {obj?.GetType().Name ?? "???"}");
        }

        #endregion

        #region [ -- Private helper methods -- ]

        /*
         * Finds the next due date for task based upon a "every weekday of type 'x' repetition" pattern.
         */
        static DateTime CreateWeekdayDueDate(string name, Node dueNode, DayOfWeek weekday)
        {
            /*
             * Finding "time of day" value to use.
             * 
             * Defaults to "00:00" if not given.
             */
            var timeEntities = dueNode.Children
                .FirstOrDefault(x => x.Name == "time")?
                .GetEx<string>()?
                .Split(':') ?? new string[] { "00:00" };

            // Sanity checking the time.
            if (timeEntities.Length != 2)
                throw new ApplicationException($"Syntax error in [time] in task named {name}");

            /*
             * Converting time to hours and minutes in integer form,
             * and figuring out which weekday we should evaluate task.
             */
            var hour = int.Parse(timeEntities[0]);
            var minutes = int.Parse(timeEntities[1]);

            /*
             * Iterating forwards in time, until we reach a time and weekday matching
             * the specified pattern.
             * 
             * Notice, this implies that no tasks created for example for the current day,
             * on an earlier hour than Now, will be evaluated before a week from now.
             */
            var when = DateTime.Now.ToUniversalTime().Date.AddHours(hour).AddMinutes(minutes);
            while (when < DateTime.Now || weekday != when.DayOfWeek)
            {
                when = when.AddDays(1);
            }
            return when;
        }

        /*
         * Finds the next due date for the task based upon an "every n 'time entity'"
         * repetition pattern, where 'time entity' might be hours, minutes, seconds or days.
         */
        static DateTime CreateEveryXDueDate(string name, Node dueNode, Func<long, DateTime> func)
        {
            /*
             * Finds out how often this task repeats, which is an integer number,
             * being the number of seconds between each evaluation.
             */
            var seconds = dueNode.Children.FirstOrDefault(x => x.Name == "value")?
                .GetEx<long>() ??
                throw new ApplicationException($"Syntax error in task named '{name}', no [value] node found beneath [{dueNode.Name}].");

            // Sanity checking value fetched above.
            if (seconds < 1)
                throw new ArgumentException($"The [value] parts of your '{name}' task repetition pattern must be a positive integer.");

            // Calculating next due date based upon findings from above.
            return func(seconds);
        }

        /*
         * Finds the next due date which implies evaluating the task on the
         * last day of the month, at some specified time of the day.
         */
        static DateTime CreateLastDayOfMonthDueDate(string name, Node dueNode)
        {
            /*
             * Figuring out at what time of the day task should be evaluated.
             */
            var timeEntities = dueNode.Children
                .FirstOrDefault(x => x.Name == "time")?
                .GetEx<string>()?
                .Split(':') ?? throw new ApplicationException($"No [time] found in task named {name}");

            /*
             * Basic sanity checking.
             */
            if (timeEntities.Length != 2)
                throw new ApplicationException($"Syntax error in [time] in task named {name}");

            /*
             * Constructing the next due date for the task, which will be the current
             * month, and on its last day, assuming the time has not passed - At which
             * point we'll have to wait to evaluate the task.
             */
            var candidate = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month),
                int.Parse(timeEntities[0]),
                int.Parse(timeEntities[1]),
                0,
                DateTimeKind.Utc);

            /*
             * Checking if time is in the past, at which point we postpone it one
             * additional month into the future.
             */
            if (candidate < DateTime.Now)
            {
                // Shifting date one month ahead, since candidate due date has passed.
                var year = DateTime.Now.Month == 12 ? DateTime.Now.Year + 1 : DateTime.Now.Year;
                var month = DateTime.Now.Month == 12 ? 1 : DateTime.Now.Month + 1;
                candidate = new DateTime(
                    year,
                    month,
                    DateTime.DaysInMonth(year, month),
                    int.Parse(timeEntities[0]),
                    int.Parse(timeEntities[1]),
                    0,
                    DateTimeKind.Utc);
            }

            // Returning due date.
            return candidate;
        }

        /*
         * Finds the next due date based upon an "every x day of month" repetition pattern.
         */
        static DateTime CreateDayOfMonthDueDate(string name, Node dueNode, int dayOfMonth)
        {
            /*
             * Retrieving day of month, and sanity checking its value.
             * 
             * Notice, we don't allow for values higher than 28, to avoid "Februrary" issues.
             * Use "last-day-of-month" repetition pattern if this is a problem.
             */
            if (dayOfMonth > 28 || dayOfMonth < 1)
                throw new ApplicationException($"Unknown 'day of month' value in task named '{name}'. Day of month value was '{dayOfMonth}' and it must be between 1 and 28");

            /*
             * Figuring out the time of the day to evaluate the task, defaulting to "00:00",
             * and sanity checking it.
             */
            var timeEntities = dueNode.Children
                .FirstOrDefault(x => x.Name == "time")?
                .GetEx<string>()?
                .Split(':') ?? throw new ApplicationException($"No [time] found in task named {name}");
            if (timeEntities.Length != 2)
                throw new ApplicationException($"Syntax error in [time] in task named {name}");

            /*
             * Converting above values to integer values, and doing some
             * basic sanity checking.
             */
            var hour = int.Parse(timeEntities[0]);
            var minutes = int.Parse(timeEntities[1]);
            if (hour < 0 || hour > 23 || minutes < 0 || minutes > 59)
                throw new ArgumentException($"The [time] pattern of your scheduled '{name}' task must by between 00:00 and 23:59");

            /*
             * Figuring out when to evaluate the task next time.
             * Notice, we never allow for creating tasks in the past, hence if due date has passed,
             * we simply add one month to its due date.
             */
            var nextDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, dayOfMonth, hour, minutes, 0);
            if (nextDate < DateTime.Now)
                return nextDate.AddMonths(1);
            else
                return nextDate;
        }

        #endregion
    }
}
