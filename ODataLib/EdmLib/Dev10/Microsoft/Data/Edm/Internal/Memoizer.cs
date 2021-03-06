//   Copyright 2011 Microsoft Corporation
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

#if !SILVERLIGHT
namespace Microsoft.Data.Edm.Internal
{
    /// <summary>
    /// Remembers the result of evaluating an expensive function so that subsequent
    /// evaluations are faster. Thread-safe.
    /// </summary>
    /// <typeparam name="TArg">Type of the argument to the function.</typeparam>
    /// <typeparam name="TResult">Type of the function result.</typeparam>
    internal sealed class Memoizer<TArg, TResult>
    {
        private readonly Func<TArg, TResult> function;
        private readonly Dictionary<TArg, Result> resultCache;
        private readonly ReaderWriterLockSlim slimLock;

        /// <summary>
        /// Constructs the memoizer.
        /// </summary>
        /// <param name="function">Required. Function whose values are being cached.</param>
        /// <param name="argComparer">Optional. Comparer used to determine if two functions arguments are the same.</param>
        internal Memoizer(Func<TArg, TResult> function, IEqualityComparer<TArg> argComparer)
        {
            Debug.Assert(function != null, "function != null");

            this.function = function;
            this.resultCache = new Dictionary<TArg, Result>(argComparer);
            this.slimLock = new ReaderWriterLockSlim();
        }

        /// <summary>
        /// Evaluates the wrapped function for the given argument. If the function has already
        /// been evaluated for the given argument, returns cached value. Otherwise, the value
        /// is computed and returned.
        /// </summary>
        /// <param name="arg">Function argument.</param>
        /// <returns>Function result.</returns>
        internal TResult Evaluate(TArg arg)
        {
            Result result;
            bool hasResult;

            // check to see if a result has already been computed
            this.slimLock.EnterReadLock();
            try
            {
                hasResult = this.resultCache.TryGetValue(arg, out result);
            }
            finally
            {
                this.slimLock.ExitReadLock();
            }

            if (!hasResult)
            {
                // compute the new value
                this.slimLock.EnterWriteLock();
                try
                {
                    // see if the value has been computed in the interim
                    if (!this.resultCache.TryGetValue(arg, out result))
                    {
                        result = new Result(() => this.function(arg));
                        this.resultCache.Add(arg, result);
                    }
                }
                finally
                {
                    this.slimLock.ExitWriteLock();
                }
            }

            // note: you need to release the global cache lock before (potentially) acquiring
            // a result lock in result.GetValue()
            return result.GetValue();
        }

        /// <summary>
        /// Encapsulates a 'deferred' result. The result is constructed with a delegate (must not 
        /// be null) and when the user requests a value the delegate is invoked and stored.
        /// </summary>
        private class Result
        {
            private TResult value;
            private Func<TResult> createValueDelegate;

            internal Result(Func<TResult> createValueDelegate)
            {
                Debug.Assert(null != createValueDelegate, "delegate must be given");
                this.createValueDelegate = createValueDelegate;
            }

            internal TResult GetValue()
            {
                if (null == this.createValueDelegate)
                {
                    // if the delegate has been cleared, it means we have already computed the value
                    return this.value;
                }

                // lock the entry while computing the value so that two threads
                // don't simultaneously do the work
                lock (this)
                {
                    if (null == this.createValueDelegate)
                    {
                        // between our initial check and our acquisition of the lock, some other
                        // thread may have computed the value
                        return this.value;
                    }

                    this.value = this.createValueDelegate();

                    // ensure createValueDelegate (and its closure) is garbage collected, and set to null
                    // to indicate that the value has been computed
                    this.createValueDelegate = null;
                    return this.value;
                }
            }
        }
    }
}
#endif
