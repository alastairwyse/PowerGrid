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
using PowerGrid.Core;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Defines methods to read and write grids from and to persistent storage.
    /// </summary>
    /// <typeparam name="TEntity">The type of data held in each item in the grid.</typeparam>
    /// <typeparam name="TOuterKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of the items in the grid.</typeparam>
    /// <typeparam name="TGridItem">The items in the grid (i.e. where each item includes the <see cref="IGridItemOuterKeyProperties">outer key properties</see>).</typeparam>
    /// <typeparam name="TGridItemPTO">The <see cref="IPersistenceTransferObject">persistence transfer object</see> equivalent of <see cref="TGridItem"/>.</typeparam>
    public interface IGridPersister<TEntity, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TEntity : IGridItem<TEntity>
        where TOuterKeyProperties : IGridItemOuterKeyProperties
        where TGridItem : TEntity, IGridItemOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridItemOuterKeyProperties, IGridItem<TGridItem>, IPersistenceTransferObject
    {
        // TODO: Previously had granular CRUD methods here to deal with individual grid items, but realized that the whole comparison and all resulting updates need to be done in a transaction.
        //   In the future might want to add more granular methods... e.g. just to upsert a collection of grid items, or to delete a collection of grid items.
        //   How to get a grid?  Parameters could be different depending on grid item.
        //   Also need methods to get grid details... should that be a aeparate iterface?
        // TODO: Get methods will return PTO type (if/shen defined)

        /// <summary>
        /// Writes the specified grid to persistent storage.
        /// </summary>
        /// <param name="outerKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of all items in parameter <paramref name="gridItems"/>.</param>
        /// <param name="items">The items to persist.</param>
        /// <returns>A tuple containing: the version number of the written grid, and statistics containing counts of the items persisted.</returns>
        public (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(TOuterKeyProperties outerKeyProperties, IList<TEntity> items);

        /// <summary>
        /// Retrieve the grid with the specified properties.
        /// </summary>
        /// <param name="gridKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of the grid to retrieve.</param>
        /// <param name="version">The version number of the grid to retrieve.</param>
        /// <returns>The items in the grid.</returns>
        public IEnumerable<TGridItemPTO> GetGrid(TOuterKeyProperties gridKeyProperties, Int32 version);

        // TODO:
        //   GetGrids() for IGridCommonKeyProperties
        //   and for OuterKeyproperties (returns a set of versions)
    }
}
