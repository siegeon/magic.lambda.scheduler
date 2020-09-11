/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.logging.helpers;

namespace magic.lambda.scheduler.utilities
{
    /// <inheritdoc />
    public sealed class Scheduler : IScheduler
    {
        readonly IServiceProvider _services;
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        Timer _timer;
        readonly SemaphoreSlim _locker = new SemaphoreSlim(1);

        /// <summary>
        /// Creates a new instance of the task scheduler, allowing you to create, edit, and delete scheduled
        /// and hibernated tasks in your system.
        /// </summary>
        /// <param name="services">Service provider to resolve services.</param>
        /// <param name="logger">Logger to use.</param>
        /// <param name="configuration">Configuration to use.</param>
        /// <param name="autoStart">If true, automatically starts the task scheduler.</param>
        public Scheduler(
            IServiceProvider services,
            ILogger logger,
            IConfiguration configuration,
            bool autoStart)
        {
            _services = services;
            _logger = logger;
            _configuration = configuration;
            if (autoStart)
                StartScheduler().GetAwaiter().GetResult();
        }

        #region [ -- Interface implementations -- ]

        /// <inheritdoc />
        public bool Running
        {
            get => _timer != null;
        }

        /// <inheritdoc />
        public async Task StartScheduler()
        {
            await _locker.WaitAsync();
            try
            {
                if (_timer != null)
                {
                    await _logger?.InfoAsync("Can't start task scheduler since it's already running");
                    return;
                }
                if (await ResetTimer())
                    await _logger?.InfoAsync("Task scheduler was successfully started");
                else
                    await _logger?.InfoAsync("Task scheduler was not started since there are no scheduled tasks");
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
        public async Task StopScheduler()
        {
            await _locker.WaitAsync();
            try
            {
                if (_timer == null)
                {
                    await _logger?.InfoAsync("Can't stop task scheduler since it's not running");
                    return;
                }
                _timer?.Dispose();
                _timer = null;
                await _logger?.InfoAsync("Task scheduler was successfully stopped");
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
        public async Task<DateTime?> NextTask()
        {
            return _timer == null ? null : (await GetNextTask())?.Due;
        }

        /// <inheritdoc />
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
                await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));

                if (hasDueDate &&
                    (node.Children.FirstOrDefault(x => x.Name == "auto-start")?.GetEx<bool>() ?? true))
                {
                    if (!Running)
                        await _logger?.InfoAsync("Starting task scheduler since task has a due date");
                    await ResetTimer(); // In case task is next upcoming for execution.
                }
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
        public async Task UpdateTask(Node node)
        {
            await _locker.WaitAsync();
            try
            {
                var id = GetID(node);
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateUpdateTaskLambda(node, id));
                await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
        public async Task ScheduleTask(Node node)
        {
            await _locker.WaitAsync();
            try
            {
                var id = GetID(node);
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateInsertDueDateLambda(node, id));
                await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
                if (!Running)
                    await _logger?.InfoAsync("Starting task scheduler since we now have a due date for a task");
                await ResetTimer(); // In case schedule is next upcoming execution.
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
        public async Task ScheduleDelete(Node node)
        {
            await _locker.WaitAsync();
            try
            {
                var id = node.GetEx<long>();
                var lambda = CreateConnectionLambda();
                var deleteNode = new Node($"wait.{GetDatabaseType()}.delete");
                deleteNode.Add(new Node("table", "task_due"));
                var whereNode = new Node("where");
                var andNode = new Node("and");
                andNode.Add(new Node("id", id));
                whereNode.Add(andNode);
                deleteNode.Add(whereNode);
                lambda.Add(deleteNode);
                await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
                await ResetTimer(); // In case schedule is next upcoming execution.
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
        public async Task DeleteTask(Node node)
        {
            await _locker.WaitAsync();
            try
            {
                var id = GetID(node);
                var lambda = CreateConnectionLambda();
                lambda.Add(CreateDeleteLambda(id));
                await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
                await ResetTimer(); // In case task was the next upcoming task.
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Node>> ListTasks(string query, long offset, long limit)
        {
            var lambda = CreateConnectionLambda();
            lambda.Add(CreateReadTaskLambda(query, offset, limit, null));
            await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
            return lambda.Children.First().Children.ToList();
        }

        /// <inheritdoc />
        public async Task<long> CountTasks(string query)
        {
            var lambda = CreateConnectionLambda();
            var readLambda = new Node($"wait.{GetDatabaseType()}.read");
            readLambda.Add(new Node("table", "tasks"));
            var columnsLambda = new Node("columns");
            columnsLambda.Add(new Node("count(*)"));
            readLambda.Add(columnsLambda);
            if (!string.IsNullOrEmpty(query))
            {
                var whereNode = new Node("where");
                var andNode = new Node("and");
                andNode.Add(new Node("id.like", query + "%"));
                whereNode.Add(andNode);
                readLambda.Add(whereNode);
            }
            lambda.Add(readLambda);
            await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
            return readLambda.Children.First().Children.First().Get<long>();
        }

        /// <inheritdoc />
        public async Task<Node> GetTask(string taskId)
        {
            var lambda = CreateConnectionLambda();
            lambda.Add(CreateReadTaskLambda(null, 0, 1, taskId));
            lambda.Add(CreateReadTaskDueDateLambda(taskId));
            await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));
            var result = lambda.Children.First().Children.First();
            result.Children.First(x => x.Name == "id").UnTie();
            var desc = result.Children.First(x => x.Name == "description");
            if (desc.Value == null)
                desc.UnTie();
            if (lambda.Children.Skip(1).First().Children.Any())
            {
                var schedule = new Node("schedule");
                foreach (var idx in lambda.Children.Skip(1).First().Children)
                {
                    var tmp = new Node(".");
                    tmp.Add(new Node("id", idx.Children.First(x => x.Name == "id").Value));
                    if (idx.Children.First(x => x.Name == "repeats")?.Value != null)
                        tmp.Add(new Node("repeats", idx.Children.First(x => x.Name == "repeats")?.Value));
                    tmp.Add(new Node("due", idx.Children.First(x => x.Name == "due").Value));
                    schedule.Add(tmp);
                }
                if (schedule.Children.Any())
                    result.Add(schedule);
            }
            return result;
        }

        /// <inheritdoc />
        public async Task ExecuteTask(string taskId)
        {
            await _locker.WaitAsync();
            try
            {
                var task = await GetTask(taskId);
                var hyperlambda = task.Children.First(x => x.Name == "hyperlambda").Get<string>();
                var lambda = new Node("", hyperlambda);
                GetSignaler().Signal("hyper2lambda", lambda);
                lambda.Value = null;
                await GetSignaler().SignalAsync("wait.eval", lambda);
                await _logger?.InfoAsync($"Task with id of '{taskId}' was executed successfully");
            }
            catch (Exception error)
            {
                _logger?.Error($"Task with id of '{taskId}' failed", error);
            }
            finally
            {
                _locker.Release();
            }
        }

        /// <inheritdoc />
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

        string GetDatabaseType()
        {
            return _configuration?.GetSection("magic:databases:default")?.Value ?? "magic";
        }

        string GetDatabaseName()
        {
            return _configuration?.GetSection("magic:tasks:database")?.Value ?? "mysql";
        }

        ISignaler GetSignaler()
        {
            return _services.GetService(typeof(ISignaler)) as ISignaler;
        }

        Node CreateConnectionLambda()
        {
            // Creating lambda for deletion.
            return new Node($"wait.{GetDatabaseType()}.connect", GetDatabaseName());
        }

        Node CreateDeleteLambda(string taskId)
        {
            var result = new Node($"wait.{GetDatabaseType()}.delete");
            result.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskId));
            whereNode.Add(andNode);
            result.Add(whereNode);
            return result;
        }

        Node CreateDeleteDueLambda(long taskDueId)
        {
            var result = new Node($"wait.{GetDatabaseType()}.delete");
            result.Add(new Node("table", "task_due"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskDueId));
            whereNode.Add(andNode);
            result.Add(whereNode);
            return result;
        }

        Node CreateInsertTaskLambda(Node node, string taskId)
        {
            // Retrieving arguments and sanity checking invocation.
            var description = node.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>();
            var lambdaNode = node.Children.FirstOrDefault(x => x.Name == ".lambda")?.Clone() ?? 
                throw new ArgumentException("No [.lambda] supplied to create task");

            // Converting lambda from task to Hyperlambda.
            GetSignaler().Signal("lambda2hyper", lambdaNode);
            var hyperlambda = lambdaNode.Get<string>()?.Trim();
            if (string.IsNullOrEmpty(hyperlambda?.Trim()))
                throw new ArgumentException("No Hyperlambda given to create task");

            // Creating and returning result.
            var result = new Node($"wait.{GetDatabaseType()}.create");
            result.Add(new Node("table", "tasks"));
            var valuesNode = new Node("values");
            valuesNode.Add(new Node("id", taskId));
            if (!string.IsNullOrEmpty(description))
                valuesNode.Add(new Node("description", description));
            valuesNode.Add(new Node("hyperlambda", hyperlambda));
            result.Add(valuesNode);
            return result;
        }

        Node CreateUpdateTaskLambda(Node node, string taskId)
        {
            // Retrieving arguments and sanity checking invocation.
            var description = node.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>();

            // Converting lambda from task to Hyperlambda, if there is any lambda.
            var lambdaNode = node.Children.FirstOrDefault(x => x.Name == ".lambda")?.Clone();
            string hyperlambda = null;
            if (lambdaNode != null)
            {
                GetSignaler().Signal("lambda2hyper", lambdaNode);
                hyperlambda = lambdaNode.Get<string>()?.Trim();
            }

            // Creating and returning result.
            var result = new Node($"wait.{GetDatabaseType()}.update");
            result.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskId));
            whereNode.Add(andNode);
            result.Add(whereNode);
            var valuesNode = new Node("values");
            if (description != null)
                valuesNode.Add(new Node("description", description));
            if (hyperlambda != null)
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
                var rep = PatternFactory.Create(repNode.GetEx<string>());
                repeats = rep.Value;

                // Finding next due date from [repeats].
                due = rep.Next();
            }
            else
            {
                var dueNode = node.Children.FirstOrDefault(x => x.Name == "due");
                due = dueNode.GetEx<DateTime>().ToUniversalTime();
                if (due < DateTime.UtcNow)
                    throw new ArgumentException("You cannot create a task with a [due] date that's in the past");
            }

            // Creating lambda.
            var result = new Node($"wait.{GetDatabaseType()}.create");
            result.Add(new Node("table", "task_due"));
            var insertDueValues = new Node("values");
            insertDueValues.Add(new Node("task", taskId));
            insertDueValues.Add(new Node("due", due));
            if (repeats != null)
                insertDueValues.Add(new Node("repeats", repeats));
            result.Add(insertDueValues);
            return result;
        }

        Node CreateUpdateDueDateLambda(long taskDueId, string repeats)
        {
            var updateNode = new Node($"wait.{GetDatabaseType()}.update");
            updateNode.Add(new Node("table", "task_due"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", taskDueId));
            whereNode.Add(andNode);
            var valuesNode = new Node("values");
            valuesNode.Add(new Node("due", PatternFactory.Create(repeats).Next()));
            updateNode.Add(valuesNode);
            updateNode.Add(whereNode);
            return updateNode;
        }

        Node CreateReadTaskLambda(string query, long offset, long limit, string taskId)
        {
            var result = new Node($"wait.{GetDatabaseType()}.read");
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
            else
            {
                if (!string.IsNullOrEmpty(query))
                {
                    var whereNode = new Node("where");
                    var andNode = new Node("and");
                    andNode.Add(new Node("id.like", query + "%"));
                    whereNode.Add(andNode);
                    result.Add(whereNode);
                }
                result.Add(new Node("order", "created"));
            }
            return result;
        }

        Node CreateReadTaskDueDateLambda(string taskId)
        {
            var result = new Node($"wait.{GetDatabaseType()}.read");
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
            var readNode = new Node($"wait.{GetDatabaseType()}.read");
            readNode.Add(new Node("table", "task_due"));
            readNode.Add(new Node("order", "due"));
            readNode.Add(new Node("limit", 1));
            return readNode;
        }

        async Task<string> GetTaskHyperlambda(string id)
        {
            var selectLambda = CreateConnectionLambda();
            var readNode = new Node($"wait.{GetDatabaseType()}.read");
            readNode.Add(new Node("table", "tasks"));
            var whereNode = new Node("where");
            var andNode = new Node("and");
            andNode.Add(new Node("id", id));
            whereNode.Add(andNode);
            readNode.Add(whereNode);
            selectLambda.Add(readNode);
            await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { selectLambda }));
            return selectLambda.Children.First().Children.First().Children.First(x => x.Name == "hyperlambda").Get<string>();
        }

        string GetID(Node node)
        {
            var id = node.Children.FirstOrDefault(x => x.Name == "id")?.GetEx<string>() ??
                node.GetEx<string>() ??
                throw new ArgumentException("No [id] or value provided to create task");
            if (id.Any(x => "abcdefghijklmnopqrstuvwxyz0123456789.-_".IndexOf(x) == -1))
                throw new ArgumentException("[id] of task can only contain [a-z], [0-9] and '.', '-' or '_' characters");
            return id;
        }

        async Task<(DateTime Due, long TaskDueId, string Repeats, string TaskId)?> GetNextTask()
        {
            // Retrieving next upcoming task.
            var lambda = CreateConnectionLambda();
            lambda.Add(CreateReadNextDueDateLambda());
            await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { lambda }));

            // Verifying we have a scheduled task.
            if (!lambda.Children.First().Children.Any())
                return null;

            // Figuring out next date.
            return (
                lambda.Children.First().Children.First().Children.First(x => x.Name == "due").Get<DateTime>(),
                lambda.Children.First().Children.First().Children.First(x => x.Name == "id").Get<long>(),
                lambda.Children.First().Children.First().Children.First(x => x.Name == "repeats").Get<string>(),
                lambda.Children.First().Children.First().Children.First(x => x.Name == "task").Get<string>());
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
            CreateTimerImplementation(date.Value.Due);
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
                    (due - DateTime.UtcNow).TotalMilliseconds,
                    new TimeSpan(45, 0, 0, 0).TotalMilliseconds)); // 45 days is maximum resolution of Timer class.

            // Creating timer.
            _timer = new Timer(
                async (state) =>
                {
                    if (due.AddMilliseconds(250) < DateTime.UtcNow)
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

            if (taskDue.Value.Due.AddMilliseconds(250) >= DateTime.UtcNow)
            {
                // It is not yet time to execute this task.
                // Notice, if upcoming task was deleted before timer kicks in, this might be true.
                await ResetTimer();
                return;
            }

            // Retrieving task's Hyperlambda.
            var hyperlambda = await GetTaskHyperlambda(taskDue.Value.TaskId);

            // Converting Hyperlambda to lambda and executing task.
            var exeNode = new Node("", hyperlambda);
            GetSignaler().Signal("hyper2lambda", exeNode);
            exeNode.Value = null;
            try
            {
                await GetSignaler().SignalAsync("wait.eval", exeNode);
            }
            catch (Exception error)
            {
                _logger?.Error($"Unhandled exception while executing scheduled task with id of '{taskDue.Value.TaskId}'", error);
            }

            // Checking if task repeats, and if so, we update its due date.
            if (taskDue.Value.Repeats == null)
            {
                // Task does not repeat, hence deleting its due date.
                var deleteLambda = CreateConnectionLambda();
                deleteLambda.Add(CreateDeleteDueLambda(taskDue.Value.TaskDueId));
                await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { deleteLambda }));
            }
            else
            {
                // Task repeats, hence updating its due date.
                var updateLambda = CreateConnectionLambda();
                updateLambda.Add(CreateUpdateDueDateLambda(taskDue.Value.TaskDueId, taskDue.Value.Repeats));
                await GetSignaler().SignalAsync("wait.eval", new Node("", null, new Node[] { updateLambda }));
            }

            // Reset timer.
            await ResetTimer();
        }

        #endregion
    }
}
