/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;

namespace magic.lambda.scheduler.contracts
{
    /// <summary>
    /// Common interface for repetition patterns.
    /// </summary>
    public interface ITaskPattern
    {
        /// <summary>
        /// Calculates the next date and time for when the task is to be executed.
        /// </summary>
        /// <returns>Date and time when task should be executed.</returns>
        DateTime Next();

        /// <summary>
        /// Returns the string representation of the repetition pattern.
        /// 
        /// This is the patter we persist as the repetition pattern for the task when a task is persisted.
        /// </summary>
        /// <value>String representation for repetition pattern.</value>
        string Value { get; }
    }
}
