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
using System.Collections.Generic;
using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer;

namespace PowerGrid.Hosting.Rest
{
    /// <summary>
    /// Combines and hosts components to persist grids of weather forecasts.
    /// </summary>
    public class WeatherForecastPersisterHost
    {
        /// <summary>Manages locks to allow concurrent persistence of grids.</summary>
        protected IPersistenceConcurrencyManager concurrencyManager;
        /// <summary>Persister for grids of weather forecasts.</summary>
        protected WeatherForecastPersister weatherForecastPersister;


        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.WeatherForecastPersisterHost class.
        /// </summary>
        /// <param name="concurrencyManager">Manages locks to allow concurrent persistence of grids.</param>
        /// <param name="weatherForecastPersister">Persister for grids of weather forecasts.</param>WeatherForecast
        public WeatherForecastPersisterHost(IPersistenceConcurrencyManager concurrencyManager, WeatherForecastPersister weatherForecastPersister)
        {
            this.concurrencyManager = concurrencyManager;
            this.weatherForecastPersister = weatherForecastPersister;
        }

        public (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties, IList<WeatherForecast> items)
        {
            GridCommonKeyProperties commonLockKeyProperties = new GridCommonKeyProperties(gridOuterKeyProperties.Tag);
            GridCommonKeyPropertiesLockKey commonLockKey = new(commonLockKeyProperties);
            WeatherForecastGridOuterKeyProperties outerKeyProperties = new(gridOuterKeyProperties.Tag, gridOuterKeyProperties.Date, gridOuterKeyProperties.Time);
            WeatherForecastGridOuterKeyPropertiesLockKey outerLockKey = new(outerKeyProperties);
            (Int32, GridComparisonStatistics) result = new();
            concurrencyManager.AcquireLockAndInvokeAction<GridCommonKeyProperties, WeatherForecastGridOuterKeyProperties>
            (
                commonLockKey,
                outerLockKey,
                () =>
                {
                    result = weatherForecastPersister.PersistGrid(gridOuterKeyProperties, items);
                }
            );

            return result;
        }

        public IEnumerable<WeatherForecastGridItemPTO> GetGrid(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties, Int32 version)
        {
            return weatherForecastPersister.GetGrid(gridOuterKeyProperties, version);
        }

        public IList<GridVersionAndTransactionTimestamp> GetGridDetails(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            return weatherForecastPersister.GetGridDetails(gridOuterKeyProperties);
        }

        public IList<Tuple<WeatherForecastGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> GetGridDetails(GridCommonKeyProperties gridCommonKeyProperties)
        {
            return weatherForecastPersister.GetGridDetails(gridCommonKeyProperties);
        }

        public void SoftDeleteLatestGrid(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            weatherForecastPersister.SoftDeleteLatestGrid(gridOuterKeyProperties);
        }

        public void HardDeleteGrids(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            weatherForecastPersister.HardDeleteGrids(gridOuterKeyProperties);
        }

        public void HardDeleteGrids(GridCommonKeyProperties gridCommonKeyProperties)
        {
            weatherForecastPersister.HardDeleteGrids(gridCommonKeyProperties);
        }
    }
}
