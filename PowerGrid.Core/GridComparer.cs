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
        where T : IKeyPropertyComparable<T>, IValuePropertyEquatable<T>
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
            Int32 itemsAddedCount = 0, itemsUpdatedCount = 0, itemsDeletedCount = 0;
            IEnumerator<T> existingEnumerator = existingGridContents.GetEnumerator();
            IEnumerator<T> newEnumerator = newGridContents.GetEnumerator();
            existingEnumerator.Reset();
            newEnumerator.Reset();
            Boolean existingEnumeratorMoveNextResult = existingEnumerator.MoveNext();
            Boolean newEnumeratorMoveNextResult = newEnumerator.MoveNext();

            while (existingEnumeratorMoveNextResult == true || newEnumeratorMoveNextResult == true)
            {
                T existingItem = existingEnumerator.Current;
                T newItem = newEnumerator.Current;

                if (existingEnumeratorMoveNextResult == true && newEnumeratorMoveNextResult == true)
                {
                    Int32 keyComparisonResult = existingItem.KeyCompareTo(newItem);
                    if (keyComparisonResult == 0)
                    {
                        Boolean valueComparisonResult = existingItem.ValuePropertiesEqual(newItem);
                        if (valueComparisonResult == true)
                        {
                            // The exsting and new items full match, so no need to change/update
                        }
                        else
                        {
                            updatedItemsEmitter.Emit(newItem);
                            itemsUpdatedCount++;
                        }
                        existingEnumeratorMoveNextResult = existingEnumerator.MoveNext();
                        newEnumeratorMoveNextResult = newEnumerator.MoveNext();
                    }
                    else if (keyComparisonResult > 0)
                    {
                        // The existing item follows the new item
                        addedItemsEmitter.Emit(newItem);
                        itemsAddedCount++;
                        newEnumeratorMoveNextResult = newEnumerator.MoveNext();
                    }
                    else
                    {
                        // The existing item preceeds the new item
                        deletedItemsEmitter.Emit(existingItem);
                        itemsDeletedCount++;
                        existingEnumeratorMoveNextResult = existingEnumerator.MoveNext();
                    }
                }
                else if (existingEnumeratorMoveNextResult == true)
                {
                    deletedItemsEmitter.Emit(existingItem);
                    itemsAddedCount++;
                    existingEnumeratorMoveNextResult = existingEnumerator.MoveNext();
                }
                else
                {
                    addedItemsEmitter.Emit(newItem);
                    itemsAddedCount++;
                    newEnumeratorMoveNextResult = newEnumerator.MoveNext();
                }
            }

            return new GridComparisonStatistics(itemsAddedCount, itemsUpdatedCount, itemsDeletedCount);
        }
    }
}
