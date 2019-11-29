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
     * TODO : Merge into utility library.
     */
    internal class Synchronizer<T>
    {
        readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        readonly T _shared;

        public Synchronizer(T shared)
        {
            _shared = shared;
        }

        /*
         * Acquires a read lock and executes the specified function.
         */
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

        /*
         * Acquires a read lock, executes the specified function, and returns its result to caller.
         */
        public T2 Read<T2>(Func<T, T2> functor)
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

        /*
         * Acquires a write lock, and invokes the specified Action.
         */
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

        /*
         * Acquires a write lock, and invokes the specified Action.
         */
        public T2 ReadWrite<T2>(Func<T, T2> functor)
        {
            _lock.EnterWriteLock();
            try
            {
                return functor(_shared);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
