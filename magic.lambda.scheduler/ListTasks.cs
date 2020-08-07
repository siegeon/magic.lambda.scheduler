/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using magic.node;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;
using magic.node.extensions;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [wait.tasks.list] slot that will return the names of all tasks in the system.
    /// </summary>
    [Slot(Name = "wait.tasks.list")]
    public class ListTasks : ISlotAsync
    {
        readonly IScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Scheduler service to use.</param>
        public ListTasks(IScheduler scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public async Task SignalAsync(ISignaler signaler, Node input)
        {
            var jobs = await _scheduler.ListTasks(
                input.Children.FirstOrDefault(x => x.Name == "offset")?.GetEx<long>() ?? 0,
                input.Children.FirstOrDefault(x => x.Name == "limit")?.GetEx<long>() ?? 10);
            input.AddRange(jobs);
            foreach (var idx in input.Children)
            {
                var desc = idx.Children.First(x => x.Name == "description");
                if (desc.Value == null)
                    desc.UnTie();
                idx.Children.First(x => x.Name == "hyperlambda").UnTie();
            }
        }
    }
}
