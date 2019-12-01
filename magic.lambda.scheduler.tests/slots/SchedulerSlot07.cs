/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading;
using magic.node;
using magic.signals.contracts;

namespace magic.lambda.scheduler.tests
{
    [Slot(Name = "foo.task.scheduler-07")]
    public class SchedulerSlot07 : ISlot
    {
        internal static int _invocations;
        internal static ManualResetEvent _handle = new ManualResetEvent(false);

        public void Signal(ISignaler signaler, Node input)
        {
            if (++_invocations == 100)
                _handle.Set();
        }
    }
}
