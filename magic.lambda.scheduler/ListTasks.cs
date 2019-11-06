/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using magic.node;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [scheduler.tasks.list] slot that will return the names of all tasks in the system.
    /// </summary>
    [Slot(Name = "scheduler.tasks.list")]
    public class ListTasks : ISlot
    {
        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            // Clearing input values.
            input.Clear();
            input.Value = null; // To be sure ...

            // Retrieves all tasks from common helper class.
            var tasks = Common.GetTasks().Select(x => new Node("", x.Name));

            // Returning tasks declaration to caller.
            input.AddRange(tasks);
        }
    }
}
