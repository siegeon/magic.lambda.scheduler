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

        public string DatabaseType
        {
            get => _configuration.GetSection("magic:databases:default").Value;
        }

        public string DatabaseName
        {
            get => _configuration.GetSection("magic:scheduler:tasks-database").Value;
        }

        public void Start()
        {
            lock (_locker)
            {
                _logger?.LogInfo("Attempting to start task scheduler");
                if (_timer != null)
                {
                    _logger?.LogInfo("Task scheduler already started");
                    return;
                }
                ResetTimer();
                _logger?.LogInfo("Task scheduler was started");
            }
        }

        public void Stop()
        {
            lock (_locker)
            {
                _logger?.LogInfo("Attempting to stop task scheduler");
                if (_timer == null)
                {
                    _logger?.LogInfo("Task scheduler already stopped");
                    return;
                }
                _timer?.Dispose();
                _logger?.LogInfo("Task scheduler was stopped");
            }
        }

        public void Create(Node node)
        {
            lock (_locker)
            {
                CreateImplementation(node);
            }
        }

        public void Delete(Node node)
        {
            lock (_locker)
            {
                DeleteImplementation(GetTitle(node));
            }
        }

        public List<Node> List()
        {
            lock (_locker)
            {
                return null;
            }
        }

        public Node Get(Node node)
        {
            lock (_locker)
            {
                return null;
            }
        }

        #region [ -- Interface implementations -- ]

        public void Dispose()
        {
            // This will make sure we wait for any currently executing tasks to finish before disposing.
            lock (_locker)
            {
                _timer?.Dispose();
            }
        }

        #endregion 

        #region [ -- Private helper methods -- ]

        string GetTitle(Node node)
        {
            var title = node.Children.FirstOrDefault(x => x.Name == "title")?.GetEx<string>() ??
                node.GetEx<string>() ??
                throw new ArgumentNullException("No [title] supplied to create task");
            if (title.Any(x => "abcdefghijklmnopqrstuvwxyz0123456789.-_".IndexOf(x) == -1))
                throw new ArgumentException("[title] of task can only contain [a-z], [0-9] and '.', '-' or '_' characters");
            return title;
        }

        void CreateImplementation(Node node)
        {
            var title = GetTitle(node);
            DeleteImplementation(title);
            CreateTaskImplementation(node, title);
            if (node.Children.Any(x => x.Name == "when" || x.Name == "pattern"))
                CreateDueDate(node, title);
        }

        void DeleteImplementation(string taskName)
        {
            // Creating lambda for deletion.
            var insertLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var deleteNode = new Node($"{DatabaseType}.delete");
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

        void CreateTaskImplementation(Node node, string title)
        {
            // Retrieving arguments and sanity checking invocation.
            var description = node.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>() ?? 
                throw new ArgumentNullException("No [description] supplied to create task");
            var lambdaNode = node.Children.FirstOrDefault(x => x.Name == ".lambda")?.Clone() ?? 
                throw new ArgumentNullException("No [.lambda] supplied to create task");

            // Creating our signaler, and converting lambda to Hyperlambda.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("lambda2hyper", lambdaNode);
            var hyperlambda = lambdaNode.Get<string>();

            // Inserting task.
            var insertLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var insertNode = new Node($"{DatabaseType}.create");
            insertNode.Add(new Node("table", "tasks"));
            var valuesNode = new Node("values");
            valuesNode.Add(new Node("id", title));
            valuesNode.Add(new Node("description", description));
            valuesNode.Add(new Node("hyperlambda", hyperlambda));
            insertNode.Add(valuesNode);
            insertLambda.Add(insertNode);
            signaler.Signal("eval", new Node("", null, new Node[] { insertLambda }));
        }

        void CreateDueDate(Node node, string title)
        {
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
            var insertLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var createNode = new Node($"{DatabaseType}.create");
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

        Tuple<DateTime, string> GetNextDueDate()
        {
            // Creating lambda necessary to find upcoming task's due date.
            var selectLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var readNode = new Node($"{DatabaseType}.read");
            readNode.Add(new Node("table", "task_due"));
            readNode.Add(new Node("order", "due"));
            readNode.Add(new Node("limit", 1));
            readNode.Add(new Node("columns", null, new Node[] { new Node("due") }));
            selectLambda.Add(readNode);

            // Evaluating lambda.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("eval", new Node("", null, new Node[] { selectLambda }));

            // Figuring out next date.
            return Tuple.Create<DateTime, string>(
                selectLambda.Children.First().Children.First().Children.First().Get<DateTime>(),
                selectLambda.Children.First().Children.First().Children.First().Get<string>());
        }

        void ResetTimer()
        {
            // Getting upcoming task's due date.
            var date = GetNextDueDate();
            CreateTimerImplementation(date.Item1);
        }

        void CreateTimerImplementation(DateTime when)
        {
            // Disposing old timer if it's not null.
            _timer?.Dispose();

            // Figuring out upcoming due date.
            var nextDue = (long)Math.Max(
                250L,
                Math.Min(
                    (when - DateTime.Now).TotalMilliseconds,
                    new TimeSpan(45, 0, 0, 0).TotalMilliseconds)); // 45 days is maximum resolution of Timer class.

            // Creating timer.
            _timer = new Timer(
                (state) =>
                {
                    if (when.AddMilliseconds(-250) > DateTime.Now)
                        ExecuteNext(); // Task is due.
                    else
                        CreateTimerImplementation(when); // Re-creating timer since date was too far into future to create Timer.
                },
                null,
                nextDue,
                Timeout.Infinite);
        }

        void ExecuteNext()
        {
            // Getting upcoming task's due date.
            var due = GetNextDueDate();
            if (due.Item1 > DateTime.Now)
            {
                // It is not yet time to execute this task.
                ResetTimer();
                return;
            }

            // Creating lambda necessary to retrieve next task.
            var selectLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var readNode = new Node($"{DatabaseType}.read");
            readNode.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", due.Item2));
            whereNode.Add(andNode);
            readNode.Add(whereNode);
            selectLambda.Add(readNode);

            // Retrieving task.
            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
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
                var deleteLambda = new Node($"{DatabaseType}.connect", DatabaseName);
                var deleteNode = new Node($"{DatabaseType}.delete");
                deleteNode.Add(new Node("table", "tasks"));
                whereNode = new Node("where");
                andNode = new Node("and");
                andNode.Add(new Node("id", due.Item2));
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
