/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Interface for task scheduler, allowing you to schedule tasks, and/or delete existing schedules.
    /// </summary>
    public interface ITaskScheduler
    {
        /// <summary>
        /// Schedules an existing task.
        /// </summary>
        /// <param name="task">Actual task you want to schedule.</param>
        /// <param name="repetition">Repetition pattern for schedule.</param>
        void ScheduleTask(string taskId, IRepetitionPattern repetition);

        /// <summary>
        /// Schedules an existing task.
        /// </summary>
        /// <param name="task">Actual task you want to schedule.</param>
        /// <param name="due">Date and time for when task should be scheduled for execution.</param>
        void ScheduleTask(string taskId, DateTime due);

        /// <summary>
        /// Deletes an existing schedule for a task.
        /// </summary>
        /// <param name="id">Unique ID of schedule.</param>
        void DeleteSchedule(int id);

        /// <summary>
        /// Starts scheduler.
        /// </summary>
        void Start();
    }
}
