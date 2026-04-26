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
    /// <typeparam name="TGridItemPTO">The type of items stored in grids in persistent storage.</typeparam>
    /// <typeparam name="TGridItem">The type of items in the new grids to be written to persistent storage.</typeparam>
    public interface IGridPersister<TGridItemPTO, TGridItem> where TGridItemPTO : TGridItem, IGridItem<TGridItem> where TGridItem : IGridItem<TGridItem>
    {
        // TODO: Previously had granular CRUD methods here to deal with individual grid items, but realized that the whole comparison and all resulting updates need to be done in a transaction.
        //   In the future might want to add more granular methods... e.g. just to upsert a collection of grid items, or to delete a collection of grid items.
        //   How to get a grid?  Parameters could be different depending on grid item.
        //   Also need methods to get grid details... should that be a aeparate iterface?
        // TODO: Get methods will return PTO type (if/shen defined)

        /// <summary>
        /// Writes the specified grid to persistent storage.
        /// </summary>
        /// <param name="gridItems">The items in the grid.</param>
        GridComparisonStatistics PersistGrid(IList<TGridItem> gridItems);
    }
}
