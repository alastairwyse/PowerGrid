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

using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using System;
using System.Collections.Generic;

namespace PowerGrid.Persistence
{
    public abstract class PersisterBase<TEntity, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TOuterKeyProperties : IGridItemOuterKeyProperties
        where TGridItem : IGridItemOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridItemOuterKeyProperties, IGridItem<TGridItem>, IPersistenceTransferObject
    {
        public abstract GridComparisonStatistics PersistGrid(TOuterKeyProperties outerKeyProperties, IList<StockPrice> gridItems);

        protected abstract IEnumerable<StockPricePTO> GetExistingGrid(SqlConnection connection, String dataSource, DateOnly date, DateTime transactionTimestamp)
    }
}
