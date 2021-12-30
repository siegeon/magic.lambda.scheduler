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
using magic.lambda.scheduler.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler.services
{
    /// <inheritdoc />
    public sealed class Scheduler : ITaskScheduler, ITaskStorage
    {
        readonly ISignaler _signaler;
        readonly IMagicConfiguration _configuration;

        /// <summary>
        /// Creates a new instance of the task scheduler, allowing you to create, edit, delete, and
        /// update tasks in your system - In addition to letting you schedule tasks.
        /// </summary>
        /// <param name="signaler">Needed to signal slots.</param>
        /// <param name="configuration">Needed to retrieve default database type.</param>
        public Scheduler(ISignaler signaler, IMagicConfiguration configuration)
        {
            _signaler = signaler;
            _configuration = configuration;
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
                            Created = (DateTime)reader[3]
                        };
                    });
                });
            });
        }

        /// <inheritdoc />
        public long CountTasks(string filter)
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

                    return (long)cmd.ExecuteScalar();
                });
            });
        }

        /// <inheritdoc />
        public MagicTask GetTask(string id, bool schedules = false)
        {
            return DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select id, description, hyperlambda, created from tasks where id = @id")
                    .Append(DatabaseHelper.GetPagingSql(_configuration, 0L, 1L));

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
                    }).FirstOrDefault() ?? throw new HyperlambdaException($"Task with ID of '{id}' was not found.");;

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

        /// <inheritdoc />
        public void ExecuteTask(string id)
        {
            // Retrieving task.
            var task = GetTask(id);
            if (task == null)
                throw new HyperlambdaException($"Task with ID of '{id}' was not found");

            // Transforming task's Hyperlambda to a lambda object.
            var hlNode = new Node("", task.Hyperlambda);
            _signaler.Signal("hyper2lambda", hlNode);

            // Executing task.
            _signaler.Signal("eval", hlNode);
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

                    var scheduledId = (long)cmd.ExecuteScalar();
                    CreateSchedule(scheduledId, taskId, due);
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

                    var scheduleId = (long)cmd.ExecuteScalar();
                    CreateSchedule(scheduleId, taskId, due);
                });
            });
        }

        /// <inheritdoc />
        public void DeleteSchedule(int id)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sql = "delete from task_due where id = @id";

                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@id", id);

                    if (cmd.ExecuteNonQuery() != 1)
                        throw new HyperlambdaException($"Task with ID of '{id}' was not found.");
                });
            });
        }

        #endregion

        #region [ -- Private helper methods -- ]

        /*
         * Returns schedules for task.
         */
        IEnumerable<Schedule> GetSchedules(IDbConnection connection, string id)
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
         * Creates a timer for the specified schedule ID and executes the task at
         * that specific time.
         */
        void CreateSchedule(long scheduleId, string taskId, DateTime due)
        {
            /*
             * Notice, since the maximum future date for Timer is 45 days into the future, we
             * might have to do some "trickery" here to make sure we tick in the timer 45 days from
             * now, and postpone the execution if the schedule is for more than 45 days into the future.
             */
            var nextDue = (long)Math.Max(
                250L,
                Math.Min(
                    (due - DateTime.UtcNow).TotalMilliseconds,
                    new TimeSpan(45, 0, 0, 0).TotalMilliseconds));
            var postpone = (due - DateTime.UtcNow).TotalMilliseconds > new TimeSpan(45, 0, 0, 0).TotalMilliseconds;
            var timer = new Timer((state) =>
            {
                // Checking if we have to postpone execution of task further into the future.
                if (postpone)
                {
                    // More than 45 days until schedule is due, hence just re-creating our timer.
                    CreateSchedule(scheduleId, taskId, due);
                    return;
                }
            }, null, nextDue, Timeout.Infinite);
        }

        #endregion
    }
}
