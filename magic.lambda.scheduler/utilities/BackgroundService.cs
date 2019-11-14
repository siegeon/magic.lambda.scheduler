/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace magic.lambda.scheduler
{
    public class BackgroundService : IHostedService, IDisposable
    {
        readonly Tasks _tasks;
        Timer _timer;

        public BackgroundService(IServiceProvider services, string tasksFile)
        {
            _tasks = new Tasks(services, tasksFile);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CreateTimer();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            _timer = null;
            return Task.CompletedTask;
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
                _tasks.ExecuteNextTask,
                null,
                _tasks.NextTask(),
                Timeout.InfiniteTimeSpan);
        }

        #endregion
    }
}
