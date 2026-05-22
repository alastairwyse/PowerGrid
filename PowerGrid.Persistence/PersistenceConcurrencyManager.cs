/*
 * Copyright 2026 Alastair Wyse (https://github.com/alastairwyse/PowerGrid/)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Concurrent;
using PowerGrid.Core;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Manages locks to allow concurrent persistence of grids.
    /// </summary>
    public class PersistenceConcurrencyManager
    {
        /// <summary>Maps <see cref="IGridLockKey"/> implementations to lock objects for the sets of grid items the <see cref="IGridLockKey"/> implementation represents.</summary>
        protected ConcurrentDictionary<IGridLockKey, Object> lockDictionary;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.PersistenceConcurrencyManager class.
        /// </summary>
        public PersistenceConcurrencyManager()
        {
            lockDictionary = new ConcurrentDictionary<IGridLockKey, Object>();
        }

        /// <summary>
        /// Acquires an exclusive lock using the specified grid lock key, and invokes the specified action.
        /// </summary>
        /// <param name="gridLockKey">A key representing the set of grid items to obtain an exclusive lock for.</param>
        /// <param name="action">The action to invoke.</param>
        public void AcquireLockAndInvokeAction(IGridLockKey gridLockKey, Action action)
        {
            Object lockObject = lockDictionary.GetOrAdd(gridLockKey, new Object());
            lock (lockObject)
            {
                action();
            }
        }
    }
}
