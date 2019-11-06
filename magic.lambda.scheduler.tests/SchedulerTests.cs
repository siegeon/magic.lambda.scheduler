/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using Xunit;
using magic.node.extensions;

namespace magic.lambda.scheduler.tests
{
    public class SchedulerTests
    {
        [Fact]
        public void InitTests_01()
        {
            var lambda = Common.Evaluate(@"");
            Assert.EndsWith("/tasks.hl", Initialize.TasksFile);
            Assert.EndsWith("/tasks/", Initialize.TasksFolder);
        }
    }
}
