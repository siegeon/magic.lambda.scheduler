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
    /// [tasks.list] slot that will return the names of all tasks in the system.
    /// </summary>
    [Slot(Name = "tasks.list")]
    public class ListTasks : ISlotAsync
    {
        readonly ITaskStorage _storage;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="storage">Storage to use for tasks.</param>
        public ListTasks(ITaskStorage storage)
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
            var jobs = await _storage.ListTasks(
                input.GetEx<string>(),
                input.Children.FirstOrDefault(x => x.Name == "offset")?.GetEx<long>() ?? 0,
                input.Children.FirstOrDefault(x => x.Name == "limit")?.GetEx<long>() ?? 10);
            input.Clear();
            if (jobs.Any())
            {
                input.AddRange(jobs);
                foreach (var idx in input.Children.Select(x => x.Children))
                {
                    var desc = idx.First(x => x.Name == "description");
                    if (desc.Value == null)
                        desc.UnTie();
                    idx.First(x => x.Name == "hyperlambda").UnTie();
                }
            }
        }
    }
}
