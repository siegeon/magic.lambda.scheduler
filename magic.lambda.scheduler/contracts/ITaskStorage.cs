/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Interface for task storage, allowing you to create, read, update and delete tasks.
    /// </summary>
    public interface ITaskStorage : IDisposable
    {
        /// <summary>
        /// Creates a new task, either a simple persisted non-due task, or a repeating or due task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task Create(Node node);

        /// <summary>
        /// Lists all tasks in system paged.
        /// </summary>
        /// <param name="query">String tasks needs to start with in their ID to be considered a match.</param>
        /// <param name="offset">Offset of where to return tasks from.</param>
        /// <param name="limit">Maximum number of tasks to return.</param>
        /// <returns>List of task declarations.</returns>
        Task<IEnumerable<Node>> List(string query, long offset, long limit);

        /// <summary>
        /// Returns the specified task, and its associated due date(s).
        /// </summary>
        /// <param name="taskId">ID of task to retrieve.</param>
        /// <returns>Node declaration of task.</returns>
        Task<Node> Get(string taskId);

        /// <summary>
        /// Counts tasks in system matching the optional query.
        /// </summary>
        /// <param name="query">String tasks needs to start with in their ID to be considered a match.</param>
        /// <returns>Number of tasks in system matching optional query.</returns>
        Task<long> Count(string query);

        /// <summary>
        /// Updates an existing task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        Task Update(Node node);

        /// <summary>
        /// Deletes the task with the specified ID
        /// </summary>
        /// <param name="node">Node containing ID of task to delete.</param>
        /// <returns>Awaitable task.</returns>
        Task Delete(Node node);

        /// <summary>
        /// Executes the task with the specified ID.
        /// </summary>
        /// <param name="taskId">ID of task to execute.</param>
        /// <returns>Awaitable task.</returns>
        Task Execute(string taskId);
    }
}
