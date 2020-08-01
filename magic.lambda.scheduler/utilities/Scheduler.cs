/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Collections.Generic;
using magic.node;

namespace magic.lambda.scheduler.utilities
{
    public sealed class Scheduler
    {
        readonly IServiceProvider _services;
        readonly ILogger _logger;
        bool _running;

        public Scheduler(
            IServiceProvider services,
            ILogger logger,
            bool autoStart)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger;
            if (autoStart)
                Start();
        }

        public bool Running
        {
            get => _running;
        }

        public void Start()
        {
            _running = true;
        }

        public void Stop()
        {
            _running = false;
        }

        public List<Node> List()
        {
            return null;
        }

        public Node Get(string jobName)
        {
            return null;
        }

        public void Create(Node node)
        {
        }

        public void Delete(string jobName)
        {
        }
    }
}
