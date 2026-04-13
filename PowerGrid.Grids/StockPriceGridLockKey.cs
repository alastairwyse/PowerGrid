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
    /// An <see cref="IGridLockKey"/> implementation for <see cref="StockPrice"/> instances.
    /// </summary>
    public class StockPriceGridLockKey : GridLockKeyBase<StockPrice>
    {
        /// <summary>The stock price object to generate the lock for.</summary>
        protected StockPrice underlyingStockPrice;
        /// <summary>The values of the <see cref="IKeyPropertyComparable{T}">key properties</see> of the <see cref="StockPrice"/> that this class creates a mutual-exclusion lock for.</summary>
        private Object[] underlyingObjectKeyPropertyValues;

        /// <inheritdoc/>
        protected override Type UnderlyingObjectType
        {
            get
            {
                return underlyingStockPrice.GetType();
            }
        }

        /// <inheritdoc/>
        protected override Object[] UnderlyingObjectKeyPropertyValues
        {
            get
            {
                return underlyingObjectKeyPropertyValues;
            }
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Grids.StockPriceGridLockKey class.
        /// </summary>
        /// <param name="underlyingStockPrice">The stock price object to generate the lock for.</param>
        public StockPriceGridLockKey(StockPrice underlyingStockPrice)
        {
            this.underlyingStockPrice = underlyingStockPrice;
            underlyingObjectKeyPropertyValues = new Object[2] { underlyingStockPrice.DataSource, underlyingStockPrice.Date };
        }
    }
}
