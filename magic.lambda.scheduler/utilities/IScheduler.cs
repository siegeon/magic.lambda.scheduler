/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// Interface for task scheduler, allowing you to create, edit and delet tasks.
    /// Both repeating task and persisted tasks.
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
        /// Creates a new task, either a simple persisted non-due task, or a repeating or due task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task CreateTask(Node node);

        /// <summary>
        /// Updates an existing task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task UpdateTask(Node node);

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

        /// <summary>
        /// Deletes the task with the specified ID
        /// </summary>
        /// <param name="node">Node containing ID of task to delete.</param>
        /// <returns>Awaitable task.</returns>
        Task DeleteTask(Node node);

        /// <summary>
        /// Lists all tasks in system paged.
        /// </summary>
        /// <param name="query">String tasks needs to start with in their ID to be considered a match.</param>
        /// <param name="offset">Offset of where to return tasks from.</param>
        /// <param name="limit">Maximum number of tasks to return.</param>
        /// <returns>List of task declarations.</returns>
        Task<IEnumerable<Node>> ListTasks(string query, long offset, long limit);

        /// <summary>
        /// Counts tasks in system matching the optional query.
        /// </summary>
        /// <param name="query">String tasks needs to start with in their ID to be considered a match.</param>
        /// <returns>Number of tasks in system matching optional query.</returns>
        Task<long> CountTasks(string query);

        /// <summary>
        /// Returns the specified task, and its associated due date(s).
        /// </summary>
        /// <param name="taskId">ID of task to retrieve.</param>
        /// <returns>Node declaration of task.</returns>
        Task<Node> GetTask(string taskId);

        /// <summary>
        /// Executes the task with the specified ID.
        /// </summary>
        /// <param name="taskId">ID of task to execute.</param>
        /// <returns>Awaitable task.</returns>
        Task ExecuteTask(string taskId);
    }
}
