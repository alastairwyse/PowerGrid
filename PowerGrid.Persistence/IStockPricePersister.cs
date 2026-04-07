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
using PowerGrid.Grids;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Defines methods to read and write <see cref="StockPrice"/> objects from and to persistent storage;
    /// </summary>
    public interface IStockPricePersister
    {
        // TODO:
        //   Add StockPrice, Update StockPrice, Delete StockPrice
        //   Get StockPriceGrid
        //   List All StockPriceGrids for a given data source and date
        //   Create a new grid

        //   Implementation should wrap multiple steps to create a dataset in a transaction
    }
}
