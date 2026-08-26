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
using PowerGrid.Core;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Defines method to manage locks to allow concurrent persistence of grids.
    /// </summary>
    public interface IPersistenceConcurrencyManager
    {
        /// <summary>
        /// Acquires an exclusive lock on the grids with the specified common key properties, and invokes the specified action.
        /// </summary>
        /// <typeparam name="TGridCommonKeyProperties">The type of the common key properties.</typeparam>
        /// <param name="commonKeyPropertiesLock">The common key properties of the grid to obtain an exclusive lock for.</param>
        /// <param name="action">The action to invoke.</param>
        void AcquireLockAndInvokeAction<TGridCommonKeyProperties>(GridCommonKeyPropertiesLockKeyBase<TGridCommonKeyProperties> commonKeyPropertiesLock, Action action)
            where TGridCommonKeyProperties : IGridCommonKeyProperties;

        /// <summary>
        /// Acquires an exclusive lock on the grid with the specified outer key properties, and invokes the specified action.
        /// </summary>
        /// <typeparam name="TGridCommonKeyProperties">The type of the common key properties.</typeparam>
        /// <typeparam name="TGridOuterKeyProperties">The type of the outer key properties.</typeparam>
        /// <param name="commonKeyPropertiesLock">The common key properties of the grid to obtain an exclusive lock for.</param>
        /// <param name="outerKeyPropertiesLock">The outer key properties of the grid to obtain an exclusive lock for.</param>
        /// <param name="action">The action to invoke.</param>
        public void AcquireLockAndInvokeAction<TGridCommonKeyProperties, TGridOuterKeyProperties>
        (
            GridCommonKeyPropertiesLockKeyBase<TGridCommonKeyProperties> commonKeyPropertiesLock,
            GridOuterKeyPropertiesLockKeyBase<TGridOuterKeyProperties> outerKeyPropertiesLock,
            Action action
        )   where TGridCommonKeyProperties : IGridCommonKeyProperties
            where TGridOuterKeyProperties : IGridOuterKeyProperties;
    }
}
