/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading;

namespace magic.lambda.scheduler.utilities
{
    // TODO: Merge with similar class in other projects.
    internal class Synchronizer<T>
    {
        readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        readonly T _shared;

        public Synchronizer(T shared)
        {
            _shared = shared;
        }

        public void Read(Action<T> functor)
        {
            _lock.EnterReadLock();
            try
            {
                functor(_shared);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public T2 Get<T2>(Func<T, T2> functor)
        {
            _lock.EnterReadLock();
            try
            {
                return functor(_shared);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Write(Action<T> functor)
        {
            _lock.EnterWriteLock();
            try
            {
                functor(_shared);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
