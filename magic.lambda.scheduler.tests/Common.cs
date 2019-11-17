/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using magic.node;
using magic.signals.services;
using magic.signals.contracts;
using magic.lambda.scheduler.utilities;
using magic.node.extensions.hyperlambda;

namespace magic.lambda.scheduler.tests
{
    public static class Common
    {
        public class Logger : ILogger
        {
            public void LogError(string taskName, Exception err)
            {
                /*
                 * Our implementation here in its unit tests simply rethrows
                 * any exceptions, to make them propagate, and raise an error
                 * for our unit tests.
                 */
                throw err;
            }
        }

        static public Node Evaluate(string hl)
        {
            var services = Initialize();
            var lambda = new Parser(hl).Lambda();
            var signaler = services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("eval", lambda);
            return lambda;
        }

        #region [ -- Private helper methods -- ]

        static IServiceProvider Initialize()
        {
            File.Delete(Directory.GetCurrentDirectory().Replace("\\", "/").TrimEnd('/') + "/tasks.hl");
            var services = new ServiceCollection();
            services.AddTransient<ISignaler, Signaler>();
            var types = new SignalsProvider(InstantiateAllTypes<ISlot>(services));
            services.AddTransient<ISignalsProvider>((svc) => types);
            services.AddTransient<ILogger, Logger>();
            var tasksFile = AppDomain.CurrentDomain.BaseDirectory + "tasks.hl";
            if (File.Exists(tasksFile))
                File.Delete(tasksFile);
            services.AddSingleton((svc) => new TaskScheduler(svc, tasksFile));
            var provider = services.BuildServiceProvider();

            // Ensuring BackgroundService is created and started.
            var backgroundServices = provider.GetService<TaskScheduler>();
            backgroundServices.Start();
            return provider;
        }

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
