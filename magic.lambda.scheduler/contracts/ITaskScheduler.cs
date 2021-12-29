/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Threading.Tasks;
using magic.node;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Interface for task scheduler, allowing you to schedule tasks, and/or start and stop scheduler.
    /// </summary>
    public interface ITaskScheduler : IDisposable
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
        Task Start();

        /// <summary>
        /// Stops the scheduler.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        Task Stop();

        /// <summary>
        /// Returns the date of the next upcoming task, if scheduler is running.
        /// </summary>
        /// <returns>Date and time for next upcoming scheduled task's execution.</returns>
        Task<DateTime?> Next();

        /// <summary>
        /// Schedules an existing task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task Schedule(Node node);

        /// <summary>
        /// Deletes an existing schedule for a task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task Delete(Node node);
    }
}
