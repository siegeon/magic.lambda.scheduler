/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;

namespace magic.lambda.scheduler.utilities
{
    public interface IScheduler : IDisposable
    {
        bool Running { get; }

        Task StartScheduler();

        Task StopScheduler();

        Task<DateTime?> NextTask();

        Task CreateTask(Node node);

        Task DeleteTask(Node node);

        Task<IEnumerable<Node>> ListTasks(long offset, long limit, string taskId = null);

        Task<Node> GetTask(string taskId);

        Task ExecuteTask(string taskId);
    }
}
