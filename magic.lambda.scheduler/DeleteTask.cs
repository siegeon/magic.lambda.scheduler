/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [scheduler.tasks.delete] slot that will delete a named task.
    /// </summary>
    [Slot(Name = "scheduler.tasks.delete")]
    public class DeleteTask : ISlot
    {
        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            // Retrieving task name and clearing input values.
            var taskName = input.GetEx<string>();
            input.Clear();
            input.Value = null;

            // Deleting task using ocmmon helper.
            Common.DeleteTask(taskName);
        }
    }
}
