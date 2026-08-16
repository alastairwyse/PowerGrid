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
using PowerGrid.Persistence;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer;
using System;
using System.Collections.Generic;

namespace PowerGrid.Hosting.Rest
{
    /// <summary>
    /// Combines and hosts components to persist grids of stock prices.
    /// </summary>
    public class StockPricePersisterHost
    {
        // NB This class is purely for POC.  Many things would need to be fixed/improved before releasing
        //   * Potential DI via interfaces
        //   * Handling of persisters for different grid items (e.g. all in one host class or not)
        //   * etc

        /// <summary>Manages locks to allow concurrent persistence of grids.</summary>
        protected PersistenceConcurrencyManager concurrencyManager;
        /// <summary>Persister for grids of stock prices.</summary>
        protected StockPricePersister stockPricePersister;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.StockPricePersisterHost class.
        /// </summary>
        /// <param name="concurrencyManager">Manages locks to allow concurrent persistence of grids.</param>
        /// <param name="stockPricePersister">Persister for grids of stock prices.</param>
        public StockPricePersisterHost(PersistenceConcurrencyManager concurrencyManager, StockPricePersister stockPricePersister)
        {
            this.concurrencyManager = concurrencyManager;
            this.stockPricePersister = stockPricePersister;
        }

        public (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(StockPriceGridOuterKeyProperties gridOuterKeyProperties, IList<StockPrice> items)
        {
            GridCommonKeyProperties commonLockKeyProperties = new GridCommonKeyProperties(gridOuterKeyProperties.Tag);
            GridCommonKeyPropertiesLockKey commonLockKey = new(commonLockKeyProperties);
            StockPriceGridOuterKeyProperties outerKeyProperties = new(gridOuterKeyProperties.Tag, gridOuterKeyProperties.DataSource, gridOuterKeyProperties.Date);
            StockPriceGridOuterKeyPropertiesLockKey outerLockKey = new(outerKeyProperties);
            (Int32, GridComparisonStatistics) result = new();
            concurrencyManager.AcquireLockAndInvokeAction<GridCommonKeyProperties, StockPriceGridOuterKeyProperties>
            (
                commonLockKey,
                outerLockKey,
                () => 
                {
                    result = stockPricePersister.PersistGrid(gridOuterKeyProperties, items);
                }
            );

            return result;
        }

        public IEnumerable<StockPriceGridItemPTO> GetGrid(StockPriceGridOuterKeyProperties gridOuterKeyProperties, Int32 version)
        {
            throw new NotImplementedException();
        }

        public IList<GridVersionAndTransactionTimestamp> GetGridDetails(StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            throw new NotImplementedException();
        }

        public IList<Tuple<StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> GetGridDetails(GridCommonKeyProperties gridCommonKeyProperties)
        {
            throw new NotImplementedException();
        }

        public void SoftDeleteLatestGrid(StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            throw new NotImplementedException();
        }

        public void HardDeleteGrids(StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            throw new NotImplementedException();
        }

        public void HardDeleteGrids(GridCommonKeyProperties gridCommonKeyProperties)
        {
            throw new NotImplementedException();
        }
    }
}
