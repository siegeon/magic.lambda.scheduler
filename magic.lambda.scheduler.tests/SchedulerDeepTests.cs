// #define DEEP_TESTING
/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using Xunit;
using magic.node.extensions;

#if DEEP_TESTING

namespace magic.lambda.scheduler.tests
{
    public class SchedulerDeepTests
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

        [Fact]
        public void CreateWhen_02()
        {
            Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-01
   when:date:""{0}""
   .lambda
      foo.task.scheduler-01",
                DateTime.Now.AddSeconds(1).ToString("O")));
            Assert.False(SchedulerSlot01._invoked);
            SchedulerSlot01._handle.WaitOne(2000);
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
        public void CreateWhen_04()
        {
            Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-04
   when:date:""{0}""
   .lambda
      foo.task.scheduler-04
scheduler.stop",
                DateTime.Now.AddSeconds(1).ToString("O")));
            Assert.False(SchedulerSlot04._invoked);
            SchedulerSlot01._handle.WaitOne(3000);
            Assert.False(SchedulerSlot04._invoked); // Scheduler should not be running
        }

        [Fact]
        public void CreateWhen_05_IllegalName_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate(@"
scheduler.tasks.create:task-01-X
   when:""2022-12-24T23:55""
   .lambda
      .foo", true, true));
        }

        [Fact]
        public void CreateWhen_06_FolderPath()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-01
   when:""2022-12-24T23:55""
   .lambda
      .foo", true, true);
        }

        [Fact]
        public void CreateWhen_07_IllegalName_NoThrow()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-01-X
   when:""2022-12-24T23:55""
   .lambda
      .foo", true, false);
        }

        [Fact]
        public void CreateImmediate_01()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-05
   immediate
   .lambda
      foo.task.scheduler-05");
            Assert.False(SchedulerSlot05._invoked);
            SchedulerSlot05._handle.WaitOne(500);
            Assert.True(SchedulerSlot05._invoked);
        }

        [Fact]
        public void CreateImmediate_02()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-06
   immediate
   .lambda
      foo.task.scheduler-06
scheduler.stop");
            Assert.False(SchedulerSlot06._invoked);
            SchedulerSlot06._handle.WaitOne(500);
            Assert.False(SchedulerSlot06._invoked); // Scheduler should not be running
        }

        [Fact]
        public void CreateImmediate_03()
        {
            Common.Evaluate(string.Format(@"
.no:int:0
while
   lt
      get-value:x:@.no
      .:int:100
   .lambda
      strings.concat
         .:task-immediate-loop
         get-value:x:@.no
      scheduler.tasks.create:x:-
         immediate
         .lambda
            foo.task.scheduler-07
      math.increment:x:@.no"));
            SchedulerSlot07._handle.WaitOne(10000);
            Assert.Equal(100, SchedulerSlot07._invocations);
        }

        [Fact]
        public void CreateImmediate_04_NotPersisted()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-08
   immediate
   persisted:bool:false
   .lambda
      foo.task.scheduler-08");
            Assert.False(SchedulerSlot08._invoked);
            SchedulerSlot08._handle.WaitOne(500);
            Assert.True(SchedulerSlot08._invoked);
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

        [Fact]
        public void CreateRepeat_01()
        {
            Common.Evaluate(@"
scheduler.tasks.create:task-01
   repeat:seconds
      value:1
   .lambda
      foo.task.scheduler-02");
            SchedulerSlot02._handle.WaitOne(3000);
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
                DateTime.Now.AddMinutes(1).ToString("O"),
                DateTime.Now.AddMinutes(2).ToString("O")));
            Assert.Equal(2, lambda.Children.Skip(2).First().Children.Count());
            Assert.Equal("task-01", lambda.Children.Skip(2).First().Children.First().Children.First(x => x.Name == "name").GetEx<string>());
            Assert.Equal("task-02", lambda.Children.Skip(2).First().Children.Skip(1).First().Children.First(x => x.Name == "name").GetEx<string>());
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
                DateTime.Now.AddMinutes(1).ToString("O"),
                DateTime.Now.AddMinutes(2).ToString("O")));
            Assert.Equal(2, lambda.Children.Skip(2).First().Children.Count());
            Assert.Equal("task-01", lambda.Children.Skip(2).First().Children.First().Children.First(x => x.Name == "name").GetEx<string>());
            Assert.Equal("task-02", lambda.Children.Skip(2).First().Children.Skip(1).First().Children.First(x => x.Name == "name").GetEx<string>());
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

            // Notice, this will reload our job file.
            var lambda = Common.Evaluate("scheduler.tasks.list", false);
            Assert.Equal(2, lambda.Children.First().Children.Count());
            Assert.Equal("task-01", lambda.Children.First().Children.First().Children.First(x => x.Name == "name").GetEx<string>());
            Assert.Equal("task-02", lambda.Children.First().Children.Skip(1).First().Children.First(x => x.Name == "name").GetEx<string>());
        }

        [Fact]
        public void CreateDeleteBeforeExecution_01()
        {
            Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-01
   when:date:""{0}""
   .lambda
      foo.task.scheduler-03
scheduler.tasks.delete:task-01",
                DateTime.Now.AddSeconds(1).ToString("O")));
            SchedulerSlot03._handle.WaitOne(2000);
            Assert.False(SchedulerSlot03._invoked);
        }

        [Fact]
        public void CreateGet_01()
        {
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-01
   when:date:""{0}""
   .lambda
      .foo
scheduler.tasks.get:task-01",
                DateTime.Now.AddHours(1).ToString("O")));
            Assert.Single(lambda.Children.Skip(1).First().Children);
            Assert.Equal("task-01", lambda.Children.Skip(1).First().Children.First().Name);
        }

        [Fact]
        public void CreateGet_02()
        {
            var now = DateTime.Now.Date;
            var lambda = Common.Evaluate(string.Format(@"
scheduler.tasks.create:task-01
   repeat:{0}
      time:""00:00""
   .lambda
      .foo
scheduler.tasks.get:task-01",
                now.AddDays(2).DayOfWeek));
            Assert.Single(lambda.Children.Skip(1).First().Children);
            Assert.Equal(
                now.AddDays(2), 
                lambda.Children.Skip(1).First().Children.First().Children.FirstOrDefault(x => x.Name == "due").GetEx<DateTime>());
        }
    }
}

#endif