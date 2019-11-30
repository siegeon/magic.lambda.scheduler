/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions;
using magic.node.extensions.hyperlambda;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// Internal class to keep track of upcoming jobs.
    /// 
    /// Also responsible for loading and saving jobs to disc, etc.
    /// </summary>
    public sealed class Jobs : IDisposable
    {
        readonly List<Job> _jobs = new List<Job>();
        readonly string _jobFile;

        /// <summary>
        /// Creates a new list of jobs, by loading serialized jobs from the
        /// given job file.
        /// </summary>
        /// <param name="jobFile"></param>
        public Jobs(string jobFile)
        {
            _jobFile = jobFile ?? throw new ArgumentNullException(nameof(jobFile));
            LoadJobs();
        }

        /// <summary>
        /// Adds a new job to the internal list of jobs, and saves all jobs into the job file.
        /// 
        /// Notice, will remove any jobs it has from before, having the same name as the name
        /// of your new job.
        /// </summary>
        /// <param name="job">Job you wish to add to this instance.</param>
        public void Add(Job job)
        {
            _jobs.RemoveAll(x => x.Name == job.Name);
            _jobs.Add(job);
            SaveJobs();
        }

        /// <summary>
        /// Deletes the job with the specified name, if any.
        /// 
        /// Will also specifically stop the job, to avoid that it is executed in the future,
        /// and discards the job's timer instance.
        /// </summary>
        /// <param name="jobName">Name of job to delete.</param>
        public void Delete(string jobName)
        {
            var job = _jobs.Find(x => x.Name == jobName);
            if (job == null)
                return;
            _jobs.Remove(job);
            job.Dispose();
            SaveJobs();
        }

        /// <summary>
        /// Will return the job with the specified name, if any.
        /// </summary>
        /// <param name="jobName">Name of job to retrieve.</param>
        /// <returns></returns>
        public Job Get(string jobName)
        {
            return _jobs.FirstOrDefault(x => x.Name == jobName);
        }

        /// <summary>
        /// Lists all jobs in this instance.
        /// </summary>
        /// <returns>List of all jobs in this instance, in no particular order.</returns>
        public IEnumerable<Job> List()
        {
            return _jobs;
        }

        #region [ -- Interface implementations -- ]

        public void Dispose()
        {
            // Notice, all jobs needs to be disposed, to dispose their System.Threading.Timer instances.
            foreach (var idx in _jobs)
            {
                idx.Dispose();
            }
        }

        #endregion

        #region [ -- Private helper methods -- ]

        /*
         * Loads jobs from disc.
         */
        void LoadJobs()
        {
            if (File.Exists(_jobFile))
            {
                using (var stream = File.OpenRead(_jobFile))
                {
                    var lambda = new Parser(stream).Lambda();
                    foreach (var idx in lambda.Children)
                    {
                        // Making sure we ignore jobs that should have been executed in the past.
                        var when = idx.Children.FirstOrDefault(x => x.Name == "when");
                        if (when == null || when.Get<DateTime>() > DateTime.Now)
                            _jobs.Add(Job.CreateJob(idx, true));
                    }
                }
            }
        }

        /*
         * Saves jobs to disc.
         */
        void SaveJobs()
        {
            var hyper = Generator.GetHyper(_jobs.Select(x => x.GetNode()));
            using (var stream = File.CreateText(_jobFile))
            {
                stream.Write(hyper);
            }
        }

        #endregion
    }
}
