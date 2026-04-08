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
    /// Checks that no items exist in the sorted contents of a grid that have duplicate <see cref="IKeyPropertyComparable{T}">key properties</see>.
    /// </summary>
    /// <typeparam name="T">The type of data stored in the grid.</typeparam>
    public class GridContentsDuplicateChecker<T> where T : IKeyPropertyComparable<T>, IValuePropertyEquatable<T>
    {
        /// <summary>
        /// Checks that no items exist in a grid that have duplicate <see cref="IKeyPropertyComparable{T}">key properties</see>.
        /// </summary>
        /// <param name="gridContents">The sorted items in the grid.</param>
        /// <returns>The checked grid items.</returns>
        /// <exception cref="GridContentsDuplicateItemsException{T}">Duplicate items were found in the grid.</exception>
        public IEnumerable<T> CheckForDuplicates(IEnumerable<T> gridContents)
        {
            T lastItem = default(T);
            Boolean populatedLastItem = false;
            foreach (T currentItem in gridContents)
            {
                if (populatedLastItem == false)
                {
                    populatedLastItem = true;
                    lastItem = currentItem;

                    yield return currentItem;
                }
                else
                {
                    if (lastItem.KeyCompareTo(currentItem) == 0)
                    {
                        throw new GridContentsDuplicateItemsException<T>("Grid contains items with duplicate key values.", currentItem);
                    }
                    else
                    {
                        lastItem = currentItem;

                        yield return currentItem;
                    }
                }
            }
        }
    }
}
