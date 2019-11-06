/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using Xunit;
using magic.node.extensions;

namespace magic.lambda.scheduler.tests
{
    public class SchedulerTests
    {
        [Fact]
        public void Init_01()
        {
            var lambda = Common.Evaluate(@"");
            Assert.EndsWith("/tasks.hl", utilities.Common.TasksFile);
            Assert.EndsWith("/tasks/", utilities.Common.TasksFolder);
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
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss")));
            Assert.Equal(2, lambda.Children.Skip(1).First().Children.Count());
            Assert.Equal(
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss"), 
                lambda.Children.Skip(1).First().Children.First().GetEx<DateTime>().ToString("yyyy-MM-ddTHH\\:mm\\:ss"));
            Assert.Equal(".lambda", lambda.Children.Skip(1).First().Children.Skip(1).First().Name);
            Assert.Single(lambda.Children.Skip(1).First().Children.Skip(1).First().Children);
            Assert.Equal(".foo", lambda.Children.Skip(1).First().Children.Skip(1).First().Children.First().Name);
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
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss")));
            Assert.True(lambda.Children.Skip(1).First().Children.Count() > 0);
            Assert.Contains(lambda.Children.Skip(1).First().Children, x => x.Name == "foo-bar");
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
                date.ToString("yyyy-MM-ddTHH\\:mm\\:ss")));
            // Notice, this will throw if it fails.
        }

        [Fact]
        public void DeleteTask_02_Throws()
        {
            Assert.Throws<ArgumentException>(() => Common.Evaluate("scheduler.tasks.delete:non-existing"));
        }
    }
}
