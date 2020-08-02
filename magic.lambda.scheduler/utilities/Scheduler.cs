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
using System.Threading.Tasks;

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

        public ISignaler Signaler
        {
            get => _services.GetService(typeof(ISignaler)) as ISignaler;
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
                if (ResetTimer())
                    _logger?.LogInfo("Task scheduler was started");
                else
                    _logger?.LogInfo("Task scheduler was not started since there are no due tasks");
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
                _timer = null;
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
                DeleteImplementation(GetID(node));
            }
        }

        public IEnumerable<Node> List(long offset, long limit, string id = null)
        {
            lock (_locker)
            {
                // Creating lambda for deletion.
                var readLambda = new Node($"{DatabaseType}.connect", DatabaseName);
                var readNode = new Node($"{DatabaseType}.read");
                readNode.Add(new Node("table", "tasks"));
                readNode.Add(new Node("offset", offset));
                readNode.Add(new Node("limit", limit));
                if (id != null)
                {
                    // Retrieving specific task.
                    var whereNode = new Node("where");
                    var andNode = new Node("and");
                    andNode.Add(new Node("id", id));
                    whereNode.Add(andNode);
                    readNode.Add(whereNode);
                }
                readLambda.Add(readNode);

                // Evaluating lambda.
                Signaler.Signal("eval", new Node("", null, new Node[] { readLambda }));
                return readLambda.Children.First().Children.Select(x =>
                {
                    x.UnTie();
                    return x;
                });
            }
        }

        public Node Get(Node node)
        {
            lock (_locker)
            {
                return List(0, 1, node.GetEx<string>()).FirstOrDefault();
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

        string GetID(Node node)
        {
            var id = node.Children.FirstOrDefault(x => x.Name == "id")?.GetEx<string>() ??
                node.GetEx<string>() ??
                throw new ArgumentNullException("No [id] or value provided to create task");
            if (id.Any(x => "abcdefghijklmnopqrstuvwxyz0123456789.-_".IndexOf(x) == -1))
                throw new ArgumentException("[id] of task can only contain [a-z], [0-9] and '.', '-' or '_' characters");
            return id;
        }

        void CreateImplementation(Node node)
        {
            var id = GetID(node);
            DeleteImplementation(id);
            CreateTaskImplementation(node, id);

            // Making sure task has a [when], and/or [pattern], before we create a task_due record.
            if (node.Children.Any(x => x.Name == "when" || x.Name == "pattern"))
                CreateDueDate(node, id);
        }

        void DeleteImplementation(string id)
        {
            // Creating lambda for deletion.
            var insertLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var deleteNode = new Node($"{DatabaseType}.delete");
            deleteNode.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", id));
            whereNode.Add(andNode);
            deleteNode.Add(whereNode);
            insertLambda.Add(deleteNode);

            // Evaluating lambda.
            Signaler.Signal("eval", new Node("", null, new Node[] { insertLambda }));
        }

        void CreateTaskImplementation(Node node, string id)
        {
            // Retrieving arguments and sanity checking invocation.
            var description = node.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>();
            var lambdaNode = node.Children.FirstOrDefault(x => x.Name == ".lambda")?.Clone() ?? 
                throw new ArgumentNullException("No [.lambda] supplied to create task");

            // Converting lambda to Hyperlambda.
            Signaler.Signal("lambda2hyper", lambdaNode);
            var hyperlambda = lambdaNode.Get<string>();

            // Inserting task.
            var insertLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var insertNode = new Node($"{DatabaseType}.create");
            insertNode.Add(new Node("table", "tasks"));
            var valuesNode = new Node("values");
            valuesNode.Add(new Node("id", id));
            if (!string.IsNullOrEmpty(description))
                valuesNode.Add(new Node("description", description));
            valuesNode.Add(new Node("hyperlambda", hyperlambda));
            insertNode.Add(valuesNode);
            insertLambda.Add(insertNode);
            Signaler.Signal("eval", new Node("", null, new Node[] { insertLambda }));
        }

        void CreateDueDate(Node node, string title)
        {
            // Creating [mysql.create] invocation.
            string pattern = null;
            DateTime due;
            var repNode = node.Children.FirstOrDefault(x => x.Name == "pattern");
            if (repNode != null)
            {
                // Repetition pattern.
                var rep = new RepetitionPattern(repNode.GetEx<string>());
                pattern = rep.Pattern;

                // Finding next due date from [pattern].
                due = rep.Next();
            }
            else
            {
                var whenNode = node.Children.FirstOrDefault(x => x.Name == "when");
                if (whenNode == null)
                    throw new ArgumentException("No [when] or [pattern] supplied to create task");
                due = whenNode.GetEx<DateTime>();
                if (due < DateTime.Now)
                    throw new ArgumentException("You cannot create a task with a due date that's in the past");
            }

            // Creating lambda.
            var insertLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var createNode = new Node($"{DatabaseType}.create");
            createNode.Add(new Node("table", "task_due"));
            var insertDueValues = new Node("values");
            insertDueValues.Add(new Node("task", title));
            insertDueValues.Add(new Node("due", due));
            if (pattern != null)
                insertDueValues.Add(new Node("pattern", pattern));
            createNode.Add(insertDueValues);
            insertLambda.Add(createNode);

            // Evaluating lambda.
            Signaler.Signal("eval", new Node("", null, new Node[] { insertLambda }));

            // Resetting timer.
            ResetTimer();
        }

        Tuple<DateTime, string, string, string> GetNextTask()
        {
            // Creating lambda necessary to find upcoming task's due date.
            var selectLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var readNode = new Node($"{DatabaseType}.read");
            readNode.Add(new Node("table", "task_due"));
            readNode.Add(new Node("order", "due"));
            readNode.Add(new Node("limit", 1));
            selectLambda.Add(readNode);

            // Evaluating lambda.
            Signaler.Signal("eval", new Node("", null, new Node[] { selectLambda }));

            // Verifying we have a scheduled task.
            if (!selectLambda.Children.First().Children.Any())
                return null;

            // Figuring out next date.
            return Tuple.Create(
                selectLambda.Children.First().Children.First().Children.First(x => x.Name == "due").Get<DateTime>(),
                selectLambda.Children.First().Children.First().Children.First(x => x.Name == "id").Get<string>(),
                selectLambda.Children.First().Children.First().Children.First(x => x.Name == "pattern").Get<string>(),
                selectLambda.Children.First().Children.First().Children.First(x => x.Name == "task").Get<string>());
        }

        bool ResetTimer()
        {
            // Getting upcoming task's due date.
            var date = GetNextTask();
            if (date != null)
            {
                CreateTimerImplementation(date.Item1);
                return true;
            }
            return false;
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
                async (state) =>
                {
                    if (when.AddMilliseconds(250) < DateTime.Now)
                        await ExecuteNext(); // Task is due.
                    else
                        CreateTimerImplementation(when); // Re-creating timer since date was too far into future to create Timer.
                },
                null,
                nextDue,
                Timeout.Infinite);
        }

        async Task ExecuteNext()
        {
            // Getting upcoming task's due date.
            var taskDue = GetNextTask();
            if (taskDue == null)
                return; // No more due tasks.

            if (taskDue.Item1.AddMilliseconds(250) >= DateTime.Now)
            {
                // It is not yet time to execute this task.
                // Notice, if upcoming task was deleted before timer kicks in, this might be true.
                ResetTimer();
                return;
            }

            // Retrieving task's Hyperlambda.
            var selectLambda = new Node($"{DatabaseType}.connect", DatabaseName);
            var readNode = new Node($"{DatabaseType}.read");
            readNode.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskDue.Item4));
            whereNode.Add(andNode);
            readNode.Add(whereNode);
            selectLambda.Add(readNode);
            Signaler.Signal("eval", new Node("", null, new Node[] { selectLambda }));
            var hyperlambda = selectLambda.Children.First().Children.First().Children.First(x => x.Name == "hyperlambda").Get<string>();

            // Executing task.
            var exeNode = new Node("", hyperlambda);
            Signaler.Signal("hyper2lambda", exeNode);
            exeNode.Value = null;
            await Signaler.SignalAsync("wait.eval", exeNode);

            // Checking if task repeats, and if so, we update its due date.
            if (taskDue.Item3 == null)
            {
                // Task does not repeat, hence deleting its due date.
                var deleteLambda = new Node($"{DatabaseType}.connect", DatabaseName);
                var deleteNode = new Node($"{DatabaseType}.delete");
                deleteNode.Add(new Node("table", "task_due"));
                whereNode = new Node("where");
                andNode = new Node("and");
                andNode.Add(new Node("id", taskDue.Item2));
                whereNode.Add(andNode);
                deleteNode.Add(whereNode);
                deleteLambda.Add(deleteNode);

                // Deleting task's due date record.
                Signaler.Signal("eval", new Node("", null, new Node[] { deleteLambda }));
            }
            else
            {
                // Task repeats, hence updating its due date.
                var updateLambda = new Node($"{DatabaseType}.connect", DatabaseName);
                var updateNode = new Node($"{DatabaseType}.update");
                updateNode.Add(new Node("table", "task_due"));
                whereNode = new Node("where");
                andNode = new Node("and");
                andNode.Add(new Node("id", taskDue.Item2));
                whereNode.Add(andNode);
                var valuesNode = new Node("values");
                valuesNode.Add(new Node("due", new RepetitionPattern(taskDue.Item3).Next()));
                updateNode.Add(valuesNode);
                updateNode.Add(whereNode);
                updateLambda.Add(updateNode);

                // Updating task's due date record.
                Signaler.Signal("eval", new Node("", null, new Node[] { updateLambda }));
            }

            // Reset timer.
            ResetTimer();
        }

        #endregion
    }
}
