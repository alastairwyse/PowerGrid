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

namespace PowerGrid.Hosting.Rest.Models.DataTransferObjects
{
    /// <summary>
    /// DTO container class holding an item in the reponse to a <see cref="StockPricePersisterHost.GetGridDetails(Grids.GridCommonKeyProperties)"/> method call.
    /// </summary>
    public class GetGridDetailsResponseItem
    {
        /// <summary>The outer key properties of the stock price grid.</summary>
        public StockPriceGridOuterKeyProperties StockPriceGridOuterKeyProperties { get; set; }

        /// <summary>The version of the grid.</summary>
        public Int32 Version { get; set; }

        /// <summary>The UTC transaction (creation) timestamp for the grid.</summary>
        public DateTime TransactionTimestamp { get; set; }
    }
}
