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
    /// Model/container class holding a stock price.
    /// </summary>
    /// <param name="Company">The company the price was quoted for.</param>
    /// <param name="Price">The price.</param>
    public record StockPrice(String Company, Decimal Price) : IGridItem<StockPrice>
    {
        /// <inheritdoc/>
        public Int32 KeyCompareTo(StockPrice other)
        {
            return this.Company.CompareTo(other.Company);
        }

        /// <inheritdoc/>
        public Boolean ValuePropertiesEqual(StockPrice other)
        {
            return this.Price.Equals(other.Price);
        }
    }
}
