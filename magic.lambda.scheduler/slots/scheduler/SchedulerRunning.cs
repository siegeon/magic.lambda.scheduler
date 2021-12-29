/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using magic.node;
using magic.signals.contracts;
using magic.lambda.scheduler.contracts;

namespace magic.lambda.scheduler.slots.scheduler
{
    /// <summary>
    /// [scheduler.running] slot that will return boolean true if scheduler
    /// is running.
    /// </summary>
    [Slot(Name = "scheduler.running")]
    public class SchedulerRunning : ISlot
    {
        readonly ITaskScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Which background service to use.</param>
        public SchedulerRunning(ITaskScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.Value = _scheduler.Running;
        }
    }
}
