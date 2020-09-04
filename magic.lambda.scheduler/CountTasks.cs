/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading.Tasks;
using magic.node;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;
using magic.node.extensions;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [wait.tasks.count] slot that will return the number of tasks in your
    /// system matching the optional [count] argument.
    /// </summary>
    [Slot(Name = "wait.tasks.count")]
    public class CountTasks : ISlotAsync
    {
        readonly IScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Scheduler service to use.</param>
        public CountTasks(IScheduler scheduler)
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
            input.Value = await _scheduler.CountTasks(input.GetEx<string>());
        }
    }
}
