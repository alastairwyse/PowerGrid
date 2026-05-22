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
using PowerGrid.Core;

namespace PowerGrid.Grids
{
    /// <summary>
    /// Model/container class holding a <see cref="StockPrice"/> augmented with key properties allowing it to be stored in a grid.
    /// </summary>
    /// <param name="Tag">A tag used to classify the grid.</param>
    /// <param name="DataSource">The source/entity which provided the price.</param>
    /// <param name="Date">The date the price was quoted for.</param>
    /// <param name="Company">The company the price was quoted for.</param>
    /// <param name="Price">The price.</param>
    public record StockPriceGridItem(String Tag, String DataSource, DateOnly Date, String Company, Decimal Price)
        : StockPrice(Company, Price), IStockPriceOuterKeyProperties, IGridItem<StockPriceGridItem>
    {
        /// <inheritdoc/>
        public int KeyCompareTo(StockPriceGridItem other)
        {
            if (this.Tag.CompareTo(other.Tag) == 0)
            {
                if (this.DataSource.CompareTo(other.DataSource) == 0)
                {
                    if (this.Date.CompareTo(other.Date) == 0)
                    {
                        return base.KeyCompareTo(other);
                    }
                    else
                    {
                        return this.Date.CompareTo(other.Date);
                    }
                }
                else
                {
                    return this.DataSource.CompareTo(other.DataSource);
                }
            }
            else
            {
                return this.Tag.CompareTo(other.Tag);
            }
        }

        /// <inheritdoc/>
        public bool ValuePropertiesEqual(StockPriceGridItem other)
        {
            return base.ValuePropertiesEqual(other);
        }
    }
}
