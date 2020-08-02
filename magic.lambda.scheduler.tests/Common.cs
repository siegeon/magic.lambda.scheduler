/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
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
using Microsoft.Extensions.Configuration;
using Moq;

namespace magic.lambda.scheduler.tests
{
    public static class Common
    {
        public class Logger : ILogger
        {
            public void LogError(string jobName, Exception err)
            {
                /*
                 * Our implementation here in its unit tests simply rethrows
                 * any exceptions, to make them propagate, and raise an error
                 * for our unit tests.
                 */
                throw err;
            }

            public void LogInfo(string description)
            {
                Console.WriteLine(description);
            }
        }

        static public Node Evaluate(string hl, bool deleteExistingJobs = true, bool isFolder = false)
        {
            var services = Initialize(deleteExistingJobs, isFolder);
            var lambda = new Parser(hl).Lambda();
            var signaler = services.GetService(typeof(ISignaler)) as ISignaler;
            signaler.Signal("eval", lambda);
            return lambda;
        }

        #region [ -- Private helper methods -- ]

        static IServiceProvider Initialize(bool deleteJobFile, bool isFolder)
        {
            var services = new ServiceCollection();
            services.AddTransient<ISignaler, Signaler>();
            var types = new SignalsProvider(InstantiateAllTypes<ISlot>(services));
            services.AddTransient<ISignalsProvider>((svc) => types);
            services.AddTransient<ILogger, Logger>();
            var jobPath = AppDomain.CurrentDomain.BaseDirectory +
                (isFolder ? "tasks/" : "tasks.hl");
            if (deleteJobFile && File.Exists(jobPath))
                File.Delete(jobPath);
            else if (deleteJobFile && Directory.Exists(jobPath))
                Directory.Delete(jobPath, true);
            var mockConfiguration = new Mock<IConfiguration>();
            mockConfiguration.SetupGet(x => x[It.Is<string>(x => x == "magic:lambda:while:max-iterations")]).Returns("5000");
            mockConfiguration.SetupGet(x => x[It.Is<string>(x => x == "magic:database:default")]).Returns("mysql");
            services.AddTransient((svc) => mockConfiguration.Object);
            services.AddSingleton((svc) => new Scheduler(svc, null, mockConfiguration.Object, true));
            var provider = services.BuildServiceProvider();

            // Ensuring BackgroundService is created and started.
            var backgroundServices = provider.GetService<Scheduler>();
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
