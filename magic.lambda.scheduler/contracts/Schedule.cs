/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Class encapsulating a single schedule for a task.
    /// </summary>
    public class Schedule
    {
        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="due">Next upcoming due date for schedule.</param>
        /// <param name="repeats">Repetition pattern for schedule.</param>
        public Schedule(DateTime due, string repeats)
        {
            Due = due;
            Repeats = repeats;
        }

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="due">Next upcoming due date for schedule.</param>
        public Schedule(DateTime due)
        {
            Due = due;
        }

        /// <summary>
        /// Unique ID of schedule.
        /// </summary>
        /// <value>Unique ID.</value>
        public int Id { get; internal set; }

        /// <summary>
        /// Next upcoming due date and time for schedule.
        /// </summary>
        /// <value>Next due date and time.</value>
        public DateTime Due { get; private set; }

        /// <summary>
        /// Repetition pattern for schedule.
        /// </summary>
        /// <value>Repetition pattern.</value>
        public string Repeats { get; private set; }
    }
}
