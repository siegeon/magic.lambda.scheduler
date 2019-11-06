/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// [scheduler.get-task] slot that will return an existing task with the specified name.
    /// </summary>
    [Slot(Name = "scheduler.tasks.get")]
    public class GetTask : ISlot
    {
        /// <summary>
        /// Slot implementation.
        /// </summary>
        /// <param name="signaler">Signaler that raised signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.Clear();
            var taskName = input.GetEx<string>();
            var task = Common.GetTask(taskName);
            if (task == null)
                throw new ArgumentException($"Task with the name of {taskName} doesn't exist.");
            input.AddRange(task.Children.ToList());
        }
    }
}
