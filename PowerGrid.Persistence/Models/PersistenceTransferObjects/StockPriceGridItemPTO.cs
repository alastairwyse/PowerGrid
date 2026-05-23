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
using System;

namespace PowerGrid.Persistence.Models.PersistenceTransferObjects
{
    /// <summary>
    /// Model/container class holding a <see cref="StockPriceGridItem"/> augmented with properties allowing it to be transferred to and form persistent storage.
    /// </summary>
    /// <param name="Id">A unique id for the object within persistent storage.</param>
    /// <param name="Tag">A tag used to classify the grid.</param>
    /// <param name="DataSource">The source/entity which provided the price.</param>
    /// <param name="Date">The date the price was quoted for.</param>
    /// <param name="Company">The company the price was quoted for.</param>
    /// <param name="Price">The price.</param>
    /// <param name="TransactionFrom">The date and time that the object became active.</param>
    /// <param name="TransactionTo">The date and time that the object was superseded or deleted.</param>
    public record StockPriceGridItemPTO
    (
        Int64 Id, 
        String Tag, 
        String DataSource, 
        DateOnly Date, 
        String Company, 
        Decimal Price, 
        DateTime TransactionFrom, 
        DateTime TransactionTo
    )
        : StockPriceGridItem(Tag, DataSource, Date, Company, Price), IStockPriceOuterKeyProperties, IGridItem<StockPriceGridItem>, IPersistenceTransferObject
    {
    }
}
