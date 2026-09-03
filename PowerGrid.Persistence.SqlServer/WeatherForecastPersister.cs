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
        #region TEMP Refactoring -> need to move to proper position in class

        const String tagParameterName = "@Tag";
        const String dateParameterName = "@Date";
        const String timeParameterName = "@Time";

        /// <inheritdoc/>
        protected override String GridItemTableName
        {
            get { return "WeatherForecasts"; }
        }

        /// <inheritdoc/>
        protected override String GridItemEntityName
        {
            get { return "weather forecast"; }
        }

        /// <inheritdoc/>
        protected override String MaxVersionQuery
        {
            get
            {
                return @$"
                SELECT  MAX([Version]) AS {maxVersionColumnAlias} 
                FROM    WeatherForecastGrids 
                WHERE   Tag = {tagParameterName}
                  AND   [Date] = CONVERT(date, {dateParameterName}, 23)
                  AND   [Time] = CONVERT(time, {timeParameterName}, 24);";
            }
        }

        /// <inheritdoc/>
        protected override String GridInsertStatementSqlText
        {
            get
            {
                return $@"
                INSERT 
                INTO    WeatherForecastGrids 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            [Version], 
                            TransactionTimestamp
                        )
                VALUES  (
                            {tagParameterName}, 
                            CONVERT(date, {dateParameterName}, 23), 
                            CONVERT(time, {timeParameterName}, 24), 
                            {versionParameterName}, 
                            CONVERT(datetime2, {createDateTimeParameterName}, 126)
                        );";
            }
        }

        /// <inheritdoc/>
        protected override void AddGridOuterKeyPropertyQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
            sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactSql23DateStyle));
            sqlCommandShim.AddParameter(command, timeParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Time.ToString(transactSql24TimeStyle));
        }

        #endregion

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        public WeatherForecastPersister
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger
        )
            : base(connectionString, retryCount, retryInterval, operationTimeout, logger)
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public WeatherForecastPersister
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger,
            IMetricLogger metricLogger
        )
            : base(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger)
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="dateTimeProvider">A mock <see cref="IDateTimeProvider"/></param>
        /// <param name="sqlConnectionShim">A mock <see cref="ISqlConnectionShim"/>.</param>
        /// <param name="sqlTransactionShim">A mock <see cref="ISqlTransactionShim"/>.</param>
        /// <param name="sqlCommandShim">A mock <see cref="ISqlCommandShim"/>.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public WeatherForecastPersister
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger,
            IMetricLogger metricLogger,
            IDateTimeProvider dateTimeProvider,
            ISqlConnectionShim sqlConnectionShim,
            ISqlTransactionShim sqlTransactionShim,
            ISqlCommandShim sqlCommandShim
        ) : base(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger, dateTimeProvider, sqlConnectionShim, sqlTransactionShim, sqlCommandShim)
        {
        }

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

        #region Private/Protected Methods

        #endregion
    }
}
