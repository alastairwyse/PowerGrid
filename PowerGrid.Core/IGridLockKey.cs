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

namespace PowerGrid.Core
{
    /// <summary>
    /// Defines properties of a class, instances of which can be used as keys in a <see cref="Dictionary{TKey, TValue}"/> where the values in the dictionary are mutual-exclusion lock objects, which can be used to apply locks to safely modify a persistent store of grids concurrently.
    /// </summary>
    /// <remarks>The properties defined in the interface should be implemented in conjunction with implementations of <see cref="IEquatable{T}"/> and <see cref="Object.GetHashCode">GetHashCode()</see> to allow implementations of this interface to act as keys in a <see cref="Dictionary{TKey, TValue}"/>.</remarks>
    public interface IGridLockKey : IEquatable<IGridLockKey>
    {
        /// <summary>The values of the <see cref="IKeyPropertyComparable{T}">key properties</see> of the grid item to apply locks for.</summary>
        public abstract Object[] KeyPropertyValues { get; }
    }
}
