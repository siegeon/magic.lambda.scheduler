/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.scheduler.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler.slots.scheduler
{
    /// <summary>
    /// [tasks.schedule] slot that will schedule an existing task for being executed, either
    /// according to some [repeats], or at a specific [due] date in the future.
    /// </summary>
    [Slot(Name = "tasks.schedule")]
    public class ScheduleTask : ISlot
    {
        readonly ITaskScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Which background service to use.</param>
        public ScheduleTask(ITaskScheduler scheduler)
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
            var taskId = input.GetEx<string>();
            var pattern = input.Children.FirstOrDefault(x => x.Name == "repeats")?.GetEx<string>();
            if (pattern != null)
            {
                _scheduler.ScheduleTask(taskId, PatternFactory.Create(pattern));
            }
            else
            {
                var due = input
                    .Children
                    .FirstOrDefault(x => x.Name == "due")?
                    .GetEx<DateTime>() ?? 
                    throw new HyperlambdaException("No [due] or [repeats] provided to [tasks.schedule]");

                // Sanity checking invocation.
                if (due < DateTime.UtcNow)
                    throw new HyperlambdaException($"[tasks.schedule] cannot be invoked with a date and time being in the past.");
                _scheduler.ScheduleTask(taskId, due);
            }
        }
    }
}
