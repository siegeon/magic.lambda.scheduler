/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Collections.Generic;
using magic.node;

namespace magic.lambda.scheduler.utilities
{
    public interface IScheduler : IDisposable
    {
        bool Running { get; }

        void StartScheduler();

        void StopScheduler();

        DateTime? NextTask();

        void CreateTask(Node node);

        void DeleteTask(Node node);

        IEnumerable<Node> ListTasks(long offset, long limit, string id = null);

        Node GetTask(Node node);
    }
}
