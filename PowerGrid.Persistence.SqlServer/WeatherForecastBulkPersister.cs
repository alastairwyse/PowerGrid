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
using System.Data;
using System.Globalization;
using System.Linq;
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using ApplicationLogging;
using ApplicationMetrics;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and bulk writes <see cref="WeatherForecast"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class WeatherForecastBulkPersister : BulkPersisterBase<WeatherForecast, GridCommonKeyProperties, WeatherForecastGridOuterKeyProperties, WeatherForecastGridItem, WeatherForecastGridItemPTO>
    {
        const String tagParameterName = "@Tag";
        const String dateParameterName = "@Date";
        const String timeParameterName = "@Time";
        const String countryParameterName = "@Country";
        const String cityParameterName = "@City";
        const String temperatureParameterName = "@Temperature";

        const String tagColumnName = "Tag";
        const String dateColumnName = "Date";
        const String timeColumnName = "Time";
        const String countryColumnName = "Country";
        const String cityColumnName = "City";
        const String temperatureColumnName = "Temperature";

        #region Base Class Property Implementations

        /// <inheritdoc/>
        protected override String GridItemTableName
        {
            get { return "WeatherForecasts"; }
        }

        /// <inheritdoc/>
        protected override String GridTableName
        {
            get { return "WeatherForecastGrids"; }
        }

        /// <inheritdoc/>
        protected override String GridItemEntityName
        {
            get { return "weather forecast"; }
        }

        /// <inheritdoc/>
        protected override String GridMaxVersionQuery
        {
            get
            {
                return @$"
                SELECT  MAX([{versionColumnName}]) AS {maxVersionColumnAlias} 
                FROM    {GridTableName} 
                WHERE   Tag = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24)";
            }
        }

        /// <inheritdoc/>
        protected override String GridMaxVersionAndTransactionTimestampQuery
        {
            get
            {
                return @$"
                SELECT  [{versionColumnName}] AS [{versionColumnName}], 
                        CONVERT(nvarchar(30), {transactionTimestampColumnName} , 126) AS {transactionTimestampColumnName}
                FROM    {GridTableName} 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24) 
                  AND   [{versionColumnName}] = 
                        (
                          {GridMaxVersionQuery}
                        );";
            }
        }

        /// <inheritdoc/>
        protected override String GridTransactionTimestampQuery
        {
            get
            {
                return @$"
                SELECT  CONVERT(nvarchar(30), {transactionTimestampColumnName} , 126) AS {transactionTimestampColumnName}
                FROM    {GridTableName} 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24) 
                  AND   [{versionColumnName}] = {versionParameterName};";
            }
        }

        /// <inheritdoc/>
        protected override String GridContentsQuery
        {
            get
            {
                return @$"
                SELECT  {idColumnName}, 
                        {tagColumnName}, 
                        CONVERT(nvarchar(30), [{dateColumnName}], 23) AS [{dateColumnName}], 
                        CONVERT(nvarchar(30), [{timeColumnName}], 24) AS [{timeColumnName}], 
                        {countryColumnName}, 
                        {cityColumnName}, 
                        {temperatureColumnName}, 
                        CONVERT(nvarchar(30), {transactionFromColumnName}, 126) AS {transactionFromColumnName}, 
                        CONVERT(nvarchar(30), {transactionToColumnName}, 126) AS {transactionToColumnName}
                FROM    {GridItemTableName} 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24) 
                  AND   CONVERT(datetime2, {transactionTimestampParameterName}, 126) BETWEEN {transactionFromColumnName} AND {transactionToColumnName} 
                ORDER   BY {countryColumnName}, 
                           {cityColumnName};";
            }
        }

        /// <inheritdoc/>
        protected override String GridDetailsByCommonKeyPropertiesQuery
        {
            get
            {
                return @$"{GridDetailsBaseQuery}
                WHERE   {tagColumnName} = {tagParameterName};";
            }
        }

        protected override String GridDetailsByOuterKeyPropertiesQuery
        {
            get
            {
                return @$"{GridDetailsBaseQuery}
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24);";
            }
        }

        /// <inheritdoc/>
        protected override String SoftDeleteLatestGridStatementSqlText
        {
            get
            {
                return @$"
                UPDATE  {GridItemTableName} 
                SET     {transactionToColumnName} = CONVERT(datetime2, {deleteDateTimeParameterName}, 126) 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24) 
                  AND   CONVERT(datetime2, {currentDateTimeParameterName}, 126) BETWEEN {transactionFromColumnName} AND {transactionToColumnName};";
            }
        }

        /// <inheritdoc/>
        protected override String HardDeleteGridsByCommonKeyPropertiesStatementSqlText
        {
            get
            {
                return @$"
                DELETE 
                FROM    {GridTableName} 
                WHERE   {tagColumnName} = {tagParameterName};";
            }
        }

        /// <inheritdoc/>
        protected override String HardDeleteGridItemssByCommonKeyPropertiesStatementSqlText
        {
            get
            {
                return @$"
                DELETE 
                FROM    {GridItemTableName} 
                WHERE   {tagColumnName} = {tagParameterName};";
            }
        }

        /// <inheritdoc/>
        protected override String HardDeleteGridsByOuterKeyPropertiesStatementSqlText
        {
            get
            {
                return @$"
                DELETE 
                FROM    {GridTableName} 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24);";
            }
        }

        /// <inheritdoc/>
        protected override String HardDeleteGridItemssByOuterKeyPropertiesStatementSqlText
        {
            get
            {
                return @$"
                DELETE 
                FROM    {GridItemTableName} 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{timeColumnName}] = CONVERT(time, {timeParameterName}, 24);";
            }
        }

        /// <inheritdoc/>
        protected override String GridInsertStatementSqlText
        {
            get
            {
                return $@"
                INSERT 
                INTO    {GridTableName} 
                        (
                            {tagColumnName}, 
                            [{dateColumnName}], 
                            [{timeColumnName}], 
                            [{versionColumnName}], 
                            {transactionTimestampColumnName}
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
        protected override String GridItemsInsertStatementSqlText
        {
            get
            {
                return @$"
                INSERT 
                INTO    {GridItemTableName} 
                        (
                            {tagColumnName}, 
                            [{dateColumnName}], 
                            [{timeColumnName}], 
                            {countryColumnName}, 
                            {cityColumnName}, 
                            {temperatureColumnName}, 
                            {transactionFromColumnName}, 
                            {transactionToColumnName} 
                        )
                VALUES  (
                            {tagParameterName}, 
                            CONVERT(date, {dateParameterName}, 23), 
                            CONVERT(time, {timeParameterName}, 24), 
                            {countryParameterName}, 
                            {cityParameterName}, 
                            {temperatureParameterName}, 
                            CONVERT(datetime2, {insertDateTimeParameterName}, 126), 
                            CONVERT(datetime2, {temporalMaximumDateTimeParameterName}, 126)
                        );";
            }
        }

        #endregion

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastBulkPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="bufferSizeLimit">The maximum number of <see cref="WeatherForecastGridItem"/> objects buffer before writing in bulk.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        public WeatherForecastBulkPersister
        (
            String connectionString,
            Int32 bufferSizeLimit,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger
        )
            : base(connectionString, bufferSizeLimit, retryCount, retryInterval, operationTimeout, logger)
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastBulkPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="bufferSizeLimit">The maximum number of <see cref="WeatherForecastGridItem"/> objects buffer before writing in bulk.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public WeatherForecastBulkPersister
        (
            String connectionString,
            Int32 bufferSizeLimit,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger,
            IMetricLogger metricLogger
        )
            : base(connectionString, bufferSizeLimit, retryCount, retryInterval, operationTimeout, logger, metricLogger)
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastBulkPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="bufferSizeLimit">The maximum number of <see cref="WeatherForecastGridItem"/> objects buffer before writing in bulk.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="dateTimeProvider">A mock <see cref="IDateTimeProvider"/></param>
        /// <param name="sqlConnectionShim">A mock <see cref="ISqlConnectionShim"/>.</param>
        /// <param name="sqlTransactionShim">A mock <see cref="ISqlTransactionShim"/>.</param>
        /// <param name="sqlCommandShim">A mock <see cref="ISqlCommandShim"/>.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public WeatherForecastBulkPersister
        (
            String connectionString,
            Int32 bufferSizeLimit,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger,
            IMetricLogger metricLogger,
            IDateTimeProvider dateTimeProvider,
            ISqlConnectionShim sqlConnectionShim,
            ISqlTransactionShim sqlTransactionShim,
            ISqlCommandShim sqlCommandShim
        ) : base(connectionString, bufferSizeLimit, retryCount, retryInterval, operationTimeout, logger, metricLogger, dateTimeProvider, sqlConnectionShim, sqlTransactionShim, sqlCommandShim)
        {
        }

        #region Private/Protected Methods

        #region Base Class Method Implementations

        /// <inheritdoc/>
        protected override Action<WeatherForecast> NewEntityValidationAction
        {
            get
            {
                return (WeatherForecast weatherForecast) =>
                {
                    // Test for less than absolute zero
                    const Int32 absoluteZero = -274;
                    if (weatherForecast.Temperature < absoluteZero)
                        throw new GridContentsValidationException<WeatherForecast>($"{weatherForecast.ToString()} {nameof(WeatherForecast.Temperature)} {weatherForecast.Temperature} cannot be less than {absoluteZero}.", weatherForecast);
                };
            }
        }

        /// <inheritdoc/>
        protected override WeatherForecastGridOuterKeyProperties GetOuterKeyPropertiesFromDataReader(IDataReader dataReader)
        {
            String tag = (String)dataReader[tagColumnName];
            DateOnly date = DateOnly.ParseExact((String)dataReader[dateColumnName], transactSql23DateStyle, DateTimeFormatInfo.InvariantInfo);
            TimeOnly time = TimeOnly.ParseExact((String)dataReader[timeColumnName], transactSql24TimeStyle, DateTimeFormatInfo.InvariantInfo);

            return new WeatherForecastGridOuterKeyProperties(tag, date, time);
        }

        /// <inheritdoc/>
        protected override WeatherForecastGridItemPTO GetGridItemPTOFromDataReader(IDataReader dataReader)
        {
            Int64 id = (Int64)dataReader[idColumnName];
            String tag = (String)dataReader[tagColumnName];
            DateOnly date = DateOnly.ParseExact((String)dataReader[dateColumnName], transactSql23DateStyle, DateTimeFormatInfo.InvariantInfo);
            TimeOnly time = TimeOnly.ParseExact((String)dataReader[timeColumnName], transactSql24TimeStyle, DateTimeFormatInfo.InvariantInfo);
            String country = (String)dataReader[countryColumnName];
            String city = (String)dataReader[cityColumnName];
            Int32 temperature = (Int32)dataReader[temperatureColumnName];
            (DateTime transactionFrom, DateTime transactionTo) = GetTransactionFromAndToDateTimesFromDataReader(dataReader);

            return new WeatherForecastGridItemPTO(id, tag, date, time, country, city, temperature, transactionFrom, transactionTo);
        }

        /// <inheritdoc/>
        protected override void AddGridCommonKeyPropertyQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, GridCommonKeyProperties gridCommonKeyProperties)
        {
            sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridCommonKeyProperties.Tag);
        }

        /// <inheritdoc/>
        protected override void AddGridOuterKeyPropertyQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, WeatherForecastGridOuterKeyProperties gridOuterKeyProperties)
        {
            AddGridCommonKeyPropertyQueryParameters(sqlCommandShim, command, new GridCommonKeyProperties(gridOuterKeyProperties.Tag));
            sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactSql23DateStyle));
            sqlCommandShim.AddParameter(command, timeParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Time.ToString(transactSql24TimeStyle));
        }

        /// <inheritdoc/>
        protected override void AddGridItemQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, WeatherForecast entity)
        {
            sqlCommandShim.AddParameter(command, countryParameterName, SqlDbType.NVarChar, entity.Country);
            sqlCommandShim.AddParameter(command, cityParameterName, SqlDbType.NVarChar, entity.City);
            sqlCommandShim.AddParameter(command, temperatureParameterName, SqlDbType.Int, entity.Temperature);
        }

        /// <inheritdoc/>
        protected override WeatherForecastGridOuterKeyProperties ExtractOuterKeyPropertiesFromGridItem(WeatherForecastGridItem gridItem)
        {
            return new WeatherForecastGridOuterKeyProperties(gridItem.Tag, gridItem.Date, gridItem.Time);
        }

        /// <inheritdoc/>
        protected override WeatherForecastGridItem ConvertEntityAndOuterKeyPropertiesToGridItem(WeatherForecastGridOuterKeyProperties gridOuterKeyProperties, WeatherForecast entity)
        {
            return new WeatherForecastGridItem(gridOuterKeyProperties.Tag, gridOuterKeyProperties.Date, gridOuterKeyProperties.Time, entity.Country, entity.City, entity.Temperature);
        }

        #endregion

        /// <summary>
        /// The text for a SQL query which returns the details of all grids for a set of outer key properties.
        /// </summary>
        protected String GridDetailsBaseQuery
        {
            get
            {
                return @$"
                SELECT  {tagColumnName}, 
                        CONVERT(nvarchar(30), [{dateColumnName}], 23) AS [{dateColumnName}], 
                        CONVERT(nvarchar(30), [{timeColumnName}], 24) AS [{timeColumnName}], 
                        [{versionColumnName}], 
                        CONVERT(nvarchar(30), {transactionTimestampColumnName}, 126) AS {transactionTimestampColumnName} 
                FROM    {GridTableName} ";
            }
        }

        /// <inheritdoc/>
        protected override BulkAddStoredProcedureEmitterBase<WeatherForecastGridItem> InitializeBulkAddEmitter(DateTime transactionTimestamp, SqlConnection connection, SqlTransaction transaction)
        {
            return new BulkAddEmitter(transactionTimestamp, bufferSizeLimit, sqlConnectionShim, sqlCommandShim, connection, transaction, sqlRetryLogicOption, operationTimeout, connectionRetryAction);
        }

        /// <inheritdoc/>
        protected override BulkDeleteStoredProcedureEmitterBase<WeatherForecastGridItemPTO> InitializeBulkDeleteEmitter(DateTime transactionTimestamp, SqlConnection connection, SqlTransaction transaction)
        {
            return new BulkDeleteEmitter(transactionTimestamp, bufferSizeLimit, sqlConnectionShim, sqlCommandShim, connection, transaction, sqlRetryLogicOption, operationTimeout, connectionRetryAction);
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which persists <see cref="WeatherForecastGridItem"/> objects to a SQL Server database in bulk, by buffering objects received through the <see cref="BulkAddEmitter.Emit(WeatherForecastGridItem)"/> method, and writing those objects to SQL Server when the buffer reaches a specified size.
        /// </summary>
        protected class BulkAddEmitter : BulkAddStoredProcedureEmitterBase<WeatherForecastGridItem>
        {
            #region Base Class Property Implementations

            /// <inheritdoc/>
            protected override String BulkInsertStoredProcedureName
            {
                get
                {
                    return "BulkInsertWeatherForecasts";
                }
            }

            #endregion

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastBulkPersister+BulkAddEmitter class.
            /// </summary>
            /// <param name="transactionTimestamp">The timestamp representing when the objects emitted were either inserted or deleted.</param>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="WeatherForecastGridItem"/> objects to hold in the buffer.</param>
            /// <param name="sqlConnectionShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</param>
            /// <param name="sqlCommandShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlCommand"/> class.</param>
            /// <param name="sqlConnection">The connection to use to insert or delete.</param>
            /// <param name="sqlTransaction">The transaction to use to insert or delete.</param>
            /// <param name="sqlRetryLogicOption">Parameters for retrying when transient errors occur.</param>
            /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
            /// <param name="connectionRetryAction">The action to invoke if an action is retried due to a transient error.</param>
            public BulkAddEmitter
            (
                DateTime transactionTimestamp,
                Int32 bufferSizeLimit,
                ISqlConnectionShim sqlConnectionShim,
                ISqlCommandShim sqlCommandShim,
                SqlConnection sqlConnection,
                SqlTransaction sqlTransaction,
                SqlRetryLogicOption sqlRetryLogicOption,
                Int32 operationTimeout,
                EventHandler<SqlRetryingEventArgs> connectionRetryAction
            )
                : base(transactionTimestamp, bufferSizeLimit, sqlConnectionShim, sqlCommandShim, sqlConnection, sqlTransaction, sqlRetryLogicOption, operationTimeout, connectionRetryAction)
            {
            }

            #region Private/Protected Methods

            #region Base Class Method Implementations

            /// <inheritdoc/>
            protected override void CopyGridItemToDataRow(DataRow row, WeatherForecastGridItem gridItem)
            {
                row[gridItemData1ColumnName] = gridItem.Tag;
                row[gridItemData2ColumnName] = gridItem.Date.ToString(transactSql23DateStyle);
                row[gridItemData3ColumnName] = gridItem.Time.ToString(transactSql24TimeStyle);
                row[gridItemData4ColumnName] = gridItem.Country;
                row[gridItemData5ColumnName] = gridItem.City;
                row[gridItemData6ColumnName] = gridItem.Temperature.ToString(CultureInfo.InvariantCulture);
            }

            #endregion

            #endregion
        }

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which soft deletes <see cref="WeatherForecastGridItemPTO"/> objects in a SQL Server database in bulk, by buffering objects received through the <see cref="BulkDeleteEmitter.Emit(WeatherForecastGridItem)"/> method, and deleting those objects from SQL Server when the buffer reaches a specified size.
        /// </summary>
        protected class BulkDeleteEmitter : BulkDeleteStoredProcedureEmitterBase<WeatherForecastGridItemPTO>
        {
            #region Base Class Property Implementations

            /// <inheritdoc/>
            protected override String BulkDeleteStoredProcedureName
            {
                get
                {
                    return "BulkDeleteWeatherForecasts";
                }
            }

            #endregion

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.WeatherForecastBulkPersister+BulkDeleteEmitter class.
            /// </summary>
            /// <param name="transactionTimestamp">The timestamp representing when the objects emitted were either inserted or deleted.</param>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="WeatherForecastGridItemPTO"/> objects to hold in the buffer.</param>
            /// <param name="sqlConnectionShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</param>
            /// <param name="sqlCommandShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlCommand"/> class.</param>
            /// <param name="sqlConnection">The connection to use to insert or delete.</param>
            /// <param name="sqlTransaction">The transaction to use to insert or delete.</param>
            /// <param name="sqlRetryLogicOption">Parameters for retrying when transient errors occur.</param>
            /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
            /// <param name="connectionRetryAction">The action to invoke if an action is retried due to a transient error.</param>
            public BulkDeleteEmitter
            (
                DateTime transactionTimestamp,
                Int32 bufferSizeLimit,
                ISqlConnectionShim sqlConnectionShim,
                ISqlCommandShim sqlCommandShim,
                SqlConnection sqlConnection,
                SqlTransaction sqlTransaction,
                SqlRetryLogicOption sqlRetryLogicOption,
                Int32 operationTimeout,
                EventHandler<SqlRetryingEventArgs> connectionRetryAction
            )
                : base(transactionTimestamp, bufferSizeLimit, sqlConnectionShim, sqlCommandShim, sqlConnection, sqlTransaction, sqlRetryLogicOption, operationTimeout, connectionRetryAction)
            {
            }
        }

        #endregion
    }
}
