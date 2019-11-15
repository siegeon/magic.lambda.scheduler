/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using Xunit;

namespace magic.lambda.scheduler.tests
{
    public class SchedulerTests
    {
        [Fact]
        public void CreateWhen_01()
        {
            var lambda = Common.Evaluate(@"
scheduler.tasks.create
   when:""2022-12-24T23:55""
   .lambda
      .foo
");
        }
    }
}
