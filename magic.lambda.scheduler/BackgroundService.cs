/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading;
using sys = System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler
{
    public class BackgroundService : IHostedService, IDisposable
    {
        Timer _timer;

        public BackgroundService(IServiceProvider services, string tasksFile)
        {
            Tasks = new Tasks(
                services, 
                tasksFile ?? throw new ArgumentNullException(nameof(tasksFile)));
        }

        public static Tasks Tasks { get; private set; }

        public sys.Task StartAsync(CancellationToken cancellationToken)
        {
            CreateTimer();
            return sys.Task.CompletedTask;
        }

        public sys.Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            _timer = null;
            return sys.Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        #region [ -- Private helper methods -- ]

        void CreateTimer()
        {
            _timer?.Dispose();
            _timer = new Timer(
                ExecuteNextTask,
                null,
                (uint)Math.Min(Tasks.NextTaskDue().TotalMilliseconds, Timeout.Infinite - 2),
                Timeout.Infinite);
        }

        void ExecuteNextTask(object state)
        {
            Tasks.ExecuteNextTask();

            // Notice, we avoid executing next task until previous task is done executing, to avoid flooding CPU with jobs.
            CreateTimer();
        }

        #endregion
    }
}
