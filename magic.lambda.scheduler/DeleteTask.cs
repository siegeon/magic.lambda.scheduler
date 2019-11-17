/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using magic.node;
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
        readonly TaskScheduler _backgroundService;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="backgroundService">Which background service to use.</param>
        public DeleteTask(TaskScheduler backgroundService)
        {
            _backgroundService = backgroundService ?? throw new ArgumentNullException(nameof(backgroundService));
        }

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
