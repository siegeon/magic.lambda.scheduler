/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using magic.node;
using magic.node.extensions;
using magic.lambda.scheduler.utilities.jobs;

namespace magic.lambda.scheduler.utilities
{
    /*
     * Class wrapping a single task, with its repetition pattern, or due date,
     * and its associated lambda object to be evaluated when task is to be evaluated.
     */
    internal abstract class Job : IComparable, IDisposable
    {
        protected Timer _timer;

        protected Job(
            string name, 
            string description, 
            Node lambda)
        {
            Name = name;
            Description = description;
            Lambda = lambda.Clone();
        }

        /*
         * Name of task.
         */
        public string Name { get; private set; }

        /*
         * Description of task.
         */
        public string Description { get; private set; }

        /*
         * Actual lambda object, which should be evaluedt as task is evaluated.
         */
        public Node Lambda { get; private set; }

        /*
         * Calculated due date for the next time the task should be evaluated.
         */
        public DateTime Due { get; internal set; }

        /*
         * Returns true if this is a repetetive task, implying the task is declared
         * to be evaluated multiple times.
         */
        public abstract bool Repeats { get; }

        public static Job CreateJob(Node taskNode)
        {
            // Figuring out what type of job caller requests.
            var repetitionPattern = taskNode.Children.Where(x => x.Name == "repeat" || x.Name == "when");
            if (repetitionPattern.Count() != 1)
                throw new ArgumentException("A task must have exactly one [repeat] or [when] argument.");

            // Finding common arguments for job.
            var name = taskNode.GetEx<string>() ?? throw new ArgumentException("No name give to task");
            var description = taskNode.Children.FirstOrDefault(x => x.Name == "description")?.GetEx<string>();
            var lambda = taskNode.Children.FirstOrDefault(x => x.Name == ".lambda") ?? throw new ArgumentException($"No [.lambda] given to task named '{name}'");

            // Creating actual job instance.
            Job result;
            switch (repetitionPattern.First().Name)
            {
                case "repeat":

                    result = RepeatJob.CreateJob(
                        name,
                        description,
                        lambda,
                        repetitionPattern.First().GetEx<string>(),
                        taskNode);
                    break;

                case "when":

                    result = new WhenJob(
                        name,
                        description,
                        lambda,
                        repetitionPattern.First().GetEx<DateTime>());
                    break;

                default:
                    throw new ApplicationException("You have reached a place in your code which should have been impossible to reach!");
            }
            result.Due = result.CalculateNextDue();
            return result;
        }

        /*
         * Creates the Timer timeout, that invokes the specified Action at the time the task should be evaluated.
         */
        internal void EnsureTimer(Func<Job, Task> callback)
        {
            var now = DateTime.Now;
            var nextDue =
                Math.Max(
                    250,
                    Math.Min((Due - now).TotalMilliseconds, new TimeSpan(45, 0, 0, 0).TotalMilliseconds));
            _timer = new Timer(async (state) => await callback(this), null, (int)nextDue, Timeout.Infinite);
        }

        /*
         * Returns the node representation for this particular instance, such that
         * it can be serialized to disc, etc.
         */
        public abstract Node GetNode();

        /*
         * Calculates next due date.
         */
        internal abstract DateTime CalculateNextDue();

        #region [ -- Interface implementations -- ]

        /*
         * Necessary to make it possible to sort tasks according to their due dates,
         * such that tasks intended to be evaluated first, comes before other tasks in the
         * TaskList instance containing all tasks.
         */
        public int CompareTo(object obj)
        {
            if (obj is Job rhs)
                return Due.CompareTo(rhs.Due);
            throw new ArgumentException($"You tried to compare a Task to an object of type {obj?.GetType().Name ?? "???"}");
        }

        #endregion

        #region [ -- Private helper methods -- ]

        public void Dispose()
        {
            _timer?.Dispose();
        }

        #endregion
    }
}
