/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.signals.contracts;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// The background service responsible for scheduling and executing jobs.
    ///
    /// Notice, you should make sure you resolve this as a singleton if you are
    /// using an IoC container. Also notice that no jobs will execute before
    /// you explicitly somehow invoke Start on your instance, which you can do
    /// automatically by instantiating the class with autoStart set to true.
    /// 
    /// The class is thread safe, and all operations towards its internal
    /// list of jobs is synchronized
    /// </summary>
    public sealed class Scheduler : IDisposable
    {
        readonly SemaphoreSlim _sempahore;
        readonly IServiceProvider _services;
        readonly ILogger _logger;
        readonly Synchronizer<Jobs> _jobs;

        /// <summary>
        /// Creates a new scheduler, responsible for scheduling and
        /// executing jobs that have been scheduled for future execution.
        /// </summary>
        /// <param name="services">Service provider to resolve ISignaler.</param>
        /// <param name="logger">Logging provider necessary to be able to log jobs that are
        /// not executed successfully.</param>
        /// <param name="jobFile">The path to your job file,
        /// declaring what jobs your application has scheduled for future
        /// execution. Jobs will be serialized into this file, such that if the
        /// process for some reasons is taken down, the jobs will be reloaded the next
        /// time the scheduler is instantiated again.</param>
        /// <param name="autoStart">If true, will start service immediately automatically.</param>
        /// <param name="maxThreads">Maximum number of concurrent jobs to execute simultaneously.</param>
        public Scheduler(
            IServiceProvider services,
            ILogger logger,
            string jobFile,
            bool autoStart,
            int maxThreads)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger;
            _sempahore = new SemaphoreSlim(maxThreads);
            _jobs = new Synchronizer<Jobs>(new Jobs(jobFile));
            if (autoStart)
                Start();
        }

        /// <summary>
        /// Returns true if scheduler is running.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Starts your scheduler. You must invoke this method in order to
        /// start your scheduler, or instantiate the class with autoStart set
        /// to true.
        /// </summary>
        public void Start()
        {
            _jobs.Write(jobs =>
            {
                Running = true;
                foreach (var idx in jobs.List())
                {
                    idx.Schedule(async (x) => await Execute(x));
                }
            });
        }

        /// <summary>
        /// Stops your scheduler, such that no more jobs will be executed, before it
        /// is explicitly started again.
        /// </summary>
        public void Stop()
        {
            _jobs.Write(jobs =>
            {
                Running = false;
                foreach (var idx in jobs.List())
                {
                    idx.Stop();
                }
            });
        }

        /// <summary>
        /// Adds a new job to the scheduler.
        ///
        /// Notice, any previously added jobs with the same name will be deleted.
        /// The jobs added through this method will also be serialized to disc,
        /// to the specified jobs file.
        /// </summary>
        /// <param name="job">Job to add.</param>
        public void Add(Job job)
        {
            _jobs.Write((jobs) =>
            {
                jobs.Add(job);
                if (Running)
                    job.Schedule(async (x) => await Execute(x));
            });
        }

        /// <summary>
        /// Returns all jobs in the scheduler to caller.
        /// </summary>
        /// <returns>All jobs registered in the scheduler.</returns>
        public List<Job> List()
        {
            return _jobs.Read((jobs) => jobs.List().ToList());
        }

        /// <summary>
        /// Returns a previously created job to caller.
        /// </summary>
        /// <param name="jobName">Name of job you wish to retrieve.</param>
        /// <returns>A node representing your job.</returns>
        public Job Get(string jobName)
        {
            // Getting job with specified name.
            return _jobs.Read((jobs) => jobs.Get(jobName));
        }

        /// <summary>
        /// Deletes an existing job from your scheduler.
        /// </summary>
        /// <param name="jobName">Name of job to delete.</param>
        public void Delete(string jobName)
        {
            _jobs.Write((jobs) => jobs.Delete(jobName));
        }

        #region [ -- Interface implementations -- ]

        /// <summary>
        /// Disposes the scheduler.
        /// </summary>
        public void Dispose()
        {
            _jobs.Dispose();
        }

        #endregion

        #region [ -- Private helper methods -- ]

        /*
         * Callback method that is executed when a job is due.
         * 
         * Simply evaluates the lambda associated with the job, and recalculates
         * when the job is due again, if the job is repeating - Otherwise, it'll
         * delete the job from the list of jobs after having executed it.
         */
        async Task Execute(Job job)
        {
            // Making sure no more than "maxThreads" are executed simultaneously.
            await _sempahore.WaitAsync();

            try
            {
                _logger?.LogInfo($"Job with name of '{job.Name}' started executing.");
                var lambda = job.Lambda.Clone();
                var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
                await signaler.SignalAsync("wait.eval", lambda);
                _logger?.LogInfo($"Job with name of '{job.Name}' executed successfully.");
            }
            catch (Exception err)
            {
                _logger?.LogError(job.Name, err);
            }
            finally
            {
                _jobs.Write((jobs) =>
                {
                    if (!job.Repeats)
                        jobs.Delete(job.Name);
                    else if (Running)
                        job.Schedule(async (x) => await Execute(x));
                });
                _sempahore.Release();
            }
        }

        #endregion
    }
}
