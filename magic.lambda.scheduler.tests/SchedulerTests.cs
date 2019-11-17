/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading;
using Xunit;
using magic.signals.contracts;
using magic.node;

namespace magic.lambda.scheduler.tests
{
    public class SchedulerTests
    {
        [Fact]
        public void CreateWhen_01()
        {
            Common.Evaluate(@"
scheduler.tasks.create
   when:""2022-12-24T23:55""
   .lambda
      .foo
");
        }

        [Slot(Name = "foo.task.scheduler-01")]
        public class SchedulerSlot01 : ISlot
        {
            internal static bool _invoked;
            internal static ManualResetEvent _handle = new ManualResetEvent(false);

            public void Signal(ISignaler signaler, Node input)
            {
                _invoked = true;
                _handle.Set();
            }
        }

        [Fact]
        public void CreateWhen_02()
        {
            Common.Evaluate(string.Format(@"
scheduler.tasks.create
   when:date:""{0}""
   .lambda
      foo.task.scheduler-01
", DateTime.Now.AddSeconds(2).ToString("O")));
            SchedulerSlot01._handle.WaitOne(4000);
            Assert.True(SchedulerSlot01._invoked);
        }
    }
}
