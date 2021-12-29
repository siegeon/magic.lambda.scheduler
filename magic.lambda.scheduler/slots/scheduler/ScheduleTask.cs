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
        readonly ITaskStorage _storage;
        readonly ITaskScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Which background service to use.</param>
        /// <param name="storage">Needed to fetch tasks.</param>
        public ScheduleTask(ITaskScheduler scheduler, ITaskStorage storage)
        {
            _scheduler = scheduler;
            _storage = storage;
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
                var repeats = PatternFactory.Create(pattern);
                _scheduler.Schedule(taskId, repeats);
            }
            else
            {
                var due = input.Children.FirstOrDefault(x => x.Name == "due")?.GetEx<DateTime>() ?? 
                    throw new HyperlambdaException("No [due] or [repeats] provided to [tasks.schedule]");
                _scheduler.Schedule(taskId, due);
            }
        }
    }
}
