/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;

namespace magic.lambda.scheduler
{
    /// <summary>
    /// Initialize class, that helps initialize scheduler.
    /// </summary>
    public static class Initialize
    {
        /// <summary>
        /// Invoke with the root path you intend to use for maintaining your scheduled tasks.
        /// </summary>
        /// <param name="rootPath">The root path for your scheduled tasks.</param>
        public static void Init(string rootPath)
        {
            // Sanitizing folder path.
            rootPath = rootPath.Replace("\\", "/").Replace("//", "/").TrimEnd('/') + "/";

            // Sanity checking invocation.
            if (!Directory.Exists(rootPath))
                throw new ArgumentException($"The '{rootPath}' is not a folder.");

            // Ensuring "/tasks/" folder exists within root.
            var tasksFolder = Path.Combine(rootPath, "tasks/");
            if (!Directory.Exists(tasksFolder))
                Directory.CreateDirectory(tasksFolder);

            // Ensuring tasks Hyperlambda file exists in root folder.
            var tasksFile = Path.Combine(rootPath, "tasks.hl");
            if (!File.Exists(tasksFile))
            {
                using (var stream = File.CreateText(tasksFile))
                {
                    stream.Write(@"/*
 * You have no tasks.
 * When you do, they can be found in this file
 */");
                }
            }

            // Setting properties for tasks scheduler.
            TasksFolder = tasksFolder;
            TasksFile = tasksFile;
        }

        #region [ -- Private methods and helpers -- ]

        public static string TasksFolder { get; private set; }

        public static string TasksFile { get; private set; }

        #endregion
    }
}
