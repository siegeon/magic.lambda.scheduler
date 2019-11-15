/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using sys = System.Threading.Tasks;
using magic.node;
using magic.signals.contracts;
using magic.node.extensions.hyperlambda;

namespace magic.lambda.scheduler.utilities
{
    public class Tasks
    {
        readonly Synchronizer<List<Task>> _tasks = new Synchronizer<List<Task>>(new List<Task>());
        readonly Synchronizer<string> _tasksFile;
        readonly IServiceProvider _services;

        public Tasks(IServiceProvider services, string tasksFile)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _tasksFile = new Synchronizer<string>(tasksFile ?? throw new ArgumentNullException(nameof(tasksFile)));
            ReadTasksFile();
        }

        public void AddTask(Node node)
        {
            var task = new Task(node);
            _tasks.Write(tasks => tasks.Add(task));
        }

        public TimeSpan NextTaskDue()
        {
            return _tasks.Read(tasks =>
            {
                // If no tasks exists, we return max value to caller.
                if (tasks.Count() == 0)
                    return TimeSpan.MaxValue;

                // Finding first task and its due DateTime.
                var task = tasks.First();
                var taskTime = task.Due;

                // checking if task is in the past, at which point we return min value for TimeSpan.
                var now = DateTime.Now;
                if (now > taskTime)
                    return TimeSpan.MinValue;

                // Returning the timespan for when next task should be evaluated.
                return taskTime - now;
            });
        }

        public bool ExecuteNextTask()
        {
            var current = _tasks.Read(tasks => tasks.FirstOrDefault());
            if (current == null)
                return false; // No more current tasks.

            var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
            try
            {
                var lambda = current.Lambda.Clone();
                sys.Task.Factory.StartNew(async () => await signaler.SignalAsync("wait.eval", lambda));
            }
            catch (Exception err)
            {
                // We can NEVER let exceptions penetrate beyond here!
                // TODO: Logging.
            }
            finally
            {
                if (current.CalculateNextDueDate())
                    _tasks.Write(tasks => tasks.Sort());
                else
                    _tasks.Write(tasks => tasks.Remove(current));
            }
            return true;
        }

        #region [ -- Private helper methods -- ]

        void ReadTasksFile()
        {
            var lambda = _tasksFile.Read(path =>
            {
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        return new Parser(stream).Lambda();
                    }
                }
                return new Node();
            });
            _tasks.Write(tasks =>
            {
                foreach (var idx in lambda.Children)
                {
                    tasks.Add(new Task(idx));
                }
                tasks.Sort();
            });
        }

        #endregion
    }
}
