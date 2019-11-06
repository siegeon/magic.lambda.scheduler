/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using Xunit;
using magic.node.extensions;
using System.Globalization;
using magic.signals.contracts;
using magic.node;
using System.Threading;

namespace magic.lambda.scheduler.tests
{
    public class SchedulerTests
    {
        [Fact]
        public void Init_01()
        {
            var lambda = Common.Evaluate(@"");
            Assert.EndsWith("/tasks.hl", utilities.Common.TasksFile);
        }

        [Fact]
        public void Schedule_01()
        {
            var date = DateTime.Now.AddMinutes(5);
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:foo-bar
   when:date:""{0}""
   .lambda
      .foo
scheduler.tasks.get:foo-bar",
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss", CultureInfo.InvariantCulture)));
            Assert.True(lambda.Children.Skip(1).First().Children.Count() > 0);
            Assert.Equal(
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss", CultureInfo.InvariantCulture), 
                lambda.Children.Skip(1).First().Children.First().Children.First().GetEx<DateTime>().ToString("yyyy-MM-ddTHH\\:mm\\:ss", CultureInfo.InvariantCulture));
            Assert.Equal(".lambda", lambda.Children.Skip(1).First().Children.First().Children.Skip(1).First().Name);
            Assert.Equal(".foo", lambda.Children.Skip(1).First().Children.First().Children.Skip(1).First().Children.First().Name);
            Assert.Empty(lambda.Children.Skip(1).First().Children.First().Children.Skip(1).First().Children.First().Children);
            Assert.Null(lambda.Children.Skip(1).First().Children.First().Children.Skip(1).First().Children.First().Value);
        }

        static ManualResetEvent _handle1 = new ManualResetEvent(false);
        static ManualResetEvent _handle2 = new ManualResetEvent(false);
        static bool _signaled1 = false;
        static bool _signaled2 = false;

        [Slot(Name = "foo.bar.timer")]
        public class FooBarHowdy : ISlot
        {
            public void Signal(ISignaler signaler, Node input)
            {
                _signaled1 = true;
                _handle1?.Set();
                if (_handle1 == null)
                {
                    _signaled2 = true;
                    _handle2.Set();
                }
                _handle1 = null;
            }
        }

        [Fact]
        public void ScheduleAndCallback_01()
        {
            _handle1 = new ManualResetEvent(false);
            _signaled1 = false;
            var date = DateTime.Now.AddSeconds(2);
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:foo-bar
   when:date:""{0}""
   .lambda
      foo.bar.timer
scheduler.tasks.get:foo-bar",
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss", CultureInfo.InvariantCulture)));
            _handle1.WaitOne(5000);
            Assert.True(_signaled1);
        }

        [Fact]
        public void ScheduleAndCallback_02()
        {
            _handle1 = new ManualResetEvent(false);
            _handle2 = new ManualResetEvent(false);
            _signaled1 = false;
            _signaled2 = false;
            var date = DateTime.Now.AddSeconds(2);
            var lambda = Common.Evaluate(@"
scheduler.tasks.create:foo-bar
   repeat
      second:int:2
   .lambda
      foo.bar.timer
scheduler.tasks.get:foo-bar");
            _handle1.WaitOne(5000);
            _handle2.WaitOne(5000);
            Assert.True(_signaled1);
            Assert.True(_signaled2);
        }

        [Fact]
        public void GetTask_02_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate("scheduler.tasks.delete:non-existing"));
        }

        [Fact]
        public void ListTasks_01()
        {
            var date = DateTime.Now.AddMinutes(5);
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:foo-bar
   when:date:""{0}""
   .lambda
      .foo
scheduler.tasks.list",
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss", CultureInfo.InvariantCulture)));
            Assert.True(lambda.Children.Skip(1).First().Children.Count() > 0);
            Assert.Contains(lambda.Children.Skip(1).First().Children, x => x.GetEx<string>() == "foo-bar");
        }

        [Fact]
        public void DeleteTask_01()
        {
            var date = DateTime.Now.AddMinutes(5);
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:foo-bar
   when:date:""{0}""
   .lambda
      .foo
scheduler.tasks.delete:foo-bar",
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss", CultureInfo.InvariantCulture)));
            // Notice, this will throw if it fails.
        }

        [Fact]
        public void DeleteTask_02_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate("scheduler.tasks.delete:non-existing"));
        }
    }
}
