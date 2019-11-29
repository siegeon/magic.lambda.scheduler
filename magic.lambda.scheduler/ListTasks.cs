/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [scheduler.tasks.list] slot that will return the names of all tasks in the system.
    /// </summary>
    [Slot(Name = "scheduler.tasks.list")]
    public class ListTasks : ISlot
    {
        readonly TaskScheduler _scheduler;

        /// <summary>
        /// Creates a new instance of your slot.
        /// </summary>
        /// <param name="scheduler">Which background service to use.</param>
        public ListTasks(TaskScheduler scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.AddRange( _scheduler.ListTasks().Select(x =>
            {
                var task = _scheduler.GetTask(x);
                return new Node("", null, new Node[]
                {
                    new Node("name", x),
                    new Node("due", task.Children.FirstOrDefault(y => y.Name == "due").Value),
                    new Node("description", task.Children.FirstOrDefault(y => y.Name == "description")?.Value)
                });
            }).ToList());
        }
    }
}
