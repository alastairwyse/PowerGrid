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
    /// <typeparam name="T">The type of grid item the class creates a key for.</typeparam>
    public abstract class GridLockKeyBase<T> : IGridLockKey where T : IGridItem<T>
    {
        /// <summary>The grid item object to generate key for.</summary>
        protected T underlyingGridItem;

        /// <summary>The type of the object that this class creates a key for.</summary>
        protected Type underlyingGridItemType;

        /// <summary>The values of the <see cref="IKeyPropertyComparable{T}">key properties</see> of the object that this class creates a key for.</summary>
        protected abstract Object[] UnderlyingGridItemKeyPropertyValues
        {
            get;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Core.GridLockKeyBase class.
        /// </summary>
        /// <param name="underlyingGridItem">The type of grid item the class creates a key for.</param>
        public GridLockKeyBase(T underlyingGridItem)
        {
            this.underlyingGridItem = underlyingGridItem;
            this.underlyingGridItemType = underlyingGridItem.GetType();
        }

        /// <inheritdoc/>
        public Object[] KeyPropertyValues
        {
            get
            {
                return [underlyingGridItemType, .. UnderlyingGridItemKeyPropertyValues];
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
                    if (this.KeyPropertyValues[i].Equals(other.KeyPropertyValues[i]) == false)
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
            HashCode hashCode = new();
            foreach (Object currentKeyPropertyValue in KeyPropertyValues)
            {
                hashCode.Add(currentKeyPropertyValue);
            }

            return hashCode.ToHashCode();
        }
     }
}
