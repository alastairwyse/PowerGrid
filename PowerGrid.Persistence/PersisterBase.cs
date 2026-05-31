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
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Base for classes which write and read grids to and from persistent storage.
    /// </summary>
    /// <typeparam name="TEntity">The type of data held in each item in the grid.</typeparam>
    /// <typeparam name="TOuterKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of the items in the grid.</typeparam>
    /// <typeparam name="TGridItem">The items in the grid (i.e. where each item includes the <see cref="IGridItemOuterKeyProperties">outer key properties</see>).</typeparam>
    /// <typeparam name="TGridItemPTO">The <see cref="IPersistenceTransferObject">persistence transfer object</see> equivalent of <see cref="TGridItem"/>.</typeparam>
    public abstract class PersisterBase<TEntity, TOuterKeyProperties, TGridItem, TGridItemPTO> : IGridPersister<TEntity, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TEntity : IGridItem<TEntity>
        where TOuterKeyProperties : IGridItemOuterKeyProperties
        where TGridItem : TEntity, IGridItemOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridItemOuterKeyProperties, IGridItem<TGridItem>, IPersistenceTransferObject
    {
        /// <inheritdoc/>
        public abstract (Int32, GridComparisonStatistics) PersistGrid(TOuterKeyProperties outerKeyProperties, IList<TEntity> items);

        /// <inheritdoc/>
        public abstract IEnumerable<TGridItemPTO> GetGrid(TOuterKeyProperties gridKeyProperties, Int32 version);
    }
}
