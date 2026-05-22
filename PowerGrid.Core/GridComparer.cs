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
    /// Analyzes and outputs the differences between the sorted contents of two grids (one containing existing data, and one containing new data).
    /// </summary>
    /// <typeparam name="TExisting">The type of data stored in the existing grid.</typeparam>
    /// <typeparam name="TNew">The type of data stored in the new grid.</typeparam>
    /// <remarks>The reason for the distinction between <typeparamref name="TExisting"/> and <typeparamref name="TNew"/> is to allow <typeparamref name="TExisting"/> to contain additional properties (e.g. properties specific to persistence like database unique ids), and still be emitted as their original type during the comparison process.</remarks>
    public class GridComparer<TExisting, TNew> where TExisting : TNew, IGridItem<TNew> where TNew : IGridItem<TNew>
    {
        /// <summary>An <see cref="IEmitter{T}"/> instance to which items added to the existing grid are outputted during the comparison process.</summary>
        protected IEmitter<TNew> addedItemsEmitter;
        /// <summary>An <see cref="IEmitter{T}"/> instance to which items updated in the existing grid are outputted during the comparison process (the existing/superseded item, and the item which replaces it).</summary>
        protected IEmitter<Tuple<TExisting, TNew>> updatedItemsEmitter;
        /// <summary>An <see cref="IEmitter{T}"/> instance to which items deleted from the existing grid are outputted during the comparison process.</summary>
        protected IEmitter<TExisting> deletedItemsEmitter;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Core.GridComparer class.
        /// </summary>
        /// <param name="addedItemsEmitter">An <see cref="IEmitter{T}"/> instance to which items added to the existing grid are outputted during the comparison process.</param>
        /// <param name="updatedItemsEmitter">An <see cref="IEmitter{T}"/> instance to which items updated in the existing grid are outputted during the comparison process (the existing/superseded item, and the item which replaces it).</param>
        /// <param name="deletedItemsEmitter">An <see cref="IEmitter{T}"/> instance to which items deleted from the existing grid are outputted during the comparison process.</param>
        public GridComparer(IEmitter<TNew> addedItemsEmitter, IEmitter<Tuple<TExisting, TNew>> updatedItemsEmitter, IEmitter<TExisting> deletedItemsEmitter)
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
        public GridComparisonStatistics Compare(IEnumerable<TExisting> existingGridContents, IEnumerable<TNew> newGridContents)
        {
            Int32 itemsAddedCount = 0, itemsUpdatedCount = 0, itemsDeletedCount = 0;
            IEnumerator<TExisting> existingEnumerator = existingGridContents.GetEnumerator();
            IEnumerator<TNew> newEnumerator = newGridContents.GetEnumerator();
            Boolean existingEnumeratorMoveNextResult = existingEnumerator.MoveNext();
            Boolean newEnumeratorMoveNextResult = newEnumerator.MoveNext();

            while (existingEnumeratorMoveNextResult == true || newEnumeratorMoveNextResult == true)
            {
                TExisting existingItem = existingEnumerator.Current;
                TNew newItem = newEnumerator.Current;

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
                            updatedItemsEmitter.Emit(Tuple.Create(existingItem, newItem));
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
                    itemsDeletedCount++;
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
