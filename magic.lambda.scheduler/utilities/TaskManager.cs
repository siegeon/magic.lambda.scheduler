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
    internal class TaskManager
    {
        // Making sure we have synchronized access to our task list.
        readonly List<Task> _tasks = new List<Task>();

        // Making sure we have synchronized access to our tasks file.
        readonly string _tasksFile;

        /*
         * Creates a new task manager, by loading serialized tasks from the
         * given tasksFile path.
         */
        public TaskManager(string tasksFile)
        {
            _tasksFile = tasksFile ?? throw new ArgumentNullException(nameof(tasksFile));
            ReadTasksFile();
        }

        /*
         * Adds a new task to the task manager.
         */
        public void AddTask(Node node)
        {
            var task = new Task(node);
            _tasks.RemoveAll(x => x.Name == task.Name);
            _tasks.Add(task);
            _tasks.Sort();
            SaveTasksFile();
        }

        /*
         * Deletes an existing task from the task manager.
         */
        public void DeleteTask(Task task)
        {
            _tasks.Remove(task);
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
         * Returns the next upcoming task from the task manager.
         */
        public Task NextTask()
        {
            return _tasks.FirstOrDefault();
        }

        /*
         * Returns ann tasks to caller.
         */
        public IEnumerable<Task> List()
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
                _tasks.Add(new Task(idx));
            }
            _tasks.Sort();
        }

        /*
         * Saves tasks file to disc.
         */
        void SaveTasksFile()
        {
            var hyper = Generator.GetHyper(_tasks.Select(x => x._original));
            using (var stream = File.CreateText(_tasksFile))
            {
                stream.Write(hyper);
            }
        }

        #endregion
    }
}
