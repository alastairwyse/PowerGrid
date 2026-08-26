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
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using PowerGrid.Core;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Manages locks to allow concurrent persistence of grids.
    /// </summary>
    public class PersistenceConcurrencyManager : IPersistenceConcurrencyManager, IDisposable
    {
        /// <summary>Maps <see cref="IGridLockKey"/> implementations to <see cref="ReaderWriterLockSlim"/> instances which are used to apply mutual exclusion locks to common key properties of grids.</summary>
        protected ConcurrentDictionary<IGridLockKey, ReaderWriterLockSlim> commonKeyPropertiesLockDictionary;
        /// <summary>Maps <see cref="IGridLockKey"/> implementations to lock objects which are used to apply mutual exclusion locks to outer key properties of grids.</summary>
        protected ConcurrentDictionary<IGridLockKey, Object> outerKeyPropertiesLockDictionary;
        /// <summary>Indicates whether the object has been disposed.</summary>
        protected Boolean disposed;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.PersistenceConcurrencyManager class.
        /// </summary>
        public PersistenceConcurrencyManager()
        {
            commonKeyPropertiesLockDictionary = new ConcurrentDictionary<IGridLockKey, ReaderWriterLockSlim>();
            outerKeyPropertiesLockDictionary = new ConcurrentDictionary<IGridLockKey, Object>();
            disposed = false;
        }
        
        /// <inheritdoc/>
        public void AcquireLockAndInvokeAction<TGridCommonKeyProperties>(GridCommonKeyPropertiesLockKeyBase<TGridCommonKeyProperties> commonKeyPropertiesLock, Action action)
            where TGridCommonKeyProperties : IGridCommonKeyProperties
        {
            ReaderWriterLockSlim commonKeyPropertiesLockObject = commonKeyPropertiesLockDictionary.GetOrAdd(commonKeyPropertiesLock, new ReaderWriterLockSlim());
            try
            {
                commonKeyPropertiesLockObject.EnterWriteLock();
                action();
            }
            finally
            {
                commonKeyPropertiesLockObject.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public void AcquireLockAndInvokeAction<TGridCommonKeyProperties, TGridOuterKeyProperties>
        (
            GridCommonKeyPropertiesLockKeyBase<TGridCommonKeyProperties> commonKeyPropertiesLock, 
            GridOuterKeyPropertiesLockKeyBase<TGridOuterKeyProperties> outerKeyPropertiesLock, 
            Action action
        )   where TGridCommonKeyProperties : IGridCommonKeyProperties
            where TGridOuterKeyProperties : IGridOuterKeyProperties
        {
            ReaderWriterLockSlim commonKeyPropertiesLockObject = commonKeyPropertiesLockDictionary.GetOrAdd(commonKeyPropertiesLock, new ReaderWriterLockSlim());
            try
            {
                commonKeyPropertiesLockObject.EnterReadLock();
                Object outerKeyPropertiesLockObject = outerKeyPropertiesLockDictionary.GetOrAdd(outerKeyPropertiesLock, new Object());
                lock (outerKeyPropertiesLockObject)
                {
                    action();
                }
            }
            finally
            {
                commonKeyPropertiesLockObject.ExitReadLock();
            }
        }

        #region Finalize / Dispose Methods

        /// <summary>
        /// Releases the unmanaged resources used by the PersistenceConcurrencyManager.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #pragma warning disable 1591

        ~PersistenceConcurrencyManager()
        {
            Dispose(false);
        }

        #pragma warning restore 1591

        /// <summary>
        /// Provides a method to free unmanaged resources used by this class.
        /// </summary>
        /// <param name="disposing">Whether the method is being called as part of an explicit Dispose routine, and hence whether managed resources should also be freed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                    foreach (ReaderWriterLockSlim currentLockObject in commonKeyPropertiesLockDictionary.Values)
                    {
                        currentLockObject.Dispose();
                    }
                }
                // Free your own state (unmanaged objects).

                // Set large fields to null.

                disposed = true;
            }
        }

        #endregion
    }
}
