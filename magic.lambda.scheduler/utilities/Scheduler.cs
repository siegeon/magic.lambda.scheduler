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
    public sealed class Scheduler : IScheduler
    {
        // Internal helper class.
        private class NextTask
        {
            public DateTime Due { get; set; }

            public string TaskDueId { get; set; }

            public string Pattern { get; set; }

            public string TaskId { get; set; }
        }

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
                StartScheduler();
        }

        #region [ -- Interface implementations -- ]

        public bool Running
        {
            get => _timer != null;
        }

        public void StartScheduler()
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
                    _logger?.LogInfo("Task scheduler was successfully started");
                else
                    _logger?.LogInfo("Task scheduler was not started since there were no due tasks");
            }
        }

        public void StopScheduler()
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
                _logger?.LogInfo("Task scheduler was successfully stopped");
            }
        }

        public void CreateTask(Node node)
        {
            lock (_locker)
            {
                var id = GetID(node);
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateDeleteLambda(id)); // In case task with specified ID already exists.
                lambda.Add(CreateInsertTaskLambda(node, id));
                var hasDueDate = node.Children.Any(x => x.Name == "when" || x.Name == "pattern");
                if (hasDueDate)
                    lambda.Add(CreateInsertDueDateLambda(node, id));
                Signaler.Signal("eval", new Node("", null, new Node[] { lambda }));

                if (hasDueDate)
                    ResetTimer(); // In case tasks is next upcoming task.
            }
        }

        public void DeleteTask(Node node)
        {
            lock (_locker)
            {
                var id = GetID(node);
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateDeleteLambda(id));
                Signaler.Signal("eval", new Node("", null, new Node[] { lambda }));
                ResetTimer(); // In case task was the next upcoming task.
            }
        }

        public IEnumerable<Node> ListTasks(long offset, long limit, string id = null)
        {
            lock (_locker)
            {
                // Returning tasks to caller.
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateReadLambda(offset, limit, id));
                Signaler.Signal("eval", new Node("", null, new Node[] { lambda }));
                return lambda.Children.First().Children.Select(x =>
                {
                    x.UnTie();
                    return x;
                });
            }
        }

        public Node GetTask(Node node)
        {
            lock (_locker)
            {
                var id = node.GetEx<string>();
                if (string.IsNullOrEmpty(id))
                    throw new ArgumentNullException("No [id] specified to get task");
                return ListTasks(0, 1, id).FirstOrDefault();
            }
        }

        public void Dispose()
        {
            // This will make sure we wait for any currently executing tasks to finish before disposing.
            lock (_locker)
            {
                _timer?.Dispose();
            }
        }

        #endregion 

        #region [ -- Private helper methods and properties -- ]

        string DatabaseType
        {
            get => _configuration.GetSection("magic:databases:default").Value;
        }

        string DatabaseName
        {
            get => _configuration.GetSection("magic:scheduler:tasks-database").Value;
        }

        ISignaler Signaler
        {
            get => _services.GetService(typeof(ISignaler)) as ISignaler;
        }

        Node CreateConnectionLambda()
        {
            // Creating lambda for deletion.
            return new Node($"{DatabaseType}.connect", DatabaseName);
        }

        Node CreateDeleteLambda(string taskId)
        {
            var result = new Node($"{DatabaseType}.delete");
            result.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskId));
            whereNode.Add(andNode);
            result.Add(whereNode);
            return result;
        }

        Node CreateInsertTaskLambda(Node node, string taskId)
        {
            // Retrieving arguments and sanity checking invocation.
            var description = node.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>();
            var lambdaNode = node.Children.FirstOrDefault(x => x.Name == ".lambda")?.Clone() ?? 
                throw new ArgumentNullException("No [.lambda] supplied to create task");

            // Converting lambda to Hyperlambda.
            Signaler.Signal("lambda2hyper", lambdaNode);
            var hyperlambda = lambdaNode.Get<string>();
            if (string.IsNullOrEmpty(hyperlambda))
                throw new ArgumentException("No Hyperlambda given to create task");

            // Creating and returning result.
            var result = new Node($"{DatabaseType}.create");
            result.Add(new Node("table", "tasks"));
            var valuesNode = new Node("values");
            valuesNode.Add(new Node("id", taskId));
            if (!string.IsNullOrEmpty(description))
                valuesNode.Add(new Node("description", description));
            valuesNode.Add(new Node("hyperlambda", hyperlambda));
            result.Add(valuesNode);
            return result;
        }

        Node CreateInsertDueDateLambda(Node node, string taskId)
        {
            // Finding pattern and due date.
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
                due = whenNode.GetEx<DateTime>();
                if (due < DateTime.Now)
                    throw new ArgumentException("You cannot create a task with a due date that's in the past");
            }

            // Creating lambda.
            var result = new Node($"{DatabaseType}.create");
            result.Add(new Node("table", "task_due"));
            var insertDueValues = new Node("values");
            insertDueValues.Add(new Node("task", taskId));
            insertDueValues.Add(new Node("due", due));
            if (pattern != null)
                insertDueValues.Add(new Node("pattern", pattern));
            result.Add(insertDueValues);
            return result;
        }

        Node CreateUpdateDueDateLambda(string taskDueId, string pattern)
        {
            var updateNode = new Node($"{DatabaseType}.update");
            updateNode.Add(new Node("table", "task_due"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskDueId));
            whereNode.Add(andNode);
            var valuesNode = new Node("values");
            valuesNode.Add(new Node("due", new RepetitionPattern(pattern).Next()));
            updateNode.Add(valuesNode);
            updateNode.Add(whereNode);
            return updateNode;
        }

        Node CreateReadLambda(long offset, long limit, string taskId)
        {
            var result = new Node($"{DatabaseType}.read");
            result.Add(new Node("table", "tasks"));
            result.Add(new Node("offset", offset));
            result.Add(new Node("limit", limit));
            if (taskId != null)
            {
                // Retrieving specific task.
                var whereNode = new Node("where");
                var andNode = new Node("and");
                andNode.Add(new Node("id", taskId));
                whereNode.Add(andNode);
                result.Add(whereNode);
            }
            return result;
        }

        Node CreateReadNextDueDateLambda()
        {
            var readNode = new Node($"{DatabaseType}.read");
            readNode.Add(new Node("table", "task_due"));
            readNode.Add(new Node("order", "due"));
            readNode.Add(new Node("limit", 1));
            return readNode;
        }

        string GetTaskHyperlambda(string id)
        {
            var selectLambda = CreateConnectionLambda();
            var readNode = new Node($"{DatabaseType}.read");
            readNode.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", id));
            whereNode.Add(andNode);
            readNode.Add(whereNode);
            selectLambda.Add(readNode);
            Signaler.Signal("eval", new Node("", null, new Node[] { selectLambda }));
            return selectLambda.Children.First().Children.First().Children.First(x => x.Name == "hyperlambda").Get<string>();
        }

        string GetID(Node node)
        {
            var id = node.Children.FirstOrDefault(x => x.Name == "id")?.GetEx<string>() ??
                node.GetEx<string>() ??
                throw new ArgumentNullException("No [id] or value provided to create task");
            if (id.Any(x => "abcdefghijklmnopqrstuvwxyz0123456789.-_".IndexOf(x) == -1))
                throw new ArgumentException("[id] of task can only contain [a-z], [0-9] and '.', '-' or '_' characters");
            return id;
        }

        NextTask GetNextTask()
        {
            // Retrieving next upcoming task.
            var lambda = CreateConnectionLambda();
            lambda.Add(CreateReadNextDueDateLambda());
            Signaler.Signal("eval", new Node("", null, new Node[] { lambda }));

            // Verifying we have a scheduled task.
            if (!lambda.Children.First().Children.Any())
                return null;

            // Figuring out next date.
            return new NextTask
            {
                Due = lambda.Children.First().Children.First().Children.First(x => x.Name == "due").Get<DateTime>(),
                TaskDueId = lambda.Children.First().Children.First().Children.First(x => x.Name == "id").Get<string>(),
                Pattern = lambda.Children.First().Children.First().Children.First(x => x.Name == "pattern").Get<string>(),
                TaskId = lambda.Children.First().Children.First().Children.First(x => x.Name == "task").Get<string>()
            };
        }

        bool ResetTimer()
        {
            // Getting upcoming task's due date.
            var date = GetNextTask();
            if (date == null)
            {
                _timer?.Dispose();
                _timer = null;
                return false;
            }
            CreateTimerImplementation(date.Due);
            return true;
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

            if (taskDue.Due.AddMilliseconds(250) >= DateTime.Now)
            {
                // It is not yet time to execute this task.
                // Notice, if upcoming task was deleted before timer kicks in, this might be true.
                ResetTimer();
                return;
            }

            // Retrieving task's Hyperlambda.
            var hyperlambda = GetTaskHyperlambda(taskDue.TaskId);

            // Converting Hyperlambda to lambda and executing task.
            var exeNode = new Node("", hyperlambda);
            Signaler.Signal("hyper2lambda", exeNode);
            exeNode.Value = null;
            await Signaler.SignalAsync("wait.eval", exeNode);

            // Checking if task repeats, and if so, we update its due date.
            if (taskDue.Pattern == null)
            {
                // Task does not repeat, hence deleting its due date.
                var deleteLambda = CreateConnectionLambda();
                deleteLambda.Add(CreateDeleteLambda(taskDue.TaskId));
                Signaler.Signal("eval", new Node("", null, new Node[] { deleteLambda }));
            }
            else
            {
                // Task repeats, hence updating its due date.
                var updateLambda = CreateConnectionLambda();
                updateLambda.Add(CreateUpdateDueDateLambda(taskDue.TaskDueId, taskDue.Pattern));
                Signaler.Signal("eval", new Node("", null, new Node[] { updateLambda }));
            }

            // Reset timer.
            ResetTimer();
        }

        #endregion
    }
}
