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

namespace PowerGrid.Hosting.Rest.Models.DataTransferObjects
{
    /// <summary>
    /// Base for DTO container classes holding a grid of stock prices.
    /// </summary>
    public abstract class StockPriceGridBase
    {
        /// <summary>The outer key properties of the items.</summary>
        public StockPriceGridOuterKeyProperties StockPriceGridOuterKeyProperties { get; set; }
    }
}
