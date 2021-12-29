/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Linq;
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
    }
}
