﻿/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Threading.Tasks;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [task.execute] slot that will return the date for the next upcoming task.
    /// </summary>
    [Slot(Name = "tasks.execute")]
    public class ExecuteTask : ISlotAsync
    {
        readonly IScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Which background service to use.</param>
        public ExecuteTask(IScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public async Task SignalAsync(ISignaler signaler, Node input)
        {
            await _scheduler.ExecuteTask(input.GetEx<string>());
        }
    }
}
