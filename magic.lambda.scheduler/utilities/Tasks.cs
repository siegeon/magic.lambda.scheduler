/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;

namespace magic.lambda.scheduler
{
    public class Tasks
    {
        readonly string _tasksFile;
        readonly IServiceProvider _services;

        public Tasks(IServiceProvider services, string tasksFile)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _tasksFile = tasksFile ?? throw new ArgumentNullException(nameof(tasksFile));
        }

        public TimeSpan NextTask()
        {
            return TimeSpan.FromSeconds(5);
        }

        public void ExecuteNextTask(object state)
        {

        }
    }
}
