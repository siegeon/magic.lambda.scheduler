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
    /// [scheduler.tasks.create] slot that will create a new scheduled task.
    /// 
    /// You can supply [when], which creates a task that is executed once - Or you can create a [repeat]
    /// task, which is executed multiple times according to some interval. You cannot supply both [when]
    /// and [repeat].
    /// </summary>
    [Slot(Name = "scheduler.tasks.create")]
    public class CreateTask : ISlot
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
