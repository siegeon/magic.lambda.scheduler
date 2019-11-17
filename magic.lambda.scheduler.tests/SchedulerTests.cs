/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using Xunit;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.scheduler.tests
{
    public class SchedulerTests
    {
        [Fact]
        public void CreateWhen_01()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-01
   when:""2022-12-24T23:55""
   .lambda
      .foo");
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
scheduler.tasks.create:task-01
   when:date:""{0}""
   .lambda
      foo.task.scheduler-01",
                DateTime.Now.AddSeconds(2).ToString("O")));
            Assert.False(SchedulerSlot01._invoked);
            SchedulerSlot01._handle.WaitOne(4000);
            Assert.True(SchedulerSlot01._invoked);
        }

        [Fact]
        public void CreateWhen_03_NoLambda_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate(@"
scheduler.tasks.create:task-01
   when:""2022-12-24T23:55"""));
        }

        [Fact]
        public void Create_01_NoWhenRepeat_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate(@"
scheduler.tasks.create:task-01
   .lambda
      .foo"));
        }

        [Fact]
        public void Create_02_BothWhenRepeat_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate(@"
scheduler.tasks.create:task-01
   when:""2022-12-24T23:55""
   repeat:Monday
      time:""22:00"""));
        }

        [Fact]
        public void Create_03_NoName_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate(@"
scheduler.tasks.create
   when:""2022-12-24T23:55""
   .lambda
      .foo"));
        }

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

        [Fact]
        public void CreateRepeat_01()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-01
   repeat:seconds
      value:5
   .lambda
      foo.task.scheduler-02");
            SchedulerSlot02._handle.WaitOne(12000);
            Assert.Equal(2, SchedulerSlot02._invoked);
        }

        [Fact]
        public void EnsureOrder_01()
        {
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-01
   when:date:""{0}""
   .lambda
      .foo
scheduler.tasks.create:task-02
   when:date:""{1}""
   .lambda
      .foo
scheduler.tasks.list",
                DateTime.Now.AddSeconds(1).ToString("O"),
                DateTime.Now.AddSeconds(2).ToString("O")));
            Assert.Equal(2, lambda.Children.Skip(2).First().Children.Count());
            Assert.Equal("task-01", lambda.Children.Skip(2).First().Children.First().GetEx<string>());
            Assert.Equal("task-02", lambda.Children.Skip(2).First().Children.Skip(1).First().GetEx<string>());
        }

        [Fact]
        public void EnsureOrder_02()
        {
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-02
   when:date:""{1}""
   .lambda
      .foo
scheduler.tasks.create:task-01
   when:date:""{0}""
   .lambda
      .foo
scheduler.tasks.list",
                DateTime.Now.AddSeconds(1).ToString("O"),
                DateTime.Now.AddSeconds(2).ToString("O")));
            Assert.Equal(2, lambda.Children.Skip(2).First().Children.Count());
            Assert.Equal("task-01", lambda.Children.Skip(2).First().Children.First().GetEx<string>());
            Assert.Equal("task-02", lambda.Children.Skip(2).First().Children.Skip(1).First().GetEx<string>());
        }

        [Fact]
        public void EnsureSave_01()
        {
            Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-02
   when:date:""{1}""
   .lambda
      .foo
scheduler.tasks.create:task-01
   when:date:""{0}""
   .lambda
      .foo",
                DateTime.Now.AddHours(1).ToString("O"),
                DateTime.Now.AddHours(2).ToString("O")));
            var lambda = Common.Evaluate("scheduler.tasks.list", false);
            Assert.Equal(2, lambda.Children.First().Children.Count());
            Assert.Equal("task-01", lambda.Children.First().Children.First().GetEx<string>());
            Assert.Equal("task-02", lambda.Children.First().Children.Skip(1).First().GetEx<string>());
        }
    }
}
