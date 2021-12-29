/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Linq;
using System.Threading.Tasks;
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
    public class GetTask : ISlotAsync
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
        public async Task SignalAsync(ISignaler signaler, Node input)
        {
            input.AddRange(
                (await _storage.GetTask(input.GetEx<string>()))
                .Children
                .ToList());
        }
    }
}
