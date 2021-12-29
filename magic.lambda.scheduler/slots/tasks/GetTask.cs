﻿/*
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
        readonly IScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Which background service to use.</param>
        public GetTask(IScheduler scheduler)
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
            input.AddRange(
                (await _scheduler.GetTask(input.GetEx<string>()))
                .Children
                .ToList());
        }
    }
}
