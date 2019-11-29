/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.signals.contracts;
using System.Threading;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// The background service responsible for scheduling and evaluating tasks.
    ///
    /// Notice, you should make sure you resolve this as a singleton if you are
    /// using an IoC container. Also notice that no tasks will evaluate before
    /// you explicitly somehow invoke Start on your instance.
    /// 
    /// You are also responsible to make sure all operations on instance is synchronized.
    /// </summary>
    public sealed class Scheduler : IDisposable
    {
        readonly SemaphoreSlim _sempahore;
        readonly IServiceProvider _services;
        readonly ILogger _logger;
        readonly Synchronizer<Jobs> _tasks;

        /// <summary>
        /// Creates a new background service, responsible for scheduling and
        /// evaluating tasks that have been scheduled for future evaluation.
        /// </summary>
        /// <param name="services">Service provider to resolve ISignaler.</param>
        /// <param name="logger">Logging provider necessary to be able to log tasks that are
        /// not executed successfully.</param>
        /// <param name="tasksFile">The path to your tasks file,
        /// declaring what tasks your application has scheduled for future
        /// evaluation.</param>
        /// <param name="autoStart">If true, will start service immediately automatically.</param>
        /// <param name="maxThreads">Maximum number of concurrent jobs to execute at the same time.</param>
        public Scheduler(
            IServiceProvider services, 
            ILogger logger,
            string tasksFile,
            bool autoStart,
            int maxThreads)
        {
            // Need to store service provider to be able to create ISignaler during task execution.
            _services = services ?? throw new ArgumentNullException(nameof(services));

            // Storing logger in case of exceptions during job execution.
            _logger = logger;

            _sempahore = new SemaphoreSlim(0, maxThreads);

            _tasks = new Synchronizer<Jobs>(new Jobs(tasksFile));
            if (autoStart)
                Start();
        }

        /// <summary>
        /// Returns true if scheduler is running.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Starts your scheduler. You must invoke this method in order to
        /// start your scheduler.
        /// </summary>
        public void Start()
        {
            _tasks.Write(tasks =>
            {
                Running = true;
                foreach (var idx in tasks.List())
                {
                    idx.Start(async (x) => await Execute(x));
                }
            });
        }

        /// <summary>
        /// Stops your scheduler, such that no more tasks will be evaluated.
        /// </summary>
        public void Stop()
        {
            _tasks.Write(tasks =>
            {
                Running = false;
                foreach (var idx in tasks.List())
                {
                    idx.Stop();
                }
            });
        }

        /// <summary>
        /// Creates a new task.
        ///
        /// Notice, will delete any previously created tasks with the same name.
        /// </summary>
        /// <param name="job">Task to add.</param>
        public void Add(Job job)
        {
            _tasks.Write((tasks) => tasks.Add(job));
            job.Start(async (x) => await Execute(x));
        }

        /// <summary>
        /// Lists all tasks in task manager, in order of evaluation, such that
        /// the first task in queue will be the first task returned.
        /// </summary>
        /// <returns>All tasks listed in chronological order of evaluation.</returns>
        public List<Job> List()
        {
            return _tasks.Read((tasks) => tasks.List().ToList());
        }

        /// <summary>
        /// Returns a previously created task to caller.
        /// </summary>
        /// <param name="name">Name of task you wish to retrieve.</param>
        /// <returns>A node representing your task.</returns>
        public Job Get(string name)
        {
            // Getting task with specified name.
            return _tasks.Read((tasks) => tasks.Get(name));
        }

        /// <summary>
        /// Deletes an existing task from your task manager.
        /// </summary>
        /// <param name="name">Name of task to delete.</param>
        public void Delete(string name)
        {
            _tasks.Write((tasks) => tasks.Delete(name));
        }

        #region [ -- Interface implementations -- ]

        /// <summary>
        /// Disposes the TaskList.
        /// </summary>
        public void Dispose()
        {
            _tasks.Dispose();
        }

        #endregion

        #region [ -- Private helper methods -- ]

        async Task Execute(Job job)
        {
            if (!Running)
            {
                // Postponing into the future.
                job.Start(async (x) => await Execute(x));
                return;
            }

            await _sempahore.WaitAsync();

            try
            {
                // Retrieving task and its lambda object, and evaluating it.
                var lambda = job.Lambda.Clone();
                var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
                await signaler.SignalAsync("wait.eval", lambda);
            }
            catch (Exception err)
            {
                // Making sure we log exception using preferred ILogger instance.
                _logger?.LogError(job.Name, err);
            }
            finally
            {
                if (job.Repeats)
                {
                    job.Start(async (x) => await Execute(x));
                }
                else
                {
                    _tasks.Write((tasks) =>
                    {
                        tasks.Delete(job.Name);
                    });
                }
                _sempahore.Release();
            }
        }

        #endregion
    }
}
