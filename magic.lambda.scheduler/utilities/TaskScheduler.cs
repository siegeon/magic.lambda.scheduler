/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;
using magic.signals.contracts;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// The background service responsible for scheduling and evaluating tasks.
    ///
    /// Notice, you should make sure you resolve this as a singleton if you are
    /// using an IoC container. Also notice that no tasks will evaluate before
    /// you explicitly somehow invoke Start on your instance.
    /// 
    /// You are also responsible to make sure all operations on instance is synchronized.
    /// </summary>
    public sealed class TaskScheduler : IDisposable
    {
        readonly IServiceProvider _services;
        readonly Synchronizer<TaskList> _tasks;

        /// <summary>
        /// Creates a new background service, responsible for scheduling and
        /// evaluating tasks that have been scheduled for future evaluation.
        /// </summary>
        /// <param name="services">Service provider to resolve ISignaler and
        /// ILogger if necessary.</param>
        /// <param name="tasksFile">The path to your tasks file,
        /// declaring what tasks your application has scheduled for future
        /// evaluation.</param>
        /// <param name="autoStart">If true, will start service immediately automatically.</param>
        public TaskScheduler(IServiceProvider services, string tasksFile, bool autoStart)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _tasks = new Synchronizer<TaskList>(new TaskList(tasksFile));

            // Starting scheduler if we should.
            if (autoStart)
                Running = true;

            // Starting all task timers.
            _tasks.Read((tasks) =>
            {
                foreach (var idx in tasks.List())
                {
                    idx.EnsureTimer(async (x) => await ExecuteTask(x));
                }
            });
        }

        /// <summary>
        /// Returns true if scheduler is running.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Starts your scheduler. You must invoke this method in order to
        /// start your scheduler.
        /// </summary>
        public void Start()
        {
            Running = true;
        }

        /// <summary>
        /// Stops your scheduler, such that no more tasks will be evaluated.
        /// </summary>
        public void Stop()
        {
            Running = false;
        }

        /// <summary>
        /// Creates a new task.
        ///
        /// Notice, will delete any previously created tasks with the same name.
        /// </summary>
        /// <param name="node">Node declaring your task.</param>
        public void AddTask(Node node)
        {
            /*
             *  Removing any other tasks with the same name before
             *  proceeding with add.
             */
            _tasks.Write((tasks) => tasks.AddTask(node));
        }

        /// <summary>
        /// Returns a previously created task to caller.
        /// </summary>
        /// <param name="name">Name of task you wish to retrieve.</param>
        /// <returns>A node representing your task.</returns>
        public Node GetTask(string name)
        {
            // Getting task with specified name.
            var task = _tasks.Read((tasks) => tasks.GetTask(name));

            // Checking if named task exists.
            if (task == null)
                return null;

            // Creating and returning our result.
            var result = new Node(task.Name, null, task.GetNode().Children.Select(x => x.Clone()));

            // Making sure we also return upcoming due date as [due] node.
            result.Add(new Node("due", task.Due));
            return result;
        }

        /// <summary>
        /// Deletes an existing task from your task manager.
        /// </summary>
        /// <param name="name">Name of task to delete.</param>
        public void DeleteTask(string name)
        {
            _tasks.Write((tasks) => tasks.DeleteTask(name));
        }

        /// <summary>
        /// Lists all tasks in task manager, in order of evaluation, such that
        /// the first task in queue will be the first task returned.
        /// </summary>
        /// <returns>All tasks listed in chronological order of evaluation.</returns>
        public IEnumerable<string> ListTasks()
        {
            return _tasks.Read((tasks) => tasks.List().Select(x => x.Name));
        }

        #region [ -- Interface implementations -- ]

        /// <summary>
        /// Disposes the TaskList.
        /// </summary>
        public void Dispose()
        {
            _tasks.Write((tasks) => tasks.Dispose());
        }

        #endregion

        #region [ -- Private helper methods -- ]

        async Task ExecuteTask(Job job)
        {
            if (!Running)
            {
                // Postponing into the future.
                job.EnsureTimer(async (x) => await ExecuteTask(x));
                return;
            }

            try
            {
                // Retrieving task and its lambda object, and evaluating it.
                var lambda = job.Lambda.Clone();
                var signaler = _services.GetService(typeof(ISignaler)) as ISignaler;
                await signaler.SignalAsync("wait.eval", lambda);
            }
            catch (Exception err)
            {
                // Making sure we log exception using preferred ILogger instance.
                var logger = _services.GetService(typeof(ILogger)) as ILogger;
                logger?.LogError(job.Name, err);
            }
            finally
            {
                if (job.Repeats)
                {
                    job.EnsureTimer(async (x) => await ExecuteTask(x));
                }
                else
                {
                    _tasks.Write((tasks) =>
                    {
                        tasks.DeleteTask(job.Name);
                    });
                }
            }
        }

        #endregion
    }
}
