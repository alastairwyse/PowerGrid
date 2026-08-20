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

namespace PowerGrid.Hosting.Rest.Models.DataTransferObjects
{
    /// <summary>
    /// DTO container class holding the reponse to a <see cref="StockPricePersisterHost.PersistGrid(Grids.StockPriceGridOuterKeyProperties, System.Collections.Generic.IList{Grids.StockPrice})"/> method call.
    /// </summary>
    public class PersistGridResponse
    {
        /// <summary>The version number of the persisted grid</summary>
        public Int32 Version { get; set; }

        /// <summary>Statistics containing counts of the items persisted.</summary>
        public GridComparisonStatistics GridComparisonStatistics { get; set; }
    }
}
