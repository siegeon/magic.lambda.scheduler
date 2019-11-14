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

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            _timer = new Timer(ExecuteNextTask, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
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

        void ExecuteNextTask(object state)
        {

        }

        #endregion
    }
}
