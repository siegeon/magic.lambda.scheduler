/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading;

namespace magic.lambda.scheduler.utilities
{
    /*
     * Helper class to synchronize access to the shared scheduler instance.
     */
    internal static class SynchronizeScheduler
    {
        readonly static ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /*
         * Acquires a read lock and invokes the specified Action.
         */
        public static void Read(Action functor)
        {
            _lock.EnterReadLock();
            try
            {
                functor();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /*
         * Acquires a read lock, executes the specified function, and returns its result to caller.
         */
        public static T Get<T>(Func<T> functor)
        {
            _lock.EnterReadLock();
            try
            {
                return functor();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /*
         * Acquires a write lock, and invokes the specified Action.
         */
        public static void Write(Action functor)
        {
            _lock.EnterWriteLock();
            try
            {
                functor();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /*
         * Acquires a write lock, and invokes the specified Action.
         */
        public static T WriteGet<T>(Func<T> functor)
        {
            _lock.EnterWriteLock();
            try
            {
                return functor();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
