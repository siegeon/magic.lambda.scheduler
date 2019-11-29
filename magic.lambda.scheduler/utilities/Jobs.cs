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
        readonly IServiceProvider _services;
        readonly ILogger _logger;
        readonly List<Job> _tasks = new List<Job>();
        readonly string _tasksFile;

        /*
         * Creates a new task manager, by loading serialized tasks from the
         * given tasksFile path.
         */
        public Jobs(
            IServiceProvider services, 
            ILogger logger,
            string tasksFile)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tasksFile = tasksFile ?? throw new ArgumentNullException(nameof(tasksFile));
            ReadTasksFile();
        }

        /*
         * Adds a new task to the task manager.
         */
        public void Add(Job task)
        {
            _tasks.RemoveAll(x => x.Name == task.Name);
            _tasks.Add(task);
            SaveTasksFile();
        }

        /*
         * Deletes an existing task from the task manager.
         */
        public void DeleteTask(string taskName)
        {
            _tasks.RemoveAll(x => x.Name == taskName);
            SaveTasksFile();
        }

        /*
         * Returns the task with the given name, if any.
         */
        public Job GetTask(string name)
        {
            return _tasks.FirstOrDefault(x => x.Name == name);
        }

        /*
         * Returns all tasks to caller.
         */
        public IEnumerable<Job> List()
        {
            return _tasks;
        }

        /*
         * Sorts all tasks, which will be performed according to their upcoming
         * due dates, due to that Task implement IComparable on Due date.
         */
        public void Sort()
        {
            _tasks.Sort();
        }

        /*
         * Saves tasks file to disc.
         */
        public void Save()
        {
            SaveTasksFile();
        }

        #region [ -- Private helper methods -- ]

        /*
         * Loads tasks file from disc.
         */
        void ReadTasksFile()
        {
            var lambda = new Node();
            if (File.Exists(_tasksFile))
            {
                using (var stream = File.OpenRead(_tasksFile))
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
                    _tasks.Add(Job.CreateJob(_services, _logger, idx));
            }
            _tasks.Sort();
        }

        /*
         * Saves tasks file to disc.
         */
        void SaveTasksFile()
        {
            var hyper = Generator.GetHyper(_tasks.Select(x => x.GetNode()));
            using (var stream = File.CreateText(_tasksFile))
            {
                stream.Write(hyper);
            }
        }

        public void Dispose()
        {
            foreach (var idx in _tasks)
            {
                idx.Dispose();
            }
        }

        #endregion
    }
}
