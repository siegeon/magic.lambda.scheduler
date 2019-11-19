/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;

namespace magic.lambda.scheduler.utilities
{
    /// <summary>
    /// Interface necessary to provide in order to log errors occurring
    /// during evaluation of tasks.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Invoked when an error occurs during evaluation of task.
        /// </summary>
        /// <param name="taskName">Nameof task that created the error.</param>
        /// <param name="err">Exception that occurred.</param>
        void LogError(string taskName, Exception err);
    }
}
