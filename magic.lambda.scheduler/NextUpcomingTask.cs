/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading.Tasks;
using magic.node;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [wait.scheduler.next] slot that will return the date for the next upcoming task.
    /// </summary>
    [Slot(Name = "wait.scheduler.next")]
    public class NextUpcomingTask : ISlotAsync
    {
        readonly IScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Which background service to use.</param>
        public NextUpcomingTask(IScheduler scheduler)
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
            input.Value = await _scheduler.NextTask();
        }
    }
}
