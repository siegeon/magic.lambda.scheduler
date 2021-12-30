/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using magic.node;
using magic.signals.contracts;
using magic.lambda.scheduler.contracts;

namespace magic.lambda.scheduler.slots.tasks
{
    /// <summary>
    /// [tasks.execute] slot that will execute the task with the specified ID.
    /// </summary>
    [Slot(Name = "tasks.execute")]
    public class ExecuteTask : ISlot
    {
        readonly ITaskStorage _storage;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="storage">Storage to use for tasks.</param>
        public ExecuteTask(ITaskStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            _storage.ExecuteTask(CreateTask.GetID(input));
        }
    }
}
