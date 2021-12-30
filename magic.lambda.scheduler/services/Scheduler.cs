﻿/*
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
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("insert into tasks (id, hyperlambda")
                    .Append(task.Description == null ? ")" : ", description)")
                    .Append(" values (@id, @hyperlambda")
                    .Append(task.Description == null ? ")" : ", @description)");

                // Creating our SQL command, making sure we dispose it when we're done with it.
                DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    // Creating our ID argument.
                    var parId = cmd.CreateParameter();
                    parId.ParameterName = "@id";
                    parId.Value = task.ID;
                    cmd.Parameters.Add(parId);

                    // Creating our Hyperlambda argument.
                    var parHyp = cmd.CreateParameter();
                    parHyp.ParameterName = "@hyperlambda";
                    parHyp.Value = task.Hyperlambda;
                    cmd.Parameters.Add(parHyp);

                    // Checking if we've got a description, and if so creating our description parameter.
                    if (!string.IsNullOrEmpty(task.Description))
                    {
                        var parDesc = cmd.CreateParameter();
                        parDesc.ParameterName = "@description";
                        parDesc.Value = task.Description;
                        cmd.Parameters.Add(parDesc);
                    }

                    // Executing command.
                    cmd.ExecuteNonQuery();
                });
            });
        }

        /// <inheritdoc />
        public void Update(MagicTask task)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                // Creating our SQL.
                var sql = "update tasks set description = @description, hyperlambda = @hyperlambda where id = @id";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    // Creating our ID argument.
                    var parId = cmd.CreateParameter();
                    parId.ParameterName = "@id";
                    parId.Value = task.ID;
                    cmd.Parameters.Add(parId);

                    // Creating our Hyperlambda argument.
                    var parHyp = cmd.CreateParameter();
                    parHyp.ParameterName = "@hyperlambda";
                    parHyp.Value = task.Hyperlambda;
                    cmd.Parameters.Add(parHyp);

                    // Creating our description argument.
                    var parDesc = cmd.CreateParameter();
                    parDesc.ParameterName = "@description";
                    parDesc.Value = task.Description;
                    cmd.Parameters.Add(parDesc);

                    // Executing command.
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
                // Creating our SQL.
                var sql = "delete from tasks where id = @id";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    // Creating our ID argument.
                    var parId = cmd.CreateParameter();
                    parId.ParameterName = "@id";
                    parId.Value = id;
                    cmd.Parameters.Add(parId);

                    // Executing command.
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
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select id, description, hyperlambda, created from tasks")
                    .Append(string.IsNullOrEmpty(filter) ? "" : " where id like @filter or description like @filter")
                    .Append(DatabaseHelper.GetPagingSql(_configuration, offset, limit));

                // Creating our SQL command, making sure we dispose it when we're done with it.
                return DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    // Checking if we've got a filter condition.
                    if (!string.IsNullOrEmpty(filter))
                    {
                        // Creating our filter argument.
                        var parFilter = cmd.CreateParameter();
                        parFilter.ParameterName = "@filter";
                        parFilter.Value = filter;
                        cmd.Parameters.Add(parFilter);
                    }

                    // Creating our offset argument.
                    if (offset > 0)
                    {
                        var parOffset = cmd.CreateParameter();
                        parOffset.ParameterName = "@offset";
                        parOffset.Value = offset;
                        cmd.Parameters.Add(parOffset);
                    }

                    // Creating our limit argument.
                    var parLimit = cmd.CreateParameter();
                    parLimit.ParameterName = "@limit";
                    parLimit.Value = limit;
                    cmd.Parameters.Add(parLimit);

                    // Executing command.
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
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select count(*) from tasks")
                    .Append(string.IsNullOrEmpty(filter) ? "" : " where id like @filter or description like @filter");

                // Creating our SQL command, making sure we dispose it when we're done with it.
                return DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    // Checking if we've got a filter condition.
                    if (!string.IsNullOrEmpty(filter))
                    {
                        // Creating our filter argument.
                        var parFilter = cmd.CreateParameter();
                        parFilter.ParameterName = "@filter";
                        parFilter.Value = filter;
                        cmd.Parameters.Add(parFilter);
                    }

                    // Executing command.
                    return (long)cmd.ExecuteScalar();
                });
            });
        }

        /// <inheritdoc />
        public MagicTask Get(string id, bool schedules = false)
        {
            return DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder
                    .Append("select id, description, hyperlambda, created from tasks where id = @id")
                    .Append(DatabaseHelper.GetPagingSql(_configuration, 0L, 1L));

                // Creating our SQL command, making sure we dispose it when we're done with it.
                return DatabaseHelper.CreateCommand(connection, sqlBuilder.ToString(), (cmd) =>
                {
                    // Creating our filter argument.
                    var parFilter = cmd.CreateParameter();
                    parFilter.ParameterName = "@id";
                    parFilter.Value = id;
                    cmd.Parameters.Add(parFilter);

                    // Creating our offset argument.
                    var parLimit = cmd.CreateParameter();
                    parLimit.ParameterName = "@limit";
                    parLimit.Value = 1L;
                    cmd.Parameters.Add(parLimit);

                    // Executing command and putting results into temporary variable.
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

                    // Checking if caller wants to return schedules too.
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
                // Creating our SQL.
                var sql = "insert into task_due (task, due, repeats) values (@task, @due, @repeats)";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    // Creating our task ID argument.
                    var parTask = cmd.CreateParameter();
                    parTask.ParameterName = "@task";
                    parTask.Value = taskId;
                    cmd.Parameters.Add(parTask);

                    // Creating our due date argument.
                    var parDue = cmd.CreateParameter();
                    parDue.ParameterName = "@due";
                    parDue.Value = repetition.Next();
                    cmd.Parameters.Add(parDue);

                    // Creating our due date argument.
                    var parRep = cmd.CreateParameter();
                    parRep.ParameterName = "@repeats";
                    parRep.Value = repetition.Value;
                    cmd.Parameters.Add(parRep);

                    // Executing command.
                    cmd.ExecuteNonQuery();
                });
            });
        }

        /// <inheritdoc />
        public void Schedule(string taskId, DateTime due)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                // Creating our SQL.
                var sql = "insert into task_due (task, due) values (@task, @due)";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                DatabaseHelper.CreateCommand(connection, sql, (cmd) =>
                {
                    // Creating our task ID argument.
                    var parTask = cmd.CreateParameter();
                    parTask.ParameterName = "@task";
                    parTask.Value = taskId;
                    cmd.Parameters.Add(parTask);

                    // Creating our due date argument.
                    var parDue = cmd.CreateParameter();
                    parDue.ParameterName = "@due";
                    parDue.Value = due;
                    cmd.Parameters.Add(parDue);

                    // Executing command.
                    cmd.ExecuteNonQuery();
                });
            });
        }

        /// <inheritdoc />
        public void Delete(int id)
        {
            DatabaseHelper.Connect(_signaler, _configuration, (connection) =>
            {
                // Creating our SQL.
                var sql = "delete from task_due where id = @id";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Assigning SQL to command text.
                    cmd.CommandText = sql;

                    // Creating our ID argument.
                    var parId = cmd.CreateParameter();
                    parId.ParameterName = "@id";
                    parId.Value = id;
                    cmd.Parameters.Add(parId);

                    // Executing command.
                    if (cmd.ExecuteNonQuery() != 1)
                        throw new HyperlambdaException($"Task with ID of '{id}' was not found.");
                }
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
                var parLimit = cmd.CreateParameter();
                parLimit.ParameterName = "@task";
                parLimit.Value = id;
                cmd.Parameters.Add(parLimit);

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
