/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;
using magic.node.contracts;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.logging.helpers;
using magic.lambda.scheduler.contracts;
using magic.lambda.scheduler.utilities;
using magic.lambda.scheduler.services.internals;

namespace magic.lambda.scheduler.services
{
    /// <inheritdoc />
    public sealed class Scheduler : ITaskScheduler, ITaskStorage
    {
        readonly ISignaler _signaler;
        readonly IMagicConfiguration _configuration;
        readonly IServiceCreator<ISignaler> _signalCreator;
        readonly IServiceCreator<ILogger> _loggerCreator;
        readonly IServiceCreator<IMagicConfiguration> _configCreator;
        static readonly Dictionary<int, TaskScheduleWrapper> _schedules = new Dictionary<int, TaskScheduleWrapper>();
        static readonly object _dictionarySemaphore = new object();
        static SemaphoreSlim _executeSemaphore = null;

        /// <summary>
        /// Creates a new instance of the task scheduler, allowing you to create, edit, delete, and
        /// update tasks in your system - In addition to letting you schedule tasks.
        /// </summary>
        /// <param name="signaler">Needed to signal slots.</param>
        /// <param name="configuration">Needed to retrieve default database type.</param>
        /// <param name="signalCreator">Needed to be able to create an ISignaler instance during execution of scheduled tasks.</param>
        /// <param name="loggerCreator">Needed to be able to log errors occurring as tasks are executed.</param>
        /// <param name="configCreator">Needed to be able to create an IMagicConfiguration instance during execution of scheduled tasks.</param>
        public Scheduler(
            ISignaler signaler,
            IMagicConfiguration configuration,
            IServiceCreator<ISignaler> signalCreator,
            IServiceCreator<ILogger> loggerCreator,
            IServiceCreator<IMagicConfiguration> configCreator)
        {
            _signaler = signaler;
            _configuration = configuration;
            _signalCreator = signalCreator;
            _configCreator = configCreator;
            _loggerCreator = loggerCreator;
        }

        #region [ -- Interface implementation for ITaskStorage -- ]

        /// <inheritdoc />
        public async Task CreateTaskAsync(MagicTask task)
        {
            await DatabaseHelper.ConnectAsync(_signaler, _configuration, async (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("insert into tasks (id, hyperlambda")
                    .Append(task.Description == null ? ")" : ", description)")
                    .Append(" values (@id, @hyperlambda")
                    .Append(task.Description == null ? ")" : ", @description)");

                await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sqlBuilder.ToString(),
                    async (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", task.ID);
                    DatabaseHelper.AddParameter(cmd, "@hyperlambda", task.Hyperlambda);
                    if (task.Description != null)
                        DatabaseHelper.AddParameter(cmd, "@description", task.Description);
                    await cmd.ExecuteNonQueryAsync();
                });

                foreach (var idx in task.Schedules.Where(x => x.Repeats != null))
                {
                    await ScheduleTaskAsync(
                        connection,
                        task.ID,
                        PatternFactory.Create(idx.Repeats));
                }
                foreach (var idx in task.Schedules.Where(x => x.Repeats == null))
                {
                    await ScheduleTaskAsync(
                        connection,
                        task.ID,
                        idx.Due);
                }
            });
        }

        /// <inheritdoc />
        public async Task<IList<MagicTask>> ListTasksAsync(
            string filter,
            long offset,
            long limit)
        {
            return await DatabaseHelper.ConnectAsync(
                _signaler,
                _configuration,
                async (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select id, description, hyperlambda, created from tasks")
                    .Append(string.IsNullOrEmpty(filter) ? "" : " where id like @filter or description like @filter")
                    .Append(DatabaseHelper.GetPagingSql(_configuration, offset, limit));

                return await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sqlBuilder.ToString(),
                    async (cmd) =>
                {
                    if (!string.IsNullOrEmpty(filter))
                        DatabaseHelper.AddParameter(cmd, "@filter", filter);
                    if (offset > 0)
                        DatabaseHelper.AddParameter(cmd, "@offset", offset);
                    DatabaseHelper.AddParameter(cmd, "@limit", limit);

                    return await DatabaseHelper.IterateAsync(cmd, (reader) =>
                    {
                        return new MagicTask(
                            reader[0] as string,
                            reader[1] as string,
                            reader[2] as string)
                        {
                            Created = (DateTime)reader[3],
                        };
                    });
                });
            });
        }

        /// <inheritdoc />
        public Task<MagicTask> GetTaskAsync(string id, bool schedules = false)
        {
            return GetTaskAsync(
                _signaler,
                _configuration,
                id,
                schedules);
        }

        /// <inheritdoc />
        public async Task<int> CountTasksAsync(string filter)
        {
            return await DatabaseHelper.ConnectAsync(
                _signaler,
                _configuration,
                async (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select count(*) from tasks")
                    .Append(string.IsNullOrEmpty(filter) ? "" : " where id like @filter or description like @filter");

                return await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sqlBuilder.ToString(),
                    async (cmd) =>
                {
                    if (!string.IsNullOrEmpty(filter))
                        DatabaseHelper.AddParameter(cmd, "@filter", filter);

                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                });
            });
        }

        /// <inheritdoc />
        public async Task UpdateTaskAsync(MagicTask task)
        {
            // Sanity checking invocation.
            if (task.Schedules.Any())
                throw new HyperlambdaException("You cannot update schedules for tasks when updating your task");

            await DatabaseHelper.ConnectAsync(
                _signaler,
                _configuration,
                async (connection) =>
            {
                var sql = "update tasks set description = @description, hyperlambda = @hyperlambda where id = @id";

                await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sql,
                    async (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@hyperlambda", task.Hyperlambda);
                    DatabaseHelper.AddParameter(cmd, "@description", task.Description);
                    DatabaseHelper.AddParameter(cmd, "@id", task.ID);

                    if (await cmd.ExecuteNonQueryAsync() != 1)
                        throw new HyperlambdaException($"Task with ID of '{task.ID}' was not found");
                });
            });
        }

        /// <inheritdoc />
        public async Task DeleteTaskAsync(string id)
        {
            await DatabaseHelper.ConnectAsync(
                _signaler,
                _configuration,
                async (connection) =>
            {
                var sql = "delete from tasks where id = @id";

                await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sql,
                    async (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", id);

                    // Making sure we delete all related timers.
                    lock (_dictionarySemaphore)
                    {
                        foreach (var idx in _schedules.Where(x => x.Value.TaskId == id).ToList())
                        {
                            _schedules.Remove(idx.Key);
                            idx.Value.Dispose();
                        }
                    }

                    /*
                     * To avoid funny race conditions we don't execute the delete SQL
                     * before AFTER having destroyed the timers associated with the task.
                     */
                    if (await cmd.ExecuteNonQueryAsync() != 1)
                        throw new HyperlambdaException($"Task with ID of '{id}' was not found");
                });
            });
        }

        /// <inheritdoc />
        public Task ExecuteTaskAsync(string id)
        {
            return ExecuteTaskAsync(
                _signaler,
                _configuration,
                id);
        }

        #endregion

        #region [ -- Interface implementation for ITaskScheduler -- ]

        /// <inheritdoc />
        public async Task<int> ScheduleTaskAsync(string taskId, IRepetitionPattern repetition)
        {
            return await DatabaseHelper.ConnectAsync(
                _signaler,
                _configuration,
                async (connection) =>
            {
                return await ScheduleTaskAsync(
                    connection,
                    taskId,
                    repetition);
            });
        }

        /// <inheritdoc />
        public async Task<int> ScheduleTaskAsync(string taskId, DateTime due)
        {
            return await DatabaseHelper.ConnectAsync(
                _signaler,
                _configuration,
                async (connection) =>
            {
                return await ScheduleTaskAsync(
                    connection,
                    taskId,
                    due);
            });
        }

        /// <inheritdoc />
        public Task DeleteScheduleAsync(int id)
        {
            return DeleteScheduleAsync(
                _signaler,
                _configuration,
                id);
        }

        /// <inheritdoc />
        public async Task StartAsync(int maxConcurrency)
        {
            // Sanity checking invocation.
            if (maxConcurrency < 1 || maxConcurrency > 100)
                throw new HyperlambdaException($"Maximum concurrently executed tasks for the task scheduler must fall between 1 and 100, you provided {maxConcurrency}");

            // Verifying scheduler haven't already been started.
            lock (_dictionarySemaphore)
            {
                if (_executeSemaphore != null)
                    throw new HyperlambdaException("Scheduler has already been started.");

                /*
                 * Making sure we initialize execute semaphore such that we can throttle number of
                 * maximum threads used for scheduled tasks.
                 */
                _executeSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency); //NOSONAR
            }

            // Retrieving all schedules.
            var schedules = await DatabaseHelper.ConnectAsync(
                _signaler,
                _configuration,
                async (connection) =>
            {
                var sql = "select id, task, due, repeats from task_due";

                return await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sql,
                    async (cmd) =>
                {
                    return await DatabaseHelper.IterateAsync<(int Id, string Task, DateTime Due, string Pattern)>(
                        cmd,
                        (reader) =>
                    {
                        return (
                            (int)reader[0],
                            reader[1] as string,
                            (DateTime)reader[2],
                            reader[3] as string);
                    });
                });
            });

            // Creating timers for all schedules.
            foreach (var idx in schedules)
            {
                CreateTimer(
                    _signalCreator,
                    _configCreator,
                    _loggerCreator,
                    idx.Id,
                    idx.Task,
                    idx.Due,
                    idx.Pattern == null ? null : PatternFactory.Create(idx.Pattern));
            }
        }

        #endregion

        #region [ -- Private helper methods -- ]

        /*
         * Static helper method to retrieve task from database.
         */
        static async Task<MagicTask> GetTaskAsync(
            ISignaler signaler,
            IMagicConfiguration configuration,
            string id,
            bool schedules = false)
        {
            return await DatabaseHelper.ConnectAsync(
                signaler,
                configuration,
                async (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select id, description, hyperlambda, created from tasks where id = @id")
                    .Append(DatabaseHelper.GetPagingSql(configuration, 0L, 1L));

                return await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sqlBuilder.ToString(),
                    async (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", id);
                    DatabaseHelper.AddParameter(cmd, "@limit", 1L);

                    var result = (await DatabaseHelper.IterateAsync(cmd, (reader) =>
                    {
                        return new MagicTask(
                            reader[0] as string,
                            reader[1] as string,
                            reader[2] as string)
                        {
                            Created = (DateTime)reader[3],
                        };
                    })).FirstOrDefault() ?? throw new HyperlambdaException($"Task with ID of '{id}' was not found.");

                    if (schedules)
                    {
                        foreach (var idx in await GetSchedulesAsync(connection, result.ID))
                        {
                            result.Schedules.Add(idx);
                        }
                    }
                    return result;
                });
            });
        }

        /*
         * Static helper method to execute task.
         */
        static async Task ExecuteTaskAsync(
            ISignaler signaler,
            IMagicConfiguration configuration,
            string id)
        {
            // Retrieving task.
            var task = await GetTaskAsync(signaler, configuration, id);
            if (task == null)
                throw new HyperlambdaException($"Task with ID of '{id}' was not found");

            // Transforming task's Hyperlambda to a lambda object.
            var hlNode = new Node("", task.Hyperlambda);
            await signaler.SignalAsync("hyper2lambda", hlNode);

            // Executing task.
            await signaler.SignalAsync("eval", hlNode);
        }

        /*
         * Returns schedules for task.
         */
        static async Task<IList<Schedule>> GetSchedulesAsync(DbConnection connection, string id)
        {
            var sql = "select id, due, repeats from task_due where task = @task";

            return await DatabaseHelper.CreateCommandAsync(
                connection,
                sql,
                async (cmd) =>
            {
                DatabaseHelper.AddParameter(cmd, "@task", id);

                return await DatabaseHelper.IterateAsync(cmd, (reader) =>
                {
                    return new Schedule((DateTime)reader[1], reader[2] as string)
                    {
                        Id = (int)reader[0],
                    };
                });
            });
        }

        /*
         * Static helper method to delete a schedule.
         */
        static async Task DeleteScheduleAsync(
            ISignaler signaler,
            IMagicConfiguration configuration,
            int id)
        {
            await DatabaseHelper.ConnectAsync(
                signaler,
                configuration,
                async (connection) =>
            {
                var sql = "delete from task_due where id = @id";

                await DatabaseHelper.CreateCommandAsync(
                    connection,
                    sql,
                    async (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", id);

                    lock (_dictionarySemaphore)
                    {
                        if (_schedules.ContainsKey(id))
                        {
                            var schedule = _schedules[id];
                            _schedules.Remove(id);
                            schedule.Dispose();
                        }
                    }

                    /*
                     * To avoid funny race conditions we wait until we have deleted all timers before
                     * we delete schedules from database.
                     */
                    if (await cmd.ExecuteNonQueryAsync() != 1)
                        throw new HyperlambdaException($"Schedule with ID of '{id}' was not found.");
                });
            });
        }

        /*
         * Helper method to schedule task with the specified connection.
         */
        async Task<int> ScheduleTaskAsync(
            DbConnection connection,
            string taskId,
            IRepetitionPattern repetition)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append("insert into task_due (task, due, repeats) values (@task, @due, @repeats)");
            sqlBuilder.Append(DatabaseHelper.GetInsertTail(_configuration));

            return await DatabaseHelper.CreateCommandAsync(
                connection,
                sqlBuilder.ToString(),
                async (cmd) =>
            {
                var due = repetition.Next();
                DatabaseHelper.AddParameter(cmd, "@task", taskId);
                DatabaseHelper.AddParameter(cmd, "@due", due);
                DatabaseHelper.AddParameter(cmd, "@repeats", repetition.Value);

                var scheduledId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                CreateTimer(
                    _signalCreator,
                    _configCreator,
                    _loggerCreator,
                    scheduledId,
                    taskId,
                    due,
                    repetition);
                return scheduledId;
            });
        }

        /*
         * Helper methods to schedule task on the specified connection.
         */
        async Task<int> ScheduleTaskAsync(
            DbConnection connection,
            string taskId,
            DateTime due)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append("insert into task_due (task, due) values (@task, @due)");
            sqlBuilder.Append(DatabaseHelper.GetInsertTail(_configuration));

            return await DatabaseHelper.CreateCommandAsync(
                connection,
                sqlBuilder.ToString(),
                async (cmd) =>
            {
                DatabaseHelper.AddParameter(cmd, "@task", taskId);
                DatabaseHelper.AddParameter(cmd, "@due", due);

                var scheduleId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                CreateTimer(
                    _signalCreator,
                    _configCreator,
                    _loggerCreator,
                    scheduleId,
                    taskId,
                    due);
                return scheduleId;
            });
        }

        /*
         * Creates a timer that ensures task is executed at its next due date.
         */
        static void CreateTimer(
            IServiceCreator<ISignaler> signalFactory,
            IServiceCreator<IMagicConfiguration> configFactory,
            IServiceCreator<ILogger> logFactory,
            int scheduleId,
            string taskId,
            DateTime due,
            IRepetitionPattern repetition = null)
        {
            /*
             * Notice, since the maximum future date for Timer is 45 days into the future, we
             * might have to do some "trickery" here to make sure we tick in the timer 45 days from
             * now, and postpone the execution if the schedule is for more than 45 days into the future.
             *
             * Notice, we also never execute a task before at least 250 milliseconds from now.
             */
            var whenMs = (due - DateTime.UtcNow).TotalMilliseconds;
            var maxMs = new TimeSpan(45, 0, 0, 0).TotalMilliseconds;
            var nextDue = (long)Math.Max(250L, Math.Min(whenMs, maxMs));
            var postpone = whenMs > maxMs;

            /*
             * Creating our schedule and keeping a reference to it such that we can stop
             * the schedule if asked to do so.
             */
            lock (_dictionarySemaphore)
            {
                // Creating our Timer instance.
                var timer = new Timer(async (state) =>
                {
                    /*
                     * Making sure we dispose existing timer.
                     *
                     * Notice, this little trickery of awaiting our semaphore from inside another
                     * wait on the same semaphore ensures that our timer always exists in our
                     * dictionary as we try to dispose it on the timer's thread. And yes, it looks weird,
                     * but remember that the timer's callback is executed on a different thread, so
                     * there are no deadlocks here, even though it technically might look like a deadlock.
                     */
                    lock (_dictionarySemaphore)
                    {
                        // Removing and disposing timer.
                        var schedule = _schedules[scheduleId];
                        _schedules.Remove(scheduleId);
                        schedule.Dispose();
                    }

                    // Checking if we have to postpone execution of task further into the future.
                    if (postpone)
                    {
                        // More than 45 days until schedule is due, hence just re-creating our timer.
                        CreateTimer(
                            signalFactory,
                            configFactory,
                            logFactory,
                            scheduleId,
                            taskId,
                            due,
                            repetition);
                    }
                    else
                    {
                        // Executing task.
                        await ExecuteScheduleAsync(
                            signalFactory,
                            logFactory,
                            configFactory,
                            scheduleId,
                            taskId,
                            repetition);
                    }
                }, null, nextDue, Timeout.Infinite);

                // Stuffing wrapper into dictionary.
                _schedules[scheduleId] = new TaskScheduleWrapper(
                    timer,
                    taskId,
                    scheduleId,
                    repetition);
            }
        }

        /*
         * Helper method to execute task during scheduled time.
         */
        static async Task ExecuteScheduleAsync(
            IServiceCreator<ISignaler> signalFactory,
            IServiceCreator<ILogger> logCreator,
            IServiceCreator<IMagicConfiguration> configFactory,
            int scheduleId,
            string taskId,
            IRepetitionPattern repetition)
        {
            // Creating our services.
            var signaler = signalFactory.Create();
            var config = configFactory.Create();

            /*
             * Making sure we never allow for exception to propagate out of method,
             * while also making sure we never allow too many tasks to execute simultaneously.
             */
            await _executeSemaphore.WaitAsync();
            try
            {
                await ExecuteTaskAsync(signaler, config, taskId);
            }
            catch (Exception error)
            {
                // Logging exception.
                var logger = logCreator.Create();
                await logger.ErrorAsync($"Unhandled exception while executing scheduled task with id of '{taskId}'", error);
            }
            finally
            {
                // Releasing semaphore.
                _executeSemaphore.Release();
                
                // Making sure we update task_due value if task is repeating.
                if (repetition != null)
                {
                    await DatabaseHelper.ConnectAsync(
                        signaler,
                        config,
                        async (connection) =>
                    {
                        var sql = "update task_due set due = @due where id = @id";

                        await DatabaseHelper.CreateCommandAsync(
                            connection,
                            sql,
                            async (cmd) =>
                        {
                            var nextDue = repetition.Next();
                            DatabaseHelper.AddParameter(cmd, "@due", nextDue);
                            DatabaseHelper.AddParameter(cmd, "@id", scheduleId);

                            await cmd.ExecuteNonQueryAsync();

                            // Creating a new timer for task
                            CreateTimer(
                                signalFactory,
                                configFactory,
                                logCreator,
                                scheduleId,
                                taskId,
                                nextDue,
                                repetition);
                        });
                    });
                }
                else
                {
                    // Making sure we delete schedule from database.
                    await DeleteScheduleAsync(
                        signaler,
                        config,
                        scheduleId);
                }
            }
        }

        #endregion
    }
}
