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
        class NextTaskHelper
        {
            public DateTime Due { get; set; }

            public string TaskDueId { get; set; }

            public string Repeats { get; set; }

            public string TaskId { get; set; }
        }

        readonly IServiceProvider _services;
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        Timer _timer;
        readonly SemaphoreSlim _locker = new SemaphoreSlim(1);

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
                StartScheduler().GetAwaiter().GetResult();
        }

        #region [ -- Interface implementations -- ]

        public bool Running
        {
            get => _timer != null;
        }

        public async Task StartScheduler()
        {
            await _locker.WaitAsync();
            try
            {
                _logger?.LogInfo("Attempting to start task scheduler");
                if (_timer != null)
                {
                    _logger?.LogInfo("Task scheduler already started");
                    return;
                }
                if (await ResetTimer())
                    _logger?.LogInfo("Task scheduler was successfully started");
                else
                    _logger?.LogInfo("Task scheduler was not started since there were no due tasks");
            }
            finally
            {
                _locker.Release();
            }
        }

        public async Task StopScheduler()
        {
            await _locker.WaitAsync();
            try
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
            finally
            {
                _locker.Release();
            }
        }

        public async Task<DateTime?> NextTask()
        {
            return _timer == null ? null : (await GetNextTask())?.Due;
        }

        public async Task CreateTask(Node node)
        {
            await _locker.WaitAsync();
            try
            {
                var id = GetID(node);
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateDeleteLambda(id)); // In case task with specified ID already exists.
                lambda.Add(CreateInsertTaskLambda(node, id));

                // Checking if task has due date. Notice, it's very much possible to persist a task without a date for execution.
                var hasDueDate = node.Children.Any(x => x.Name == "due" || x.Name == "repeats");
                if (hasDueDate)
                    lambda.Add(CreateInsertDueDateLambda(node, id));
                await Signaler.SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));

                if (hasDueDate &&
                    (node.Children.FirstOrDefault(x => x.Name == "auto-start")?.GetEx<bool>() ?? true))
                    await ResetTimer(); // In case tasks is next upcoming task.
            }
            finally
            {
                _locker.Release();
            }
        }

        public async Task DeleteTask(Node node)
        {
            await _locker.WaitAsync();
            try
            {
                var id = GetID(node);
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateDeleteLambda(id));
                await Signaler.SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
                await ResetTimer(); // In case task was the next upcoming task.
            }
            finally
            {
                _locker.Release();
            }
        }

        public async Task<IEnumerable<Node>> ListTasks(long offset, long limit)
        {
            // Returning tasks to caller.
            var lambda = CreateConnectionLambda();
            lambda.Add(CreateReadTaskLambda(offset, limit, null));
            await Signaler.SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
            return lambda.Children.First().Children.ToList();
        }

        public async Task<Node> GetTask(string taskId)
        {
            // Returning task to caller, but sanity checking invocation first.
            if (string.IsNullOrEmpty(taskId))
                throw new ArgumentNullException("No task ID supplied to get task");
            var lambda = CreateConnectionLambda();
            lambda.Add(CreateReadTaskLambda(0, 1, taskId));
            lambda.Add(CreateReadTaskDueDateLambda(taskId));
            await Signaler.SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
            var result = lambda.Children.First().Children.First();
            if (lambda.Children.Skip(1).First().Children.Any())
                result.Add(
                    new Node(
                        "due",
                        null,
                        new Node[]
                        {
                            new Node(
                                ".",
                                null,
                                lambda.Children
                                    .Skip(1)
                                    .First()
                                    .Children
                                    .SelectMany(x => x.Children)
                                    .Where(x => x.Name == "due" || 
                                        (x.Name == "repeats" && x.Value != null)))
                        }));
            return result;
        }

        public async Task ExecuteTask(string id)
        {
            await _locker.WaitAsync();
            try
            {
                var task = await GetTask(id);
                var hyperlambda = task.Children.First(x => x.Name == "hyperlambda").Get<string>();
                var lambda = new Node("", hyperlambda);
                Signaler.Signal("hyper2lambda", lambda);
                lambda.Value = null;
                await Signaler.SignalAsync("wait.eval", lambda);
            }
            finally
            {
                _locker.Release();
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

            // Converting lambda from task to Hyperlambda.
            Signaler.Signal("lambda2hyper", lambdaNode);
            var hyperlambda = lambdaNode.Get<string>();
            if (string.IsNullOrEmpty(hyperlambda?.Trim()))
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
            // Finding repeats or due date.
            // Notice, you can only have one of [repeats] or [due]
            string repeats = null;
            DateTime due;
            var repNode = node.Children.FirstOrDefault(x => x.Name == "repeats");
            if (repNode != null)
            {
                // Repetition repeats.
                var rep = new RepetitionPattern(repNode.GetEx<string>());
                repeats = rep.Pattern;

                // Finding next due date from [repeats].
                due = rep.Next();
            }
            else
            {
                var dueNode = node.Children.FirstOrDefault(x => x.Name == "due");
                due = dueNode.GetEx<DateTime>();
                if (due < DateTime.Now)
                    throw new ArgumentException("You cannot create a task with a [due] date that's in the past");
            }

            // Creating lambda.
            var result = new Node($"{DatabaseType}.create");
            result.Add(new Node("table", "task_due"));
            var insertDueValues = new Node("values");
            insertDueValues.Add(new Node("task", taskId));
            insertDueValues.Add(new Node("due", due));
            if (repeats != null)
                insertDueValues.Add(new Node("repeats", repeats));
            result.Add(insertDueValues);
            return result;
        }

        Node CreateUpdateDueDateLambda(string taskDueId, string repeats)
        {
            var updateNode = new Node($"{DatabaseType}.update");
            updateNode.Add(new Node("table", "task_due"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskDueId));
            whereNode.Add(andNode);
            var valuesNode = new Node("values");
            valuesNode.Add(new Node("due", new RepetitionPattern(repeats).Next()));
            updateNode.Add(valuesNode);
            updateNode.Add(whereNode);
            return updateNode;
        }

        Node CreateReadTaskLambda(long offset, long limit, string taskId)
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

        Node CreateReadTaskDueDateLambda(string taskId)
        {
            var result = new Node($"{DatabaseType}.read");
            result.Add(new Node("table", "task_due"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("task", taskId));
            whereNode.Add(andNode);
            result.Add(whereNode);
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

        async Task<NextTaskHelper> GetNextTask()
        {
            // Retrieving next upcoming task.
            var lambda = CreateConnectionLambda();
            lambda.Add(CreateReadNextDueDateLambda());
            await Signaler.SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));

            // Verifying we have a scheduled task.
            if (!lambda.Children.First().Children.Any())
                return null;

            // Figuring out next date.
            return new NextTaskHelper
            {
                Due = lambda.Children.First().Children.First().Children.First(x => x.Name == "due").Get<DateTime>(),
                TaskDueId = lambda.Children.First().Children.First().Children.First(x => x.Name == "id").Get<string>(),
                Repeats = lambda.Children.First().Children.First().Children.First(x => x.Name == "repeats").Get<string>(),
                TaskId = lambda.Children.First().Children.First().Children.First(x => x.Name == "task").Get<string>()
            };
        }

        async Task<bool> ResetTimer()
        {
            // Getting upcoming task's due date.
            var date = await GetNextTask();
            if (date == null)
            {
                _timer?.Dispose();
                _timer = null;
                return false;
            }
            CreateTimerImplementation(date.Due);
            return true;
        }

        void CreateTimerImplementation(DateTime due)
        {
            // Disposing old timer if it's not null.
            _timer?.Dispose();

            // Figuring out upcoming due date.
            var nextDue = (long)Math.Max(
                250L,
                Math.Min(
                    (due - DateTime.Now).TotalMilliseconds,
                    new TimeSpan(45, 0, 0, 0).TotalMilliseconds)); // 45 days is maximum resolution of Timer class.

            // Creating timer.
            _timer = new Timer(
                async (state) =>
                {
                    if (due.AddMilliseconds(250) < DateTime.Now)
                        await ExecuteNextScheduledTask(); // Task is due.
                    else
                        CreateTimerImplementation(due); // Re-creating timer since date was too far into future to create Timer.
                },
                null,
                nextDue,
                Timeout.Infinite);
        }

        async Task ExecuteNextScheduledTask()
        {
            // Getting upcoming task's due date.
            var taskDue = await GetNextTask();
            if (taskDue == null)
                return; // No more due tasks.

            if (taskDue.Due.AddMilliseconds(250) >= DateTime.Now)
            {
                // It is not yet time to execute this task.
                // Notice, if upcoming task was deleted before timer kicks in, this might be true.
                await ResetTimer();
                return;
            }

            // Retrieving task's Hyperlambda.
            var hyperlambda = GetTaskHyperlambda(taskDue.TaskId);

            // Converting Hyperlambda to lambda and executing task.
            var exeNode = new Node("", hyperlambda);
            Signaler.Signal("hyper2lambda", exeNode);
            exeNode.Value = null;
            try
            {
                await Signaler.SignalAsync("wait.eval", exeNode);
            }
            catch (Exception error)
            {
                _logger?.LogError($"Something went wrong while executing scheduled task with id of '{taskDue.TaskId}'", error);
            }

            // Checking if task repeats, and if so, we update its due date.
            if (taskDue.Repeats == null)
            {
                // Task does not repeat, hence deleting its due date.
                var deleteLambda = CreateConnectionLambda();
                deleteLambda.Add(CreateDeleteLambda(taskDue.TaskId));
                await Signaler.SignalAsync("wait.eval", new Node("", null, new Node[] { deleteLambda }));
            }
            else
            {
                // Task repeats, hence updating its due date.
                var updateLambda = CreateConnectionLambda();
                updateLambda.Add(CreateUpdateDueDateLambda(taskDue.TaskDueId, taskDue.Repeats));
                await Signaler.SignalAsync("wait.eval", new Node("", null, new Node[] { updateLambda }));
            }

            // Reset timer.
            await ResetTimer();
        }

        #endregion
    }
}
