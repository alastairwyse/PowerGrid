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
    /// Base for classes which implement <see cref="IGridLockKey"/>.
    /// </summary>
    public abstract class GridLockKeyBase<T> : IGridLockKey where T : IKeyPropertyComparable<T>, IValuePropertyEquatable<T>
    {
        /// <summary>The type of the object that this class creates a mutual-exclusion lock for.</summary>
        protected abstract Type UnderlyingObjectType
        { 
            get; 
        }

        /// <summary>The values of the <see cref="IKeyPropertyComparable{T}">key properties</see> of the object that this class creates a mutual-exclusion lock for.</summary>
        protected abstract Object[] UnderlyingObjectKeyPropertyValues
        {
            get;
        }

        /// <summary>The complete set of <see cref="IKeyPropertyComparable{T}">key properties</see> used to create the mutual-exclusion lock.</summary>
        public Object[] KeyPropertyValues
        {
            get
            {
                return [UnderlyingObjectType, .. UnderlyingObjectKeyPropertyValues];
            }
        }

        /// <inheritdoc/>
        public Boolean Equals(IGridLockKey other)
        {
            if (this.KeyPropertyValues.Length != other.KeyPropertyValues.Length)
            {
                return false;
            }
            else
            {
                for (Int32 i = 0; i < this.KeyPropertyValues.Length; i++)
                {
                    if (this.KeyPropertyValues[i] != other.KeyPropertyValues[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <inheritdoc/>
        public override Int32 GetHashCode()
        {
            return HashCode.Combine(KeyPropertyValues);
        }
     }
}
