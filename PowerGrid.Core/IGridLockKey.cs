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
    /// Defines properties which enable the creation of mutual-exclusion locks, which are applied to a persistent store of grids so that the grids can be safely modified concurrently.
    /// </summary>
    public interface IGridLockKey : IEquatable<IGridLockKey>
    {
        /// <summary>The values of the <see cref="IKeyPropertyComparable{T}">key properties</see> of the object that this interface creates mutual-exclusion locks for.</summary>
        public abstract Object[] KeyPropertyValues
        {
            get;
        }
    }
}
