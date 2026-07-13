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
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer.Metrics;
using ApplicationLogging;
using ApplicationMetrics;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and writes <see cref="WeatherForecast"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class WeatherForecastPersister : PersisterBase<WeatherForecast, GridCommonKeyProperties, WeatherForecastGridOuterKeyProperties, WeatherForecastGridItem, WeatherForecastGridItemPTO>
    {
        /// <inheritdoc/>
        public override (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties, IList<WeatherForecast> items)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override IEnumerable<WeatherForecastGridItemPTO> GetGrid(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties, Int32 version)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override IList<GridVersionAndTransactionTimestamp> GetGridDetails(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override IList<Tuple<WeatherForecastGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> GetGridDetails(GridCommonKeyProperties gridCommonKeyProperties)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SoftDeleteLatestGrid(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void HardDeleteGrids(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void HardDeleteGrids(GridCommonKeyProperties gridCommonKeyProperties)
        {
            throw new NotImplementedException();
        }
    }
}
