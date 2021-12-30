/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Data;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using magic.node;
using magic.node.contracts;
using magic.node.extensions;
using magic.signals.contracts;
using magic.lambda.logging.helpers;
using magic.lambda.scheduler.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler.services
{
    /// <inheritdoc />
    public sealed class Scheduler : ITaskScheduler, ITaskStorage
    {
        /*
         * Helper POCO class kind of to encapsulate a single schedule for
         * a task in the future some time.
         */
        sealed private class TaskSchedule : IDisposable
        {
            public TaskSchedule(
                Timer timer,
                string taskId,
                int scheduleId, 
                IRepetitionPattern repetition)
            {
                Timer = timer;
                TaskId = taskId;
                ScheduleId = scheduleId;
                Repetition = repetition;
            }

            public Timer Timer { get; private set; }
            public string TaskId { get; private set; }
            public int ScheduleId { get; private set; }
            public IRepetitionPattern Repetition { get; set; }

            public void Dispose()
            {
                Timer.Dispose();
            }
        }

        readonly ISignaler _signaler;
        readonly IMagicConfiguration _configuration;
        readonly IServiceCreator<ISignaler> _signalCreator;
        readonly IServiceCreator<ILogger> _loggingCreator;
        readonly IServiceCreator<IMagicConfiguration> _configCreator;
        static readonly Dictionary<int, TaskSchedule> _schedules = new Dictionary<int, TaskSchedule>();
        static readonly object _locker = new object();

        /// <summary>
        /// Creates a new instance of the task scheduler, allowing you to create, edit, delete, and
        /// update tasks in your system - In addition to letting you schedule tasks.
        /// </summary>
        /// <param name="signaler">Needed to signal slots.</param>
        /// <param name="configuration">Needed to retrieve default database type.</param>
        /// <param name="signalCreator">Needed to be able to create an ISignaler instance during execution of scheduled tasks.</param>
        /// <param name="loggingCreator">Needed to be able to log errors occurring as tasks are executed.</param>
        /// <param name="configCreator">Needed to be able to create an IMagicConfiguration instance during execution of scheduled tasks.</param>
        public Scheduler(
            ISignaler signaler,
            IMagicConfiguration configuration,
            IServiceCreator<ISignaler> signalCreator,
            IServiceCreator<ILogger> loggingCreator,
            IServiceCreator<IMagicConfiguration> configCreator)
        {
            _signaler = signaler;
            _configuration = configuration;
            _signalCreator = signalCreator;
            _configCreator = configCreator;
            _loggingCreator = loggingCreator;
        }

        #region [ -- Interface implementation for ITaskStorage -- ]

        /// <inheritdoc />
        public void CreateTask(MagicTask task)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("insert into tasks (id, hyperlambda")
                    .Append(task.Description == null ? ")" : ", description)")
                    .Append(" values (@id, @hyperlambda")
                    .Append(task.Description == null ? ")" : ", @description)");

                DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", task.ID);
                    DatabaseHelper.AddParameter(cmd, "@hyperlambda", task.Hyperlambda);
                    if (task.Description != null)
                        DatabaseHelper.AddParameter(cmd, "@description", task.Description);
                    cmd.ExecuteNonQuery();
                });
            });
        }

        /// <inheritdoc />
        public void UpdateTask(MagicTask task)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sql = "update tasks set description = @description, hyperlambda = @hyperlambda where id = @id";

                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", task.ID);
                    DatabaseHelper.AddParameter(cmd, "@hyperlambda", task.Hyperlambda);
                    DatabaseHelper.AddParameter(cmd, "@description", task.Description);

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new HyperlambdaException($"Task with ID of '{task.ID}' was not found");
                });
            });
        }

        /// <inheritdoc />
        public void DeleteTask(string id)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sql = "delete from tasks where id = @id";

                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", id);

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new HyperlambdaException($"Task with ID of '{id}' was not found");

                    // Making sure we delete all related timers.
                    lock (_locker)
                    {
                        foreach (var idx in _schedules.Where(x => x.Value.TaskId == id).ToList())
                        {
                            _schedules.Remove(idx.Key);
                            idx.Value.Timer.Dispose();
                        }
                    }
                });
            });
        }

        /// <inheritdoc />
        public IEnumerable<MagicTask> ListTasks(string filter, long offset, long limit)
        {
            return DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select id, description, hyperlambda, created from tasks")
                    .Append(string.IsNullOrEmpty(filter) ? "" : " where id like @filter or description like @filter")
                    .Append(DatabaseHelper.GetPagingSql(_configuration, offset, limit));

                return DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    if (!string.IsNullOrEmpty(filter))
                        DatabaseHelper.AddParameter(cmd, "@filter", filter);
                    if (offset > 0)
                        DatabaseHelper.AddParameter(cmd, "@offset", offset);
                    DatabaseHelper.AddParameter(cmd, "@limit", limit);

                    return DatabaseHelper.Iterate(cmd, (reader) =>
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
        public int CountTasks(string filter)
        {
            return DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select count(*) from tasks")
                    .Append(string.IsNullOrEmpty(filter) ? "" : " where id like @filter or description like @filter");

                return DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    if (!string.IsNullOrEmpty(filter))
                        DatabaseHelper.AddParameter(cmd, "@filter", filter);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                });
            });
        }

        /// <inheritdoc />
        public MagicTask GetTask(string id, bool schedules = false)
        {
            return GetTask(_signaler, _configuration, id, schedules);
        }

        /// <inheritdoc />
        public void ExecuteTask(string id)
        {
            ExecuteTask(_signaler, _configuration, id);
        }

        #endregion

        #region [ -- Interface implementation for ITaskScheduler -- ]

        /// <inheritdoc />
        public void ScheduleTask(string taskId, IRepetitionPattern repetition)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append("insert into task_due (task, due, repeats) values (@task, @due, @repeats)");
                sqlBuilder.Append(DatabaseHelper.GetInsertTail(_configuration));

                DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    var due = repetition.Next();
                    DatabaseHelper.AddParameter(cmd, "@task", taskId);
                    DatabaseHelper.AddParameter(cmd, "@due", due);
                    DatabaseHelper.AddParameter(cmd, "@repeats", repetition.Value);

                    var scheduledId = Convert.ToInt32(cmd.ExecuteScalar());
                    CreateTimer(_signalCreator, _configCreator, _loggingCreator, scheduledId, taskId, due, repetition);
                });
            });
        }

        /// <inheritdoc />
        public void ScheduleTask(string taskId, DateTime due)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append("insert into task_due (task, due) values (@task, @due)");
                sqlBuilder.Append(DatabaseHelper.GetInsertTail(_configuration));

                DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@task", taskId);
                    DatabaseHelper.AddParameter(cmd, "@due", due);

                    var scheduleId = Convert.ToInt32(cmd.ExecuteScalar());
                    CreateTimer(_signalCreator, _configCreator, _loggingCreator, scheduleId, taskId, due, null);
                });
            });
        }

        /// <inheritdoc />
        public void DeleteSchedule(int id)
        {
            DeleteSchedule(_signaler, _configuration, id);
        }

        /// <inheritdoc />
        public void Start()
        {
            // Verifying cheduler haven't already been started.
            lock (_locker)
            {
                if (_schedules.Any())
                    throw new HyperlambdaException("Scheduler has already been started.");
            }

            // Retrieving all schedules.
            var schedules = DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sql = "select id, task, due, repeats from task_due";

                return DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    return DatabaseHelper.Iterate<(int Id, string Task, DateTime Due, string Pattern)>(cmd, (reader) =>
                    {
                        return ((int)reader[0], reader[1] as string, (DateTime)reader[2], reader[3] as string);
                    });
                });
            });

            // Creating timers for all schedules.
            foreach (var idx in schedules)
            {
                CreateTimer(
                    _signalCreator,
                    _configCreator,
                    _loggingCreator,
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
        static MagicTask GetTask(
            ISignaler signaler,
            IMagicConfiguration configuration,
            string id,
            bool schedules = false)
        {
            return DatabaseHelper.Connect(signaler, configuration, (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select id, description, hyperlambda, created from tasks where id = @id")
                    .Append(DatabaseHelper.GetPagingSql(configuration, 0L, 1L));

                return DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", id);
                    DatabaseHelper.AddParameter(cmd, "@limit", 1L);

                    var result = DatabaseHelper.Iterate(cmd, (reader) =>
                    {
                        return new MagicTask(
                            reader[0] as string,
                            reader[1] as string,
                            reader[2] as string)
                        {
                            Created = (DateTime)reader[3],
                        };
                    }).FirstOrDefault() ?? throw new HyperlambdaException($"Task with ID of '{id}' was not found.");

                    if (schedules)
                    {
                        foreach (var idx in GetSchedules(connection, result.ID))
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
        static void ExecuteTask(ISignaler signaler, IMagicConfiguration configuration, string id)
        {
            // Retrieving task.
            var task = GetTask(signaler, configuration, id);
            if (task == null)
                throw new HyperlambdaException($"Task with ID of '{id}' was not found");

            // Transforming task's Hyperlambda to a lambda object.
            var hlNode = new Node("", task.Hyperlambda);
            signaler.Signal("hyper2lambda", hlNode);

            // Executing task.
            signaler.Signal("eval", hlNode);
        }

        /*
         * Returns schedules for task.
         */
        static IEnumerable<Schedule> GetSchedules(IDbConnection connection, string id)
        {
            var sql = "select id, due, repeats from task_due where task = @task";

            return DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
            {
                DatabaseHelper.AddParameter(cmd, "@task", id);

                return DatabaseHelper.Iterate(cmd, (reader) =>
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
        static void DeleteSchedule(
            ISignaler signaler,
            IMagicConfiguration configuration,
            int id)
        {
            DatabaseHelper.Connect(signaler, configuration, (connection) =>
            {
                var sql = "delete from task_due where id = @id";

                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", id);

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new HyperlambdaException($"Task with ID of '{id}' was not found.");
                    lock (_locker)
                    {
                        if (_schedules.ContainsKey(id))
                        {
                            var schedule = _schedules[id];
                            _schedules.Remove(id);
                            schedule.Dispose();
                        }
                    }
                });
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
            IRepetitionPattern repetition)
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

            // Creating our Timer instance.
            var timer = new Timer((state) =>
            {
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
                    return;
                }

                // Executing task.
                ExecuteSchedule(
                    signalFactory,
                    logFactory,
                    configFactory,
                    scheduleId,
                    taskId,
                    repetition);

            }, null, nextDue, Timeout.Infinite);

            // Creating our schedule and keeping a reference to it such that we can stop schedule if asked to do so.
            var schedule = new TaskSchedule(timer, taskId, scheduleId, repetition);
            lock (_locker)
            {
                _schedules[scheduleId] = schedule;
            }
        }

        /*
         * Helper method to execute task during scheduled time.
         */
        static void ExecuteSchedule(
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

            // Making sure we never allow for exception to propagate out of method.
            try
            {
                ExecuteTask(signaler, config, taskId);
            }
            catch (Exception error)
            {
                var logger = logCreator.Create();
                logger.Error($"Unhandled exception while executing scheduled task with id of '{taskId}'", error);
            }
            finally
            {
                // Making sure we update task_due value if task is repeating.
                if (repetition != null)
                {
                    DatabaseHelper.Connect(signaler, config, (connection) =>
                    {
                        var sql = "update task_due set due = @due where id = @id";

                        DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                        {
                            var nextDue = repetition.Next();
                            DatabaseHelper.AddParameter(cmd, "@due", nextDue);
                            DatabaseHelper.AddParameter(cmd, "@id", scheduleId);

                            cmd.ExecuteNonQuery();

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
                    lock (_locker)
                    {
                        // Removing and disposing timer for schedule.
                        var schedule = _schedules[scheduleId];
                        _schedules.Remove(scheduleId);
                        schedule.Dispose();

                        // Making sure we delete schedule from database.
                        DeleteSchedule(
                            signaler,
                            config,
                            scheduleId);
                    }
                }
            }
        }

        #endregion
    }
}
