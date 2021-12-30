/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Data;
using System.Text;
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
        public void CreateAsync(MagicTask task)
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
        public void Update(MagicTask task)
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
        public void Delete(string id)
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
        public IEnumerable<MagicTask> List(string filter, long offset, long limit)
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

                    using (var reader = cmd.ExecuteReader())
                    {
                        var result = new List<MagicTask>();
                        while (reader.Read())
                        {
                            result.Add(new MagicTask(reader[0] as string, reader[1] as string, reader[2] as string)
                            {
                                Created = (DateTime)reader[3]
                            });
                        }
                        return result;
                    }
                });
            });
        }

        /// <inheritdoc />
        public long Count(string filter)
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
        public MagicTask Get(string id, bool schedules = false)
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

                    MagicTask result = null;
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = new MagicTask(
                                reader[0] as string,
                                reader[1] as string,
                                reader[2] as string)
                            {
                                Created = (DateTime)reader[3],
                            };
                        }
                        else
                        {
                            throw new HyperlambdaException($"Task with ID of '{id}' was not found.");
                        }
                    }

                    if (result != null && schedules)
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
        public void Execute(string id)
        {
            // Retrieving task.
            var task = Get(id);
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
        public void Schedule(string taskId, IRepetitionPattern repetition)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sql = "insert into task_due (task, due, repeats) values (@task, @due, @repeats)";

                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@task", taskId);
                    DatabaseHelper.AddParameter(cmd, "@due", repetition.Next());
                    DatabaseHelper.AddParameter(cmd, "@repeats", repetition.Value);

                    cmd.ExecuteNonQuery();
                });
            });
        }

        /// <inheritdoc />
        public void Schedule(string taskId, DateTime due)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                var sql = "insert into task_due (task, due) values (@task, @due)";

                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    DatabaseHelper.AddParameter(cmd, "@task", taskId);
                    DatabaseHelper.AddParameter(cmd, "@due", due);

                    cmd.ExecuteNonQuery();
                });
            });
        }

        /// <inheritdoc />
        public void Delete(int id)
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
            // Creating our SQL.
            var sql = "select id, due, repeats from task_due where task = @task";

            // Creating our SQL command, making sure we dispose it when we're done with it.
            return DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
            {
                // Creating our limit argument.
                DatabaseHelper.AddParameter(cmd, "@task", id);

                // Executing command.
                using (var reader = cmd.ExecuteReader())
                {
                    var result = new List<contracts.Schedule>();
                    while (reader.Read())
                    {
                        result.Add(new contracts.Schedule((DateTime)reader[1], reader[2] as string)
                        {
                            Id = (int)reader[0],
                        });
                    }
                    return result;
                }
            });
        }

        #endregion
    }
}
