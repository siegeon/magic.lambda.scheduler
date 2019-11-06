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
    /// Common class, that helps initialize scheduler, and contains some helper methods.
    /// </summary>
    public static class Common
    {
        // List of tasks.
        static Synchronizer<Node> _tasks = new Synchronizer<Node>(new Node());
        static object _locker = new object();

        /// <summary>
        /// Returns the folder where your tasks are stored.
        /// </summary>
        public static string TasksFolder { get; private set; }

        /// <summary>
        /// Returns your task declaration files, where tasks are declared.
        /// </summary>
        public static string TasksFile { get; private set; }

        /// <summary>
        /// Invoke with the root path you intend to use for maintaining your scheduled tasks.
        /// </summary>
        /// <param name="rootPath">The root path for your scheduled tasks.</param>
        public static void Initialize(string rootPath)
        {
            lock (_locker)
            {
                // Sanity checking for double invocations.
                if (TasksFolder != null)
                    throw new ApplicationException($"You can't invoke {nameof(Common)}.{nameof(Initialize)} twice for the task scheduler.");

                // Sanitizing folder path.
                rootPath = rootPath.Replace("\\", "/").Replace("//", "/").TrimEnd('/') + "/";

                // Sanity checking invocation.
                if (!Directory.Exists(rootPath))
                    throw new ArgumentException($"'{rootPath}' is not a folder. Root path for the scheduler needs to be a folder.");

                // Ensuring "/tasks/" folder exists within root.
                var tasksFolder = Path.Combine(rootPath, "tasks/");
                if (!Directory.Exists(tasksFolder))
                    Directory.CreateDirectory(tasksFolder);

                // Ensuring tasks Hyperlambda file exists in root folder.
                var tasksFile = Path.Combine(rootPath, "tasks.hl");
                if (!File.Exists(tasksFile))
                {
                    // Creating a default empty task file.
                    using (var stream = File.CreateText(tasksFile))
                    {
                        stream.Write(@"/*
 * You have no tasks.
 * When you do, they can be found in this file.
 */
");
                    }
                }
                else
                {
                    // Loading existing task file, and populating shared tasks object.
                    using (var reader = File.OpenText(tasksFile))
                    {
                        var hl = reader.ReadToEnd();
                        var lambda = new Parser(hl).Lambda();
                        _tasks.Write(tasks =>
                        {
                            tasks.AddRange(lambda.Children.ToList());
                        });
                    }
                }

                // Setting properties for tasks scheduler.
                TasksFolder = tasksFolder;
                TasksFile = tasksFile;
            }
        }

        #region [ -- Private and internal helper methods -- ]

        /*
         * Internal helper method that actually adds the task to the task list manager.
         */
        internal static void AddTask(Node task)
        {
            // Retrieving task name and sanity checking the name.
            var taskName = task.GetEx<string>();
            SanityCheckTaskName(taskName);

            // Synchronizing access to scheduled tasks.
            _tasks.Write((tasks) =>
            {
                // Making sure we delete any old task with the same name.
                var current = tasks.Children.FirstOrDefault(child => child.Name == taskName);
                if (current != null)
                    tasks.Remove(current);

                // Creating our new task.
                current = new Node(taskName);
                if (task.Children.Any(x => x.Name == "when"))
                    current.Add(new Node("when", task.Children.First(x => x.Name == "when").GetEx<DateTime>()));

                // Retrieving [.lambda] lambda from task, and sanity checking it exists.
                var lambda = task.Children.FirstOrDefault(x => x.Name == ".lambda");
                if (lambda == null)
                    throw new ArgumentException("No [.lambda] node found in task.");

                // Serializing Hyperlambda file containing [.lambda] object from task to disc.
                var hl = Generator.GetHyper(lambda.Children);
                using (var writer = File.CreateText(TasksFolder + taskName + ".hl"))
                {
                    writer.Write(hl);
                }

                // Adding task to global shared task list.
                tasks.Add(current);

                // Serializing all tasks to tasks file.
                hl = Generator.GetHyper(tasks.Children);
                using (var writer = File.CreateText(TasksFile))
                {
                    writer.Write(hl);
                }
            });
        }

        /*
         * Internal helper method to retrieve task with specified name.
         */
        internal static Node GetTask(string name)
        {
            return _tasks.Read(tasks =>
            {
                var result = tasks.Children.FirstOrDefault(x => x.Name == name)?.Clone();
                using (var reader = File.OpenText(TasksFolder + "/" + name + ".hl"))
                {
                    var hl = reader.ReadToEnd();
                    var lambda = new Parser(hl).Lambda();
                    lambda.Name = ".lambda";
                    result.Add(lambda);
                }
                return result;
            });
        }

        /*
         * Deletes a task from the scheduler.
         */
        internal static void DeleteTask(string taskName)
        {
            _tasks.Write(tasks =>
            {
                var task = tasks.Children.FirstOrDefault(x => x.Name == taskName);
                if (task == null)
                    throw new ArgumentException($"Task with the name of {taskName} was not found");
                task.UnTie();
                File.Delete(TasksFolder + task.Name + ".hl");
            });
        }

        /*
         * Returns all tasks in the system.
         */
        internal static IEnumerable<Node> GetTasks()
        {
            var list = new List<Node>();
            _tasks.Read(tasks =>
            {
                list.AddRange(tasks.Children.Select(x => x.Clone()));
            });
            return list;
        }

        /*
         * Sanity checks name of task, since it needs to be serialized to disc.
         */
        static void SanityCheckTaskName(string taskName)
        {
            foreach (var idxChar in taskName)
            {
                if ("abcdefghijklmnopqrstuvwxyz_-1234567890".IndexOf(idxChar) == -1)
                    throw new ArgumentException($"You can only use alphanumeric characters [a-z] and [0-1], in addition to '_' and '-' in task names. Taks {taskName} is not a legal taskname");
            }
        }

        #endregion
    }
}
