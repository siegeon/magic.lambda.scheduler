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
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append("insert into tasks (id, hyperlambda");
                if (!string.IsNullOrEmpty(task.Description))
                    sqlBuilder.Append(", description");
                sqlBuilder.Append(") values (@id, @hyperlambda");
                if (!string.IsNullOrEmpty(task.Description))
                    sqlBuilder.Append(", @description");
                sqlBuilder.Append(")");

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Assigning SQL to command text.
                    cmd.CommandText = sqlBuilder.ToString();

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
                }
            }
        }

        /// <inheritdoc />
        public void Update(MagicTask task)
        {
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sql = "update tasks set description = @description, hyperlambda = @hyperlambda where id = @id";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Assigning SQL to command text.
                    cmd.CommandText = sql;

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
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <inheritdoc />
        public void Delete(string id)
        {
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sql = "delete from tasks where id = @id";

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
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<MagicTask> List(string filter, long offset, long limit)
        {
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append("select id, description, hyperlambda, created from tasks");

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Checking if we've got a filter condition.
                    if (!string.IsNullOrEmpty(filter))
                    {
                        sqlBuilder.Append(" where id like @filter or description like @filter");

                        // Creating our filter argument.
                        var parFilter = cmd.CreateParameter();
                        parFilter.ParameterName = "@filter";
                        parFilter.Value = filter;
                        cmd.Parameters.Add(parFilter);
                    }

                    sqlBuilder.Append(GetTail(offset, limit));

                    // Assigning SQL to command text.
                    cmd.CommandText = sqlBuilder.ToString();

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
                        while (reader.Read())
                        {
                            yield return new MagicTask(reader[0] as string, reader[1] as string, reader[2] as string)
                            {
                                Created = (DateTime)reader[3]
                            };
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public long Count(string filter)
        {
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append("select count(*) from tasks");

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Checking if we've got a filter condition.
                    if (!string.IsNullOrEmpty(filter))
                    {
                        sqlBuilder.Append(" where id like @filter or description like @filter");

                        // Creating our filter argument.
                        var parFilter = cmd.CreateParameter();
                        parFilter.ParameterName = "@filter";
                        parFilter.Value = filter;
                        cmd.Parameters.Add(parFilter);
                    }

                    // Assigning SQL to command text.
                    cmd.CommandText = sqlBuilder.ToString();

                    // Executing command.
                    return (long)cmd.ExecuteScalar();
                }
            }
        }

        /// <inheritdoc />
        public MagicTask Get(string id, bool schedules = false)
        {
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append("select id, description, hyperlambda, created from tasks where id = @id");

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Creating our filter argument.
                    var parFilter = cmd.CreateParameter();
                    parFilter.ParameterName = "@id";
                    parFilter.Value = id;
                    cmd.Parameters.Add(parFilter);

                    sqlBuilder.Append(GetTail(0L, 1L));

                    // Assigning SQL to command text.
                    cmd.CommandText = sqlBuilder.ToString();

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
                    }

                    // Checking if caller wants to return schedules too.
                    if (schedules)
                    {
                        foreach (var idx in GetSchedules(connection, result.ID))
                        {
                            result.Schedules.Add(idx);
                        }
                    }
                    return result;
                }
            }
        }

        /// <inheritdoc />
        public void Execute(string id)
        {
            // Retrieving task.
            var task = Get(id);
            if (task == null)
                throw new HyperlambdaException($"Task with id of '{id}' was not found");

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
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sql = "insert into task_due (task, due, repeats) values (@task, @due, @repeats)";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Assigning SQL to command text.
                    cmd.CommandText = sql;

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
                }
            }
        }

        /// <inheritdoc />
        public void Schedule(string taskId, DateTime due)
        {
            // Creating our connection, making sure we dispose it when we're done with it.
            using (var connection = CreateConnection())
            {
                // Creating our SQL.
                var sql = "insert into task_due (task, due) values (@task, @due)";

                // Creating our SQL command, making sure we dispose it when we're done with it.
                using (var cmd = connection.CreateCommand())
                {
                    // Assigning SQL to command text.
                    cmd.CommandText = sql;

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
                }
            }
        }

        /// <inheritdoc />
        public void Delete(int id)
        {
            throw new NotImplementedException();
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
            using (var cmd = connection.CreateCommand())
            {
                // Assigning SQL to command text.
                cmd.CommandText = sql;

                // Creating our limit argument.
                var parLimit = cmd.CreateParameter();
                parLimit.ParameterName = "@task";
                parLimit.Value = id;
                cmd.Parameters.Add(parLimit);

                // Executing command.
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new contracts.Schedule((DateTime)reader[1], reader[2] as string)
                        {
                            Id = (int)reader[0],
                        };
                    }
                }
            }
        }

        /*
         * Creates and returns an IDbConnection using factory slot, and returns the result to caller.
         */
        IDbConnection CreateConnection()
        {
            // Creating our database connection.
            var dbType = _configuration["magic:databases:default"];
            var dbNode = new Node();
            _signaler.Signal($".db-factory.connection.{dbType}", dbNode);
            var connection = dbNode.Get<IDbConnection>();

            // Opening up database connection.
            connection.ConnectionString = _configuration[$"magic:databases:{dbType}:generic"].Replace("{database}", "magic");
            connection.Open();

            // Returning open connection to caller.
            return connection;
        }

        /*
         * Returns paging SQL parts to caller according to database type.
         */
        string GetTail(long offset, long limit)
        {
            var dbType = _configuration["magic:databases:default"];
            switch (dbType)
            {
                case "mssql":
                    if (offset > 0)
                        return " fetch next @limit rows only";
                    return " offset @offset rows fetch next @limit rows only";
                default:
                    if (offset > 0)
                        return " offset @offset limit @limit";
                    return " limit @limit";
            }
        }

        #endregion
    }
}
