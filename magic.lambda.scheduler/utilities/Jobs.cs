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
    /*
     * Internal class to keep track of upcoming tasks, and sort them according
     * to their due dates.
     *
     * Also responsible for loading and saving tasks to disc, etc.
     */
    internal class Jobs : IDisposable
    {
        readonly List<Job> _jobs = new List<Job>();
        readonly string _jobFile;

        /*
         * Creates a new task manager, by loading serialized tasks from the
         * given tasksFile path.
         */
        public Jobs(string jobFile)
        {
            _jobFile = jobFile ?? throw new ArgumentNullException(nameof(jobFile));
            ReadJobFile();
        }

        /*
         * Adds a new task to the task manager.
         */
        public void Add(Job job)
        {
            _jobs.RemoveAll(x => x.Name == job.Name);
            _jobs.Add(job);
            SaveJobFile();
        }

        /*
         * Deletes an existing task from the task manager.
         */
        public void Delete(string jobName)
        {
            var job = _jobs.Find(x => x.Name == jobName);
            if (job == null)
                return;
            job.Stop();
            _jobs.Remove(job);
            SaveJobFile();
        }

        /*
         * Returns the task with the given name, if any.
         */
        public Job Get(string jobName)
        {
            return _jobs.FirstOrDefault(x => x.Name == jobName);
        }

        /*
         * Returns all tasks to caller.
         */
        public IEnumerable<Job> List()
        {
            return _jobs;
        }

        /*
         * Sorts all tasks, which will be performed according to their upcoming
         * due dates, due to that Task implement IComparable on Due date.
         */
        public void Sort()
        {
            _jobs.Sort();
        }

        /*
         * Saves tasks file to disc.
         */
        public void Save()
        {
            SaveJobFile();
        }

        #region [ -- Private helper methods -- ]

        /*
         * Loads tasks file from disc.
         */
        void ReadJobFile()
        {
            var lambda = new Node();
            if (File.Exists(_jobFile))
            {
                using (var stream = File.OpenRead(_jobFile))
                {
                    lambda = new Parser(stream).Lambda();
                }
            }
            foreach (var idx in lambda.Children)
            {
                /*
                 * Making sure we don't load tasks that should have been evaluated in the past.
                 */
                var when = idx.Children.FirstOrDefault(x => x.Name == "when");
                if (when == null || when.Get<DateTime>() > DateTime.Now)
                    _jobs.Add(Job.CreateJob(idx));
            }
            _jobs.Sort();
        }

        /*
         * Saves tasks file to disc.
         */
        void SaveJobFile()
        {
            var hyper = Generator.GetHyper(_jobs.Select(x => x.GetNode()));
            using (var stream = File.CreateText(_jobFile))
            {
                stream.Write(hyper);
            }
        }

        public void Dispose()
        {
            foreach (var idx in _jobs)
            {
                idx.Dispose();
            }
        }

        #endregion
    }
}
