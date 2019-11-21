/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading;

namespace magic.lambda.scheduler.utilities
{
    internal static class SynchronizeScheduler
    {
        readonly static ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

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
    }
}
