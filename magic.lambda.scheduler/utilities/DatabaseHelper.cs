/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Data;
using magic.node;
using magic.node.contracts;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.scheduler.utilities
{
    internal static class DatabaseHelper
    {
        /*
         * Helper method to create a connection towards the default database, and execute
         * some arbitrary function from within your connection.
         */
        public static void Connect(
            ISignaler signaler,
            IMagicConfiguration configuration,
            Action<IDbConnection> functor)
        {
            using (var connection = CreateConnection(signaler, configuration))
            {
                functor(connection);
            }
        }
        /*
         * Helper method to create a connection towards the default database, and execute
         * some arbitrary function from within your connection.
         */
        public static T Connect<T>(
            ISignaler signaler,
            IMagicConfiguration configuration,
            Func<IDbConnection, T> functor)
        {
            using (var connection = CreateConnection(signaler, configuration))
            {
                return functor(connection);
            }
        }

        /*
         * Returns paging SQL parts to caller according to database type.
         */
        public static string GetPagingSql(
            IMagicConfiguration configuration,
            long offset,
            long limit)
        {
            var dbType = configuration["magic:databases:default"];
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

        #region [ -- Private helper methods -- ]

        /*
         * Creates and returns an IDbConnection using factory slot, and returns the result to caller.
         */
        static IDbConnection CreateConnection(ISignaler signaler, IMagicConfiguration configuration)
        {
            // Creating our database connection.
            var dbType = configuration["magic:databases:default"];
            var dbNode = new Node();
            signaler.Signal($".db-factory.connection.{dbType}", dbNode);
            var connection = dbNode.Get<IDbConnection>();

            // Opening up database connection.
            connection.ConnectionString = configuration[$"magic:databases:{dbType}:generic"].Replace("{database}", "magic");
            connection.Open();

            // Returning open connection to caller.
            return connection;
        }

        #endregion
    }
}
