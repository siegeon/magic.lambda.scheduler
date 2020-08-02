/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.scheduler.utilities
{
    public sealed class Scheduler : IDisposable
    {
        readonly IServiceProvider _services;
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        Timer _timer;
        readonly object _locker = new object();

        public Scheduler(
            IServiceProvider services,
            ILogger logger,
            IConfiguration configuration,
            bool autoStart)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger;
            _configuration = configuration;
            if (autoStart)
                Start();
        }

        public bool Running
        {
            get => _timer != null;
        }

        public void Start()
        {
            lock (_locker)
            {
                _logger?.LogInfo("Attempting to start task scheduler");
                if (_timer != null)
                    return; // Already started
                _logger?.LogInfo("Task scheduler started");
                StartFirstJob();
            }
        }

        public void Stop()
        {
            lock (_locker)
            {
                _logger?.LogInfo("Attempting to stop task scheduler");
                if (_timer == null)
                    return; // Already stopped
                _timer?.Dispose();
                _logger?.LogInfo("Task scheduler stopped");
            }
        }

        public List<Node> List()
        {
            lock (_locker)
            {
                return null;
            }
        }

        public Node Get(string jobName)
        {
            lock (_locker)
            {
                return null;
            }
        }

        public void Create(Node node)
        {
            // Retrieving arguments and sanity checking invocation.
            var title = node.Children.FirstOrDefault(x => x.Name == "title")?.GetEx<string>() ?? 
                throw new ArgumentNullException("No [title] supplied to create task");
            var description = node.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>() ?? 
                throw new ArgumentNullException("No [description] supplied to create task");
            var lambdaNode = node.Children.FirstOrDefault(x => x.Name == ".lambda")?.Clone() ?? 
                throw new ArgumentNullException("No [.lambda] supplied to create task");

            // Creating our signaler, and converting lambda to Hyperlambda.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("lambda2hyper", lambdaNode);
            var hyperlambda = lambdaNode.Get<string>();

            // Figuring out which database adapter we're using.
            var databaseType = _configuration.GetSection("magic:databases:default").Value;
            var databaseName = _configuration.GetSection("magic:scheduler:tasks-database").Value;

            // Sanity checking title.
            if (title.Any(x => "abcdefghijklmnopqrstuvwxyz0123456789.-_".IndexOf(x) == -1))
                throw new ArgumentException("[title] of task can only contain [a-z], [0-9] and '.', '-' or '_' characters");

            // Synchronizing access to database and timer.
            lock (_locker)
            {
                // Deleting previously created tasks with same name.
                DeleteImplementation(title);

                // Inserting task.
                var insertLambda = new Node($"{databaseType}.connect", databaseName);
                var insertNode = new Node($"{databaseType}.create");
                insertNode.Add(new Node("table", "tasks"));
                var valuesNode = new Node("values");
                valuesNode.Add(new Node("id", title));
                valuesNode.Add(new Node("description", description));
                valuesNode.Add(new Node("hyperlambda", hyperlambda));
                insertNode.Add(valuesNode);
                insertLambda.Add(insertNode);
                signaler.Signal("eval", new Node("", null, new Node[] { insertLambda }));

                // Inserting task_due item.
                if (node.Children.Any(x => x.Name == "when" || x.Name == "pattern"))
                    InsertDueDate(node, title);
            }
        }

        public void Delete(string taskName)
        {
            lock (_locker)
            {
                DeleteImplementation(taskName);
            }
        }

        #region [ -- Interface implementations -- ]

        public void Dispose()
        {
            lock (_locker)
            {
                _timer?.Dispose();
            }
        }

        #endregion 

        #region [ -- Private helper methods -- ]

        void StartFirstJob()
        {
            // Making sure we stop any previously initialized jobs.
            _timer?.Dispose();
        }

        void DeleteImplementation(string taskName)
        {
            // Getting database type and database.
            var databaseType = _configuration.GetSection("magic:databases:default").Value;
            var databaseName = _configuration.GetSection("magic:scheduler:tasks-database").Value;

            // Creating lambda for deletion.
            var insertLambda = new Node($"{databaseType}.connect", databaseName);
            var deleteNode = new Node($"{databaseType}.delete");
            deleteNode.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskName));
            whereNode.Add(andNode);
            deleteNode.Add(whereNode);
            insertLambda.Add(deleteNode);

            // Evaluating lambda.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("eval", new Node("", null, new Node[] { insertLambda }));
        }

        void InsertDueDate(Node node, string title)
        {
            // Figuring out which database adapter we're using.
            var databaseType = _configuration.GetSection("magic:databases:default").Value;
            var databaseName = _configuration.GetSection("magic:scheduler:tasks-database").Value;

            // Creating [mysql.create] invocation.
            string repetition = null;
            DateTime due;
            var repNode = node.Children.FirstOrDefault(x => x.Name == "pattern");
            if (repNode != null)
            {
                // Repetition pattern.
                var rep = new RepetitionPattern(repNode.GetEx<string>());
                repetition = rep.Pattern;

                // Finding next due date from [pattern].
                due = rep.Next();
            }
            else
            {
                var whenNode = node.Children.FirstOrDefault(x => x.Name == "when");
                if (whenNode == null)
                    throw new ArgumentException("No [when] or [pattern] supplied to create task");
                due = whenNode.GetEx<DateTime>();
            }

            // Creating lambda.
            var insertLambda = new Node($"{databaseType}.connect", databaseName);
            var createNode = new Node($"{databaseType}.create");
            createNode.Add(new Node("table", "task_due"));
            var insertDueValues = new Node("values");
            insertDueValues.Add(new Node("task", title));
            insertDueValues.Add(new Node("due", due));
            if (repetition != null)
                insertDueValues.Add(new Node("repetition", repetition));
            createNode.Add(insertDueValues);
            insertLambda.Add(createNode);

            // Evaluating lambda.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("eval", new Node("", null, new Node[] { insertLambda }));

            // Resetting timer.
            ResetTimer();
        }

        void ResetTimer()
        {
            // Disposing old timer if it's not null.
            _timer?.Dispose();

            // Figuring out which database adapter we're using.
            var databaseType = _configuration.GetSection("magic:databases:default").Value;
            var databaseName = _configuration.GetSection("magic:scheduler:tasks-database").Value;

            // Creating lambda.
            var selectLambda = new Node($"{databaseType}.connect", databaseName);
            var readNode = new Node($"{databaseType}.read");
            readNode.Add(new Node("table", "task_due"));
            readNode.Add(new Node("order", "due"));
            readNode.Add(new Node("limit", 1));
            readNode.Add(new Node("columns", null, new Node[] { new Node("due") }));
            selectLambda.Add(readNode);

            // Evaluating lambda.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("eval", new Node("", null, new Node[] { selectLambda }));

            // Figuring out next date.
            var date = selectLambda.Children.First().Children.First().Children.First().Get<DateTime>();
            _timer = new Timer(
                (state) => ExecuteNext(),
                null,
                (int)Math.Min(
                    (date - DateTime.Now).TotalMilliseconds,
                    TimeSpan.FromMilliseconds(4294000000).TotalMilliseconds),
                Timeout.Infinite);
        }

        void ExecuteNext()
        {
            // Figuring out which database adapter we're using.
            var databaseType = _configuration.GetSection("magic:databases:default").Value;
            var databaseName = _configuration.GetSection("magic:scheduler:tasks-database").Value;

            // Creating lambda.
            var selectLambda = new Node($"{databaseType}.connect", databaseName);
            var readNode = new Node($"{databaseType}.read");
            readNode.Add(new Node("table", "task_due"));
            readNode.Add(new Node("order", "due"));
            readNode.Add(new Node("limit", 1));
            selectLambda.Add(readNode);

            // Evaluating lambda.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("eval", new Node("", null, new Node[] { selectLambda }));

            // Retrieving due date, and making sure we're due for executing task.
            var due = selectLambda.Children.First().Children.First().Children.First(x => x.Name == "due").Get<DateTime>();
            if (due > DateTime.Now)
            {
                // It is not yet time to execute this task.
                ResetTimer();
                return;
            }

            // Retrieving Hyperlambda and executing it.
            var task = selectLambda.Children.First().Children.First().Children.First(x => x.Name == "task").Get<string>();

            // Creating lambda.
            selectLambda = new Node($"{databaseType}.connect", databaseName);
            readNode = new Node($"{databaseType}.read");
            readNode.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", task));
            whereNode.Add(andNode);
            readNode.Add(whereNode);
            selectLambda.Add(readNode);

            // Retrieving task.
            signaler.Signal("eval", new Node("", null, new Node[] { selectLambda }));
            var hyperlambda = selectLambda.Children.First().Children.First().Children.First(x => x.Name == "hyperlambda").Get<string>();
            var exeNode = new Node("", hyperlambda);
            signaler.Signal("hyper2lambda", exeNode);
            exeNode.Value = null;
            signaler.Signal("eval", exeNode);

            // Checking if task repeats, and if so, we update its due date.
            var pattern = selectLambda.Children.First().Children.First().Children.First(x => x.Name == "pattern").Get<string>();
            if (pattern == null)
            {
                // Task does not repeat, hence deleting its due date.
                var id = selectLambda.Children.First().Children.First().Children.First(x => x.Name == "id").Get<long>();
                var deleteLambda = new Node($"{databaseType}.connect", databaseName);
                var deleteNode = new Node($"{databaseType}.delete");
                deleteNode.Add(new Node("table", "tasks"));
                whereNode = new Node("where");
                andNode = new Node("and");
                andNode.Add(new Node("id", task));
                whereNode.Add(andNode);
                deleteNode.Add(whereNode);
                deleteLambda.Add(deleteNode);

                // Deleting task's due date record.
                signaler.Signal("eval", new Node("", null, new Node[] { deleteLambda }));
            }
            else
            {
                // Task repeats, hence updating its due date.
            }

            // Reset timer.
            ResetTimer();
        }

        #endregion
    }
}
