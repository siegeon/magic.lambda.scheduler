/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Linq;
using System.Globalization;
using Xunit;
using magic.node.extensions;

namespace magic.lambda.scheduler.tests
{
    public class TasksTests
    {
        [Fact]
        public void CreateSimpleTask()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.create:foo-bar
   .lambda
      log.info:Howdy World
");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("insert into tasks (id, hyperlambda) values (@id, @hyperlambda)", ConnectionFactory.CommandText);
            Assert.Equal(2, ConnectionFactory.Arguments.Count);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@id" && x.Item2 == "foo-bar"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@hyperlambda" && x.Item2 == "log.info:Howdy World\r\n"));
        }

        [Fact]
        public void CreateTaskWithDescription()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.create:foo-bar
   description:Foo bar
   .lambda
      log.info:Howdy World
");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("insert into tasks (id, hyperlambda, description) values (@id, @hyperlambda, @description)", ConnectionFactory.CommandText);
            Assert.Equal(3, ConnectionFactory.Arguments.Count);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@id" && x.Item2 == "foo-bar"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@description" && x.Item2 == "Foo bar"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@hyperlambda" && x.Item2 == "log.info:Howdy World\r\n"));
        }

        [Fact]
        public void CreateTaskWithoutLambda_Throws()
        {
            ConnectionFactory.Arguments.Clear();
            Assert.Throws<HyperlambdaException>(() => Common.Evaluate(@"
tasks.create:foo-bar
"));
        }

        [Fact]
        public void CreateTaskWithoutID_Throws()
        {
            ConnectionFactory.Arguments.Clear();
            Assert.Throws<HyperlambdaException>(() => Common.Evaluate(@"
tasks.create
   .lambda
      log.info:foo
"));
        }

        [Fact]
        public void UpdateTask()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.update:foo-bar
   .lambda
      log.info:Howdy World
");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("update tasks set description = @description, hyperlambda = @hyperlambda where id = @id", ConnectionFactory.CommandText);
            Assert.Equal(3, ConnectionFactory.Arguments.Count);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@id" && x.Item2 == "foo-bar"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@hyperlambda" && x.Item2 == "log.info:Howdy World\r\n"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@description" && x.Item2 == null));
        }

        [Fact]
        public void UpdateTaskWithDescription()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.update:foo-bar
   description:Howdy world
   .lambda
      log.info:Howdy World
");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("update tasks set description = @description, hyperlambda = @hyperlambda where id = @id", ConnectionFactory.CommandText);
            Assert.Equal(3, ConnectionFactory.Arguments.Count);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@id" && x.Item2 == "foo-bar"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@hyperlambda" && x.Item2 == "log.info:Howdy World\r\n"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@description" && x.Item2 == "Howdy world"));
        }

        [Fact]
        public void DeleteTask()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.delete:foo-bar2");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("delete from tasks where id = @id", ConnectionFactory.CommandText);
            Assert.Single(ConnectionFactory.Arguments);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@id" && x.Item2 == "foo-bar2"));
        }

        [Fact]
        public void DeleteTaskNoId_Throws()
        {
            ConnectionFactory.Arguments.Clear();
            Assert.Throws<HyperlambdaException>(() => Common.Evaluate(@"tasks.delete"));
        }

        [Fact]
        public void ListAllTasks()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"tasks.list");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("select id, description, hyperlambda, created from tasks limit @limit", ConnectionFactory.CommandText);
            Assert.Single(ConnectionFactory.Arguments);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@limit" && x.Item2 == "10"));
        }

        [Fact]
        public void ListTasksWithFilterAndPaging()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.list:foo
   limit:11
   offset:25");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("select id, description, hyperlambda, created from tasks where id like @filter or description like @filter offset @offset limit @limit", ConnectionFactory.CommandText);
            Assert.Equal(3, ConnectionFactory.Arguments.Count);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@offset" && x.Item2 == "25"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@limit" && x.Item2 == "11"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@filter" && x.Item2 == "foo%"));
        }

        [Fact]
        public void CountTasks()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"tasks.count");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("select count(*) from tasks", ConnectionFactory.CommandText);
            Assert.Empty(ConnectionFactory.Arguments);
        }

        [Fact]
        public void CountTasksWhere()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"tasks.count:foo");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("select count(*) from tasks where id like @filter or description like @filter", ConnectionFactory.CommandText);
            Assert.Single(ConnectionFactory.Arguments);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@filter" && x.Item2 == "foo"));
        }

        [Fact]
        public void GetTaskAndSchedules_Throws()
        {
            Assert.Throws<HyperlambdaException>(() => Common.Evaluate(@"
tasks.get:foo
   schedules:true"));
        }

        [Fact]
        public void Schedule_Due()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.schedule:foo
   due:date:""2030-12-24T17:00""");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("insert into task_due (task, due) values (@task, @due)", ConnectionFactory.CommandText);
            Assert.Equal(2, ConnectionFactory.Arguments.Count);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@task" && x.Item2 == "foo"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@due" && x.Item2 == "2030-12-24T17:00"));
        }

        [Fact]
        public void Schedule_Pattern_01()
        {
            ConnectionFactory.Arguments.Clear();
            Common.Evaluate(@"
tasks.schedule:foo
   repeats:5.seconds");
            Assert.Equal("CONNECTION-STRING-magic", ConnectionFactory.ConnectionString);
            Assert.Equal("insert into task_due (task, due, repeats) values (@task, @due, @repeats)", ConnectionFactory.CommandText);
            Assert.Equal(3, ConnectionFactory.Arguments.Count);
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@task" && x.Item2 == "foo"));
            Assert.Single(ConnectionFactory.Arguments.Where(x => x.Item1 == "@repeats" && x.Item2 == "5.seconds"));
            var due = ConnectionFactory.Arguments.FirstOrDefault(x => x.Item1 == "@due");
            var dueDate = DateTime.ParseExact(due.Item2, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            Assert.True(dueDate > DateTime.UtcNow.AddMinutes(-5));
            Assert.True(dueDate < DateTime.UtcNow.AddMinutes(5));
        }
    }
}
