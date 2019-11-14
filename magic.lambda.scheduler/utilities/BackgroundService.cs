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
        Timer _timer;
        string _tasksFile;

        public BackgroundService(string tasksFile)
        {
            _tasksFile = tasksFile ?? throw new ArgumentNullException(nameof(tasksFile));
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
            // TODO: Implement by intelligently ordering scheduled tasks according to which task is up next.
            _timer?.Dispose();
            _timer = new Timer(ExecuteNextTask, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
        }

        void ExecuteNextTask(object state)
        {
            // TODO: Implement ...

            // Disposing old timer, and creating a new kicking in when next task is due.
            CreateTimer();
        }

        #endregion
    }
}
