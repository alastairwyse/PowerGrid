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
    /// Defines methods which allow comparing objects by their key properties.
    /// </summary>
    /// <typeparam name="T">The type of objects to be compared.</typeparam>
    /// <remarks>Key properties refer to a set of properties of the object which uniquely identify the object.</remarks>
    public interface IKeyPropertyComparable<T> 
    {
        /// <summary>
        /// Compares the key properties of this object instance with the key properties of another and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="other">The object instance to compare with the current.</param>
        /// <returns>A value that indicates the relative order of the objects being compared.</returns>
        /// <remarks>The values and meanings of the returned integer match those of the <see cref="IComparable{T}.CompareTo(T?)"/> method.</remarks>
        Int32 KeyCompareTo(T other);
    }
}
