/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Threading.Tasks;
using magic.node;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Interface for scheduler, allowing you to manage schedules for tasks.
    /// </summary>
    public interface IScheduler : IDisposable
    {
        /// <summary>
        /// Returns whether or not the scheduler is running or not.
        /// </summary>
        /// <value>Returns true if the scheduler is running.</value>
        bool Running { get; }

        /// <summary>
        /// Starts the task scheduler.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        Task StartScheduler();

        /// <summary>
        /// Stops the scheduler.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        Task StopScheduler();

        /// <summary>
        /// Returns the date of the next upcoming task, if scheduler is running.
        /// </summary>
        /// <returns>Date and time for next upcoming scheduled task's execution.</returns>
        Task<DateTime?> NextTask();

        /// <summary>
        /// Schedules an existing task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task ScheduleTask(Node node);

        /// <summary>
        /// Deletes an existing schedule for a task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task ScheduleDelete(Node node);
    }
}
