/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using magic.node;
using magic.signals.contracts;

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
            throw new NotImplementedException();
        }
    }
}
