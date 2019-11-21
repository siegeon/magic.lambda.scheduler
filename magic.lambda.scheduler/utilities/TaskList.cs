/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions.hyperlambda;

namespace magic.lambda.scheduler.utilities
{
    /*
     * Internal class to keep track of upcoming tasks, and sort them according
     * to their due dates.
     *
     * Also responsible for loading and saving tasks to disc, etc.
     */
    internal class TaskList
    {
        // Making sure we have synchronized access to our task list.
        readonly List<ScheduledTask> _tasks = new List<ScheduledTask>();
        readonly string _tasksFile;

        /*
         * Creates a new task manager, by loading serialized tasks from the
         * given tasksFile path.
         */
        public TaskList(string tasksFile)
        {
            _tasksFile = tasksFile ?? throw new ArgumentNullException(nameof(tasksFile));
            ReadTasksFile();
        }

        /*
         * Adds a new task to the task manager.
         */
        public void AddTask(Node node)
        {
            // Creating our task.
            var task = new ScheduledTask(node);

            // Making sure we never add more than 1.000 tasks.
            if (_tasks.Count >= 1000)
                throw new ApplicationException("The task scheduler only supports a maximum of 1.000 tasks to avoid flooding your server accidenatlly.");

            _tasks.RemoveAll(x => x.Name == task.Name);
            _tasks.Add(task);
            _tasks.Sort();
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
        public ScheduledTask GetTask(string name)
        {
            return _tasks.FirstOrDefault(x => x.Name == name);
        }

        /*
         * Returns the next upcoming task from the task manager.
         */
        public ScheduledTask NextDueTask()
        {
            return _tasks.FirstOrDefault();
        }

        /*
         * Returns all tasks to caller.
         */
        public IEnumerable<ScheduledTask> List()
        {
            return _tasks.ToList();
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
                _tasks.Add(new ScheduledTask(idx));
            }
            _tasks.Sort();
        }

        /*
         * Saves tasks file to disc.
         */
        void SaveTasksFile()
        {
            var hyper = Generator.GetHyper(_tasks.Select(x => x.RootNode));
            using (var stream = File.CreateText(_tasksFile))
            {
                stream.Write(hyper);
            }
        }

        #endregion
    }
}
