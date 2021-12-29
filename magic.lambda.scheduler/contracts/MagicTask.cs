/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Class encapsulating a single task.
    /// </summary>
    public class MagicTask
    {
        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="id">Unique ID of your task.</param>
        /// <param name="description">Humanly readable description of task.</param>
        /// <param name="hyperlambda">Hyperlambda to associate with task.</param>
        public MagicTask(string id, string description, string hyperlambda)
        {
            ID = id;
            Description = description;
            Hyperlambda = hyperlambda;
        }

        /// <summary>
        /// Unique identifier of task, used to reference the task.
        /// </summary>
        /// <value>The unique identifier of the task.</value>
        public string ID { get; private set; }

        /// <summary>
        /// Description of task.
        /// </summary>
        /// <value>The description of the task in humanly readable format.</value>
        public string Description { get; private set; }

        /// <summary>
        /// Hyperlambda associated with the task, which is executed once the task is executed.
        /// </summary>
        /// <value>Hyperlambda for task.</value>
        public string Hyperlambda { get; private set; }

        /// <summary>
        /// Date and time when task was created.
        /// </summary>
        /// <value>Date and time when task was created.</value>
        public DateTime? Created { get; internal set; }
    }
}
