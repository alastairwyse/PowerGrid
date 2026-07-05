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

namespace PowerGrid.Core
{
    /// <summary>
    /// Base for classes which implement <see cref="IGridCommonKeyPropertiesLockKey"/>.
    /// </summary>
    /// <typeparam name="T">The type of grid common key properties the class creates a <see cref="Dictionary{TKey, TValue}"/> key for.</typeparam>
    public abstract class GridOuterKeyPropertiesLockKeyBase<T> : GridLockKeyBase<T>, IGridOuterKeyPropertiesLockKey where T : IGridOuterKeyProperties
    {
        /// <summary>
        /// Initialises a new instance of the PowerGrid.Core.GridOuterKeyPropertiesLockKeyBase class.
        /// </summary>
        /// <param name="outerKeyProperties">The outer key properties the class creates a <see cref="Dictionary{TKey, TValue}"/> key for.</param>
        public GridOuterKeyPropertiesLockKeyBase(T outerKeyProperties)
            : base(outerKeyProperties)
        {
        }
    }
}
