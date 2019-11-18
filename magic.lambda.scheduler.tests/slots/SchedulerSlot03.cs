/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading;
using magic.node;
using magic.signals.contracts;

namespace magic.lambda.scheduler.tests
{
    [Slot(Name = "foo.task.scheduler-03")]
    public class SchedulerSlot03 : ISlot
    {
        internal static bool _invoked;
        internal static ManualResetEvent _handle = new ManualResetEvent(false);

        public void Signal(ISignaler signaler, Node input)
        {
            _invoked = true;
            _handle.Set();
        }
    }
}
