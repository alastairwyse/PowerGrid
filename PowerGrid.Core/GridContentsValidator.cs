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
    /// Validates items in a grid. 
    /// </summary>
    /// <typeparam name="T">The type of data stored in the grid.</typeparam>
    public class GridContentsValidator<T> where T : IKeyPropertyComparable<T>, IValuePropertyEquatable<T>
    {
        /// <summary>
        /// Validates each of the items in a grid.
        /// </summary>
        /// <param name="gridContents">The items in the grid.</param>
        /// <param name="validationAction">An <see cref="Action{T}"/> used to validate each item.  Accepts an item as input, and should succeed if the item is valid, and throw an exception if the item in invalid (with the exception detailing the reason for validation failure).</param>
        /// <returns>The validated grid items.</returns>
        public IEnumerable<T> ValidateItems(IEnumerable<T> gridContents, Action<T> validationAction)
        {
            foreach (T currentItem in gridContents)
            {
                try
                {
                    validationAction(currentItem);
                }
                catch (Exception e)
                {
                    throw new GridContentsValidationException<T>("Failed to validate item in grid", currentItem, e);
                }

                yield return currentItem;
            }
        }
    }
}
