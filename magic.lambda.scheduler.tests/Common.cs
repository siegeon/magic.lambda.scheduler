/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using magic.node;
using magic.node.contracts;
using magic.signals.services;
using magic.signals.contracts;
using magic.lambda.logging.helpers;
using magic.node.extensions.hyperlambda;
using magic.lambda.scheduler.contracts;
using magic.lambda.scheduler.utilities;

namespace magic.lambda.scheduler.tests
{
    /*
     * Helper slot required to create a database connection for logger library.
     */
    [Slot(Name = ".db-factory.connection.mysql")]
    internal class ConnectionFactory : ISlot
    {
        public static string ConnectionString { get; private set; }
        public static bool OpenInvoked { get; private set; }
        public static string CommandText { get; private set; }
        public static List<(string, string)> Arguments { get; } = new List<(string, string)>();

        public void Signal(ISignaler signaler, Node input)
        {
            // Creating Moq objects logger internals is dependent upon.
            var dbMoq = new Mock<IDbConnection>();

            // Connection string setter.
            dbMoq
                .SetupSet(p => p.ConnectionString = It.IsAny<string>())
                .Callback<string>(value => ConnectionString = value);

            // Open method.
            dbMoq
                .Setup(x => x.Open())
                .Callback(() => OpenInvoked = true);

            // Create command method.
            dbMoq
                .Setup(x => x.CreateCommand())
                .Returns(() =>
                {
                    // Returning a Moq Command object to caller.
                    var comMoq = new Mock<IDbCommand>();

                    // Command text setter.
                    comMoq
                        .SetupSet(p => p.CommandText = It.IsAny<string>())
                        .Callback<string>(value => CommandText = value);

                    // Execute non query method.
                    comMoq.Setup(x => x.ExecuteNonQuery());

                    // Parameters getter.
                    comMoq
                        .SetupGet(p => p.Parameters)
                        .Returns(() =>
                        {
                            // Returning a new Moq data parameter collection to caller.
                            var dbParamCollMoq = new Mock<IDataParameterCollection>();
                            dbParamCollMoq
                                .Setup(x => x.Add(It.IsAny<IDbDataParameter>()));
                            return dbParamCollMoq.Object;
                        });

                    // Create parameters method.
                    comMoq
                        .Setup(p => p.CreateParameter())
                        .Returns(() => 
                        {
                            // Returning a new Moq data parameter to caller.
                            var dbParamMoq = new Mock<IDbDataParameter>();
                            dbParamMoq
                                .SetupSet(x => x.ParameterName = It.IsAny<string>())
                                .Callback<string>(x => Arguments.Add((x, null)));
                            dbParamMoq
                                .SetupSet(x => x.Value = It.IsAny<string>())
                                .Callback<object>(x => Arguments[Arguments.Count - 1] = (Arguments[Arguments.Count - 1].Item1, x as string));
                            return dbParamMoq.Object;
                        });
                    return comMoq.Object;
                });

            // Returning Moq database connection to caller.
            input.Value = dbMoq.Object;
        }
    }

    public static class Common
    {
        static public Node Evaluate(string hl)
        {
            var signaler = Initialize();
            var lambda = HyperlambdaParser.Parse(hl);
            signaler.Signal("eval", lambda);
            return lambda;
        }

        static async public Task<Node> EvaluateAsync(string hl)
        {
            var signaler = Initialize();
            var lambda = HyperlambdaParser.Parse(hl);
            await signaler.SignalAsync("eval", lambda);
            return lambda;
        }

        public static ISignaler Initialize()
        {
            return InitializeServices().GetService<ISignaler>();
        }

        public static IServiceProvider InitializeServices()
        {
            // Creating our services collection.
            var services = new ServiceCollection();

            // Adding signaler and logger.
            services.AddTransient<ISignaler, Signaler>();
            services.AddTransient<ITaskStorage, Scheduler>();
            services.AddTransient<ITaskScheduler, Scheduler>();

            // Instantiating and adding our slot/signals provider
            var types = new SignalsProvider(InstantiateAllTypes<ISlot>(services));
            services.AddTransient<ISignalsProvider>((svc) => types);

            // Creating a configuration Moq object to resolve for IoC.
            var mockConfiguration = new Mock<IMagicConfiguration>();
            mockConfiguration
                .SetupGet(x => x[It.Is<string>(x => x == "magic:databases:default")])
                .Returns(() => "mysql");
            mockConfiguration
                .SetupGet(x => x[It.Is<string>(x => x == "magic:databases:mysql:generic")])
                .Returns(() => "CONNECTION-STRING-{database}");
            services.AddTransient((svc) => mockConfiguration.Object);

            // Building and returning service provider to caller.
            return services.BuildServiceProvider();
        }

        #region [ -- Private helper methods -- ]

        static IEnumerable<Type> InstantiateAllTypes<T>(ServiceCollection services) where T : class
        {
            var type = typeof(T);
            var result = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !x.IsDynamic && !x.FullName.StartsWith("Microsoft", StringComparison.InvariantCulture))
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

            foreach (var idx in result)
            {
                services.AddTransient(idx);
            }
            return result;
        }

        #endregion
    }
}
