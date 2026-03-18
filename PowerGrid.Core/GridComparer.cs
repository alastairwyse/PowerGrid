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
    /// Analyzes and outputs the differences between the sorted contents of two grids.
    /// </summary>
    /// <typeparam name="T">The type of data stored in the grid.</typeparam>
    public class GridComparer<T>
        where T : IKeyPropertyComparable<T>, IValuePropertyComparable<T>
    {
        /// <summary>An <see cref="IEmitter{T}"/> instance to which items added to the existing grid are outputted during the comparison process.</summary>
        protected IEmitter<T> addedItemsEmitter;
        /// <summary>An <see cref="IEmitter{T}"/> instance to which items updated in the existing grid are outputted during the comparison process.</summary>
        protected IEmitter<T> updatedItemsEmitter;
        /// <summary>An <see cref="IEmitter{T}"/> instance to which items deleted from the existing grid are outputted during the comparison process.</summary>
        protected IEmitter<T> deletedItemsEmitter;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Core.GridComparer class.
        /// </summary>
        /// <param name="addedItemsEmitter">An <see cref="IEmitter{T}"/> instance to which items added to the existing grid are outputted during the comparison process.</param>
        /// <param name="updatedItemsEmitter">An <see cref="IEmitter{T}"/> instance to which items updated in the existing grid are outputted during the comparison process.</param>
        /// <param name="deletedItemsEmitter">An <see cref="IEmitter{T}"/> instance to which items deleted from the existing grid are outputted during the comparison process.</param>
        public GridComparer(IEmitter<T> addedItemsEmitter, IEmitter<T> updatedItemsEmitter, IEmitter<T> deletedItemsEmitter)
        {
            this.addedItemsEmitter = addedItemsEmitter;
            this.updatedItemsEmitter = updatedItemsEmitter;
            this.deletedItemsEmitter = deletedItemsEmitter;
        }

        /// <summary>
        /// Compares the sorted contents (based on <see cref="IKeyPropertyComparable{T}"/>) of an existing grid and a new grid, emitting/outputing the items that would need to be changed to make the existing grid match the new grid.
        /// </summary>
        /// <param name="existingGridContents">The items in the existing grid.</param>
        /// <param name="newGridContents">The items in the new grid.</param>
        /// <returns>Statistics containing counts of the items emitted.</returns>
        public GridComparisonStatistics Compare(IEnumerable<T> existingGridContents, IEnumerable<T> newGridContents)
        {
            IEnumerator<T> existingEnumerator = existingGridContents.GetEnumerator();
            IEnumerator<T> newEnumerator = newGridContents.GetEnumerator();

            // https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerator-1?view=net-10.0
            // Need to experiment with IEnumerator... see what 'Current contains' when already consumed
        }
    }
}
