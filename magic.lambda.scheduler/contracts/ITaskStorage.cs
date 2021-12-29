/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Collections.Generic;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Interface for task storage, allowing you to create, read, update, and delete tasks.
    /// </summary>
    public interface ITaskStorage
    {
        /// <summary>
        /// Creates a new task, either a simple persisted non-due task, or a repeating or due task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        void CreateAsync(MagicTask task);

        /// <summary>
        /// Lists all tasks in system paged.
        /// </summary>
        /// <param name="filter">String tasks needs to start with in their ID to be considered a match.</param>
        /// <param name="offset">Offset of where to return tasks from.</param>
        /// <param name="limit">Maximum number of tasks to return.</param>
        /// <returns>List of task declarations.</returns>
        IEnumerable<MagicTask> List(string filter, long offset, long limit);

        /// <summary>
        /// Returns the specified task, and its associated due date(s).
        /// </summary>
        /// <param name="id">ID of task to retrieve.</param>
        /// <returns>Node declaration of task.</returns>
        MagicTask Get(string id);

        /// <summary>
        /// Counts tasks in system matching the optional query.
        /// </summary>
        /// <param name="filter">String tasks needs to start with in their ID to be considered a match.</param>
        /// <returns>Number of tasks in system matching optional query.</returns>
        long Count(string filter);

        /// <summary>
        /// Updates an existing task.
        /// </summary>
        /// <param name="node">Node declaration of task.</param>
        /// <returns>Awaitable task.</returns>
        void Update(MagicTask node);

        /// <summary>
        /// Deletes the task with the specified ID.
        /// </summary>
        /// <param name="id">Unique ID of task to delete.</param>
        /// <returns>Awaitable task.</returns>
        void Delete(string id);

        /// <summary>
        /// Executes the task with the specified ID.
        /// </summary>
        /// <param name="id">ID of task to execute.</param>
        /// <returns>Awaitable task.</returns>
        void Execute(string id);
    }
}
