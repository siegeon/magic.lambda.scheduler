/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading;
using magic.node;
using magic.signals.contracts;

namespace magic.lambda.scheduler.tests
{
    [Slot(Name = "foo.task.scheduler-02")]
    public class SchedulerSlot02 : ISlot
    {
        internal static int _invoked = 0;
        internal static ManualResetEvent _handle = new ManualResetEvent(false);

        public void Signal(ISignaler signaler, Node input)
        {
            if (++_invoked == 2)
                _handle.Set();
        }
    }
}
