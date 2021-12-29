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

namespace magic.lambda.scheduler.utilities
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

                    sqlBuilder.Append(GetTail());

                    // Assigning SQL to command text.
                    cmd.CommandText = sqlBuilder.ToString();

                    // Creating our offset argument.
                    var parOffset = cmd.CreateParameter();
                    parOffset.ParameterName = "@offset";
                    parOffset.Value = offset;
                    cmd.Parameters.Add(parOffset);

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
                            var idxResult = new MagicTask(reader.GetString(0), reader.GetString(1), reader.GetString(2))
                            {
                                Created = reader.GetDateTime(3)
                            };
                            yield return idxResult;
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
        public MagicTask Get(string id)
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

                    sqlBuilder.Append(GetTail());

                    // Assigning SQL to command text.
                    cmd.CommandText = sqlBuilder.ToString();

                    // Creating our offset argument.
                    var parOffset = cmd.CreateParameter();
                    parOffset.ParameterName = "@offset";
                    parOffset.Value = 0L;
                    cmd.Parameters.Add(parOffset);

                    // Creating our offset argument.
                    var parLimit = cmd.CreateParameter();
                    parLimit.ParameterName = "@limit";
                    parLimit.Value = 1L;
                    cmd.Parameters.Add(parLimit);

                    // Executing command.
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new MagicTask(reader.GetString(0), reader.GetString(1), reader.GetString(2))
                            {
                                Created = reader.GetDateTime(3)
                            };
                        }
                    }
                    return null;
                }
            }
        }

        /// <inheritdoc />
        public void Execute(string id)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region [ -- Interface implementation for ITaskScheduler -- ]

        /// <inheritdoc />
        public bool IsRunning
        {
            get => throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Start()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Stop()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public DateTime? Next()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Schedule(MagicTask task, IRepetitionPattern repetition)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Delete(int id)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region [ -- Private helper methods -- ]

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
        string GetTail()
        {
            var dbType = _configuration["magic:databases:default"];
            switch (dbType)
            {
                case "mssql":
                    return " offset @offset rows fetch next @limit rows only";
                default:
                    return " offset @offset limit @limit";
            }
        }

        #endregion
    }
}
