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
    internal class Tasks
    {
        // Making sure we have synchronized access to our task list.
        readonly Synchronizer<List<Task>> _tasks = new Synchronizer<List<Task>>(new List<Task>());

        // Making sure we have synchronized access to our tasks file.
        readonly string _tasksFile;

        public Tasks(string tasksFile)
        {
            _tasksFile = tasksFile ?? throw new ArgumentNullException(nameof(tasksFile));
            ReadTasksFile();
        }

        public void AddTask(Node node)
        {
            var task = new Task(node);
            _tasks.Write((tasks) =>
            {
                tasks.Add(task);
                tasks.Sort();
            });
        }

        public void DeleteTask(Task task)
        {
            _tasks.Write(tasks => tasks.Remove(task));
        }

        public Task NextTask()
        {
            return _tasks.Read(tasks => tasks.FirstOrDefault());
        }

        public void Sort()
        {
            _tasks.Write(tasks => tasks.Sort());
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
            _tasks.Write(tasks =>
            {
                foreach (var idx in lambda.Children)
                {
                    tasks.Add(new Task(idx));
                }
                tasks.Sort();
            });
        }

        /*
         * Saves tasks file to disc.
         */
        void SaveTasksFile()
        {
            _tasks.Write(tasks =>
            {
                var hyper = Generator.GetHyper(tasks.Select(x => x._original));
                using (var stream = File.CreateText(_tasksFile))
                {
                    stream.Write(hyper);
                }
            });
        }

        #endregion
    }
}
