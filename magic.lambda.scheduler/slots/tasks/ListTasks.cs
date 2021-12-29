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
    /// [tasks.list] slot that will return the names of all tasks in the system.
    /// </summary>
    [Slot(Name = "tasks.list")]
    public class ListTasks : ISlot
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
        public void Signal(ISignaler signaler, Node input)
        {
            // Retrieving tasks
            var tasks = _storage.List(
                input.GetEx<string>(),
                input.Children.FirstOrDefault(x => x.Name == "offset")?.GetEx<long>() ?? 0,
                input.Children.FirstOrDefault(x => x.Name == "limit")?.GetEx<long>() ?? 10);

            // House cleaning.
            input.Clear();
            input.Value = null;

            // Returning results to caller.
            foreach (var idx in tasks)
            {
                var cur = new Node(".");
                cur.Add(new Node("id", idx.ID));
                cur.Add(new Node("created", idx.Created));
                if (!string.IsNullOrEmpty(idx.Description))
                    cur.Add(new Node("description", idx.Description));
            }
        }
    }
}
