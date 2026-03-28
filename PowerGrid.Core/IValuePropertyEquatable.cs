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
    /// Defines methods which allow determining the equality of the value properties of objects.
    /// </summary>
    /// <typeparam name="T">The type of objects to be compared.</typeparam>
    /// <remarks>Value properties refer to a set of properties of the object which do not uniquely identify the object, but hold its data.</remarks>
    public interface IValuePropertyEquatable<T>
    {
        /// <summary>
        /// Indicates whether the value properties of the current object instance are equal to the value properties of another.
        /// </summary>
        /// <param name="other">The object instance to compare with the current.</param>
        /// <returns>True if the value properties of the current object instance are equal to the value properties of the other, otherwise false.</returns>
        Boolean ValuePropertiesEqual(T other);
    }
}
