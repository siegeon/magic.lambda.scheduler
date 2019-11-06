/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Timers;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.scheduler.utilities
{
    /*
     * Class encapsulating a single task in the scheduler.
     */
    internal class Task
    {
        IServiceProvider _provider;
        Timer _timer;

        // Private CTOR to prevent creation from outside of static CTOR.
        private Task()
        { }

        /*
         * The actual name of the task, which is the same as its filename, 
         * minus path and extension.
         */
        public string Name { get; private set; }

        /*
         * Node wrapping actual task.
         */
        public Node Node { get; private set; }

        #region [ -- Private and internal helper methods -- ]

        /*
         * Static constructor, that creates and initializes task.
         */
        internal static Task Create(IServiceProvider provider, Node node)
        {
            // Retrieving name, and sanity checking.
            var name = node.GetEx<string>();
            SanityCheckTaskName(name);

            var taskNode = new Node
            {
                Name = name,
            };
            taskNode.AddRange(node.Children.Select(x => x.Clone()));

            // Creating a new task, and returning to caller.
            var task = new Task
            {
                Node = taskNode,
                Name = name,
                _provider = provider,
            };

            /*
             * Creating actual timer, and making sure it was a success
             * Notice, if not, it might be that execution date was in the past.
             */
            if (!task.CreateTimer())
                throw new ArgumentException($"Task's date was in the past, and hence no task could be created.");

            // Success, returning task to caller.
            return task;
        }

        /*
         * Static constructor, that creates and initializes task from its serialized version.
         * 
         * Notice, this expects a different node structure, since the node structure for serialized
         * tasks are slightly different the structure for tasks dynamically added using the task add slot.
         * 
         * Also, this on does not need to sanity check the names of tasks, etc.
         */
        internal static Task CreateFromSerialized(IServiceProvider provider, Node node)
        {
            // Retrieving name, and sanity checking.
            var task = new Task
            {
                Node = node.Clone(),
                Name = node.Get<string>(),
                _provider = provider
            };
            var success = task.CreateTimer();
            return success ? task : null;
        }

        /*
         * Creates the timer associated with a task.
         */
        bool CreateTimer()
        {
            // Creating timer, and associating it with lambda object's evaluation.
            var timer = new Timer();
            timer.Elapsed += async (sender, e) =>
            {
                try
                {
                    var signaler = _provider.GetService(typeof(ISignaler)) as ISignaler;
                    var lambda = Node.Children.FirstOrDefault(x => x.Name == ".lambda").Clone();
                    await signaler.SignalAsync("wait.eval", lambda);

                    // Checking if this is a one time task.
                    if (Node.Children.Any(x => x.Name == "when"))
                        Common.DeleteTask(Name); // Single invocation task.
                }
                catch (Exception)
                {
                    // TODO: Logging ..!!
                }
            };

            // Figuring out interval/execution date, etc.
            var when = Node.Children.FirstOrDefault(x => x.Name == "when");
            if (when != null)
            {
                var date = when.GetEx<DateTime>();
                if (date < DateTime.Now)
                    return false;
                var span = (date - DateTime.Now).TotalMilliseconds;
                timer.Interval = span;
                timer.AutoReset = false;
                _timer = timer;
            }
            var repeat = Node.Children.FirstOrDefault(x => x.Name == "repeat");
            if (repeat != null)
            {
                var interval = repeat.Children.FirstOrDefault();
                if (interval == null)
                    return false;
                var val = interval.GetEx<long>();
                switch (interval.Name)
                {
                    case "second":
                        timer.Interval = val * 1000;
                        break;
                    case "minute":
                        timer.Interval = val * 60000;
                        break;
                    case "hour":
                        timer.Interval = val * 3600000;
                        break;
                    default:
                        return false;
                }
                timer.AutoReset = true;
                _timer = timer;
            }

            // Starting timer and returning it to caller.
            if (_timer != null)
            {
                _timer.Enabled = true;
            }
            return _timer != null;
        }


        /*
         * Stops the specified task's timer.
         */
        internal void Stop()
        {
            _timer.Stop();
        }
        /*
         * Sanity checks name of task, since it needs to be serialized to disc.
         */
        static void SanityCheckTaskName(string taskName)
        {
            foreach (var idxChar in taskName)
            {
                if ("abcdefghijklmnopqrstuvwxyz_-1234567890.".IndexOf(idxChar) == -1)
                    throw new ArgumentException($"You can only use alphanumeric characters [a-z] and [0-1], in addition to '_' and '-' in task names. Taks {taskName} is not a legal taskname");
            }
        }

        #endregion
    }
}
