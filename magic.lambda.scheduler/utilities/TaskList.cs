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
        readonly Synchronizer<List<ScheduledTask>> _tasks = new Synchronizer<List<ScheduledTask>>(new List<ScheduledTask>());
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
            var task = new ScheduledTask(node);
            _tasks.Write(tasks =>
            {
                // Making sure we never add more than 1.000 tasks
                if (tasks.Count >= 1000)
                    throw new ApplicationException("The task scheduler only supports a maximum of 1.000 tasks to avoid flooding your server accidenatlly.");

                tasks.RemoveAll(x => x.Name == task.Name);
                tasks.Add(task);
                tasks.Sort();
                SaveTasksFile(tasks);
            });
        }

        /*
         * Deletes an existing task from the task manager.
         */
        public void DeleteTask(string taskName)
        {
            _tasks.Write(tasks =>
            {
                tasks.RemoveAll(x => x.Name == taskName);
                SaveTasksFile(tasks);
            });
        }

        /*
         * Returns the task with the given name, if any.
         */
        public ScheduledTask GetTask(string name)
        {
            return _tasks.Get(tasks => tasks.FirstOrDefault(x => x.Name == name));
        }

        /*
         * Returns the next upcoming task from the task manager.
         */
        public ScheduledTask NextDueTask()
        {
            return _tasks.Get(tasks => tasks.FirstOrDefault());
        }

        /*
         * Returns all tasks to caller.
         */
        public IEnumerable<ScheduledTask> List()
        {
            return _tasks.Get(tasks => tasks.ToList());
        }

        /*
         * Sorts all tasks, which will be performed according to their upcoming
         * due dates, due to that Task implement IComparable on Due date.
         */
        public void Sort()
        {
            _tasks.Write(tasks => tasks.Sort());
        }

        /*
         * Saves tasks file to disc.
         */
        public void Save()
        {
            SaveTasksFile(_tasks.Get(tasks => tasks.ToList()));
        }

        #region [ -- Private helper methods -- ]

        /*
         * Loads tasks file from disc.
         */
        void ReadTasksFile()
        {
            _tasks.Write(tasks =>
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
                    tasks.Add(new ScheduledTask(idx));
                }
                tasks.Sort();
            });
        }

        /*
         * Saves tasks file to disc.
         */
        void SaveTasksFile(IEnumerable<ScheduledTask> tasks)
        {
            var hyper = Generator.GetHyper(tasks.Select(x => x.RootNode));
            using (var stream = File.CreateText(_tasksFile))
            {
                stream.Write(hyper);
            }
        }

        #endregion
    }
}
