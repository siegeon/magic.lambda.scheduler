/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Linq;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.scheduler.contracts;

namespace magic.lambda.scheduler.slots.tasks
{
    /// <summary>
    /// [tasks.get] slot that will return an existing task with the specified name,
    /// including its next due date.
    /// </summary>
    [Slot(Name = "tasks.get")]
    public class GetTask : ISlot
    {
        readonly ITaskStorage _storage;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="storage">Storage to use for tasks.</param>
        public GetTask(ITaskStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        /// <returns>Awaitable task</returns>
        public void Signal(ISignaler signaler, Node input)
        {
            // Retrieving task from storage and returning results to caller.
            var task = _storage.Get(input.GetEx<string>());
            CreateResult(signaler, task, input);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Adds the properties for the task into the specified node.
         */
        static void CreateResult(ISignaler signaler, MagicTask task, Node input)
        {
            // House cleaning.
            input.Value = null;
            input.Clear();

            // Making sure we found specified task.
            if (task == null)
                return;

            // Creating a lambda object out of the Hyperlambda for our task.
            var hlNode = new Node("", task.Hyperlambda);
            signaler.Signal("hyper2lambda", hlNode);
            input.Add(new Node(".lambda", null, hlNode.Children.ToList()));

            // Returning task properties to caller.
            input.Add(new Node("id", task.ID));
            if (!string.IsNullOrEmpty(task.Description))
                input.Add(new Node("description", task.Description));
        }

        #endregion
    }
}
