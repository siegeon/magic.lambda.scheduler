/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Interface for task scheduler, allowing you to schedule tasks, and/or start and stop scheduler.
    /// </summary>
    public interface ITaskScheduler
    {
        /// <summary>
        /// Returns whether or not the scheduler is running or not.
        /// </summary>
        /// <value>Returns true if the scheduler is running.</value>
        bool IsRunning { get; }

        /// <summary>
        /// Starts the task scheduler.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        void Start();

        /// <summary>
        /// Stops the scheduler.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        void Stop();

        /// <summary>
        /// Returns the date of the next upcoming task, if scheduler is running.
        /// </summary>
        /// <returns>Date and time for next upcoming scheduled task's execution.</returns>
        DateTime? Next();

        /// <summary>
        /// Schedules an existing task.
        /// </summary>
        /// <param name="task">Actual task you want to schedule.</param>
        /// <param name="repetition">Repetition pattern for schedule.</param>
        /// <returns>Awaitable task.</returns>
        void Schedule(MagicTask task, IRepetitionPattern repetition);

        /// <summary>
        /// Deletes an existing schedule for a task.
        /// </summary>
        /// <param name="id">Unique ID of schedule.</param>
        /// <returns>Awaitable task.</returns>
        void Delete(int id);
    }
}
