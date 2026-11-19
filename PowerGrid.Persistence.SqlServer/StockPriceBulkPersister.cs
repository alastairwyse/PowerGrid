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
using PowerGrid.Persistence.Metrics;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using ApplicationLogging;
using ApplicationMetrics;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and bulk writes <see cref="StockPrice"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class StockPriceBulkPersister : BulkPersisterBase<StockPrice, GridCommonKeyProperties, StockPriceGridOuterKeyProperties, StockPriceGridItem, StockPriceGridItemPTO>
    {
        const String tagParameterName = "@Tag";
        const String dataSourceParameterName = "@DataSource";
        const String dateParameterName = "@Date";
        const String companyParameterName = "@Company";
        const String priceParameterName = "@Price";

        const String tagColumnName = "Tag";
        const String dataSourceColumnName = "DataSource";
        const String dateColumnName = "Date";
        const String companyColumnName = "Company";
        const String priceColumnName = "Price";

        #region Base Class Property Implementations

        /// <inheritdoc/>
        protected override String GridItemTableName
        {
            get { return "StockPrices"; }
        }

        /// <inheritdoc/>
        protected override String GridTableName
        {
            get { return "StockPriceGrids"; }
        }

        /// <inheritdoc/>
        protected override String GridItemEntityName
        {
            get { return "stock price"; }
        }

        /// <inheritdoc/>
        protected override String GridMaxVersionQuery
        {
            get
            {
                return @$"
                SELECT  MAX([{versionColumnName}]) AS {maxVersionColumnAlias} 
                FROM    {GridTableName} 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   {dataSourceColumnName} = {dataSourceParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23)";
            }
        }

        /// <inheritdoc/>
        protected override String GridMaxVersionAndTransactionTimestampQuery
        {
            get
            {
                return @$"
                SELECT  [{versionColumnName}], 
                        CONVERT(nvarchar(30), {transactionTimestampColumnName} , 126) AS {transactionTimestampColumnName}
                FROM    {GridTableName} 
                WHERE   {tagColumnName} = {tagParameterName} 
                  AND   {dataSourceColumnName} = {dataSourceParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
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
                  AND   {dataSourceColumnName} = {dataSourceParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND   [{versionColumnName}] = {versionParameterName};";
            }
        }

        /// <inheritdoc/>
        protected override String GridContentsQuery
        {
            get
            {
                return @$"
                SELECT Id, 
                       {tagColumnName}, 
                       {dataSourceColumnName}, 
                       CONVERT(nvarchar(30), [Date], 23) AS [{dateColumnName}], 
                       {companyColumnName}, 
                       {priceColumnName}, 
                       CONVERT(nvarchar(30), {transactionFromColumnName}, 126) AS {transactionFromColumnName}, 
                       CONVERT(nvarchar(30), {transactionToColumnName}, 126) AS {transactionToColumnName}
                FROM   {GridItemTableName} 
                WHERE  {tagColumnName} = {tagParameterName}
                  AND  {dataSourceColumnName} = {dataSourceParameterName}
                  AND  [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND  CONVERT(datetime2, {transactionTimestampParameterName}, 126) BETWEEN {transactionFromColumnName} AND {transactionToColumnName} 
                ORDER  BY {companyColumnName};";
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
                  AND   {dataSourceColumnName} = {dataSourceParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23);";
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
                  AND   {dataSourceColumnName} = {dataSourceParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
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
                  AND   {dataSourceColumnName} = {dataSourceParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23);";
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
                  AND   {dataSourceColumnName} = {dataSourceParameterName} 
                  AND   [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23);";
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
                            {dataSourceColumnName}, 
                            [{dateColumnName}], 
                            [{versionColumnName}], 
                            {transactionTimestampColumnName}
                        )
                VALUES  (
                            {tagParameterName}, 
                            {dataSourceParameterName}, 
                            CONVERT(date, {dateParameterName}, 23), 
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
                            {dataSourceColumnName}, 
                            [{dateColumnName}], 
                            {companyColumnName}, 
                            {priceColumnName}, 
                            {transactionFromColumnName}, 
                            {transactionToColumnName} 
                        )
                VALUES  (
                            {tagParameterName}, 
                            {dataSourceParameterName}, 
                            CONVERT(date, {dateParameterName}, 23), 
                            {companyParameterName}, 
                            {priceParameterName}, 
                            CONVERT(datetime2, {insertDateTimeParameterName}, 126), 
                            CONVERT(datetime2, {temporalMaximumDateTimeParameterName}, 126)
                        );";
            }
        }

        /// <inheritdoc/>
        protected override Action<StockPrice> NewEntityValidationAction
        {
            get
            {
                return (StockPrice stockPrice) =>
                {
                    if (stockPrice.Price < 0)
                        throw new GridContentsValidationException<StockPrice>($"{stockPrice.ToString()} has negative {nameof(StockPrice.Price)} {stockPrice.Price}.", stockPrice);
                };
            }
        }

        #endregion

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItem"/> objects buffer before writing in bulk.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        public StockPriceBulkPersister
        (
            String connectionString,
            Int32 bufferSizeLimit,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger
        )
            : base(connectionString, retryCount, retryInterval, operationTimeout, logger)
        {
            ValidateBufferSizeLimitParameter(bufferSizeLimit);

            this.bufferSizeLimit = bufferSizeLimit; 
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItem"/> objects buffer before writing in bulk.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public StockPriceBulkPersister
        (
            String connectionString,
            Int32 bufferSizeLimit,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger,
            IMetricLogger metricLogger
        )
            : base(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger)
        {
            ValidateBufferSizeLimitParameter(bufferSizeLimit);

            this.bufferSizeLimit = bufferSizeLimit;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItem"/> objects buffer before writing in bulk.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="dateTimeProvider">A mock <see cref="IDateTimeProvider"/></param>
        /// <param name="sqlConnectionShim">A mock <see cref="ISqlConnectionShim"/>.</param>
        /// <param name="sqlTransactionShim">A mock <see cref="ISqlTransactionShim"/>.</param>
        /// <param name="sqlCommandShim">A mock <see cref="ISqlCommandShim"/>.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public StockPriceBulkPersister
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
        ) : base(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger, dateTimeProvider, sqlConnectionShim, sqlTransactionShim, sqlCommandShim)
        {
            ValidateBufferSizeLimitParameter(bufferSizeLimit);

            this.bufferSizeLimit = bufferSizeLimit;
        }

        /// <inheritdoc/>
        public override (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(StockPriceGridOuterKeyProperties gridOuterKeyProperties, IList<StockPrice> items, PersistComparisonStrategy persistComparisonStrategy = PersistComparisonStrategy.Syncronise)
        {
            if (items.Count == 0)
                throw new ArgumentException($"Parameter '{nameof(items)}' contained no items.", nameof(items));

            using (var readConnection = new SqlConnection(connectionString))
            using (var writeConnection = new SqlConnection(connectionString))
            {
                Int32 gridVersion;
                GridComparisonStatistics comparisonStatistics;
                try
                {
                    PrepareConnection(readConnection);
                    sqlConnectionShim.Open(writeConnection);
                    PrepareConnection(writeConnection, SessionDeadlockPriority.High);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to connect to SQL Server.", e);
                }

                DateTime transactionTimestamp = dateTimeProvider.UtcNow();
                using (SqlTransaction transaction = sqlConnectionShim.BeginTransaction(writeConnection))
                {
                    BulkAddEmitter addedItemEmitter = new(transactionTimestamp, bufferSizeLimit, sqlConnectionShim, sqlCommandShim, writeConnection, transaction, sqlRetryLogicOption, operationTimeout, connectionRetryAction);
                    BulkDeleteEmitter deletedItemEmitter = new(transactionTimestamp, bufferSizeLimit, sqlConnectionShim, sqlCommandShim, writeConnection, transaction, sqlRetryLogicOption, operationTimeout, connectionRetryAction);
                    Action<SqlConnection, SqlTransaction, Tuple<StockPriceGridItemPTO, StockPriceGridItem>, DateTime> updatedItemsEmitterOperationAction = (SqlConnection connection, SqlTransaction transaction, Tuple<StockPriceGridItemPTO, StockPriceGridItem> updatedGridItems, DateTime transactionDateTime) =>
                    {
                        deletedItemEmitter.Emit(updatedGridItems.Item1);
                        addedItemEmitter.Emit(updatedGridItems.Item2);
                    };
                    DataBaseOperationEmitter<Tuple<StockPriceGridItemPTO, StockPriceGridItem>> updatedItemsEmitter = new(writeConnection, transaction, transactionTimestamp, updatedItemsEmitterOperationAction);
                    GridComparer<StockPriceGridItemPTO, StockPriceGridItem> gridComparer;
                    if (persistComparisonStrategy == PersistComparisonStrategy.AddAndUpdateOnly)
                    {
                        gridComparer = new(addedItemEmitter, updatedItemsEmitter, new NullEmitter<StockPriceGridItemPTO>());
                    }
                    else
                    {
                        gridComparer = new(addedItemEmitter, updatedItemsEmitter, deletedItemEmitter);
                    }

                    // Setup IEnumerable 'chains' 
                    GridContentsValidator<StockPrice> newItemValidator = new();
                    GridContentsDuplicateChecker<StockPrice> newItemsDuplicateChecker = new();
                    // Order of below chain is 1 validate, 2 order, 3 dup check
                    IEnumerable<StockPrice> newGridContents = newItemsDuplicateChecker.CheckForDuplicates
                    (
                        newItemValidator.ValidateItems
                        (
                            items,
                            NewEntityValidationAction
                        ).Order(Comparer<StockPrice>.Create
                        (
                            (StockPrice first, StockPrice second) => { return first.KeyCompareTo(second); }
                        ))
                    );
                    IEnumerable<StockPriceGridItem> ConvertEntitiesToGridItems(IEnumerable<StockPrice> items)
                    {
                        foreach (StockPrice currentItem in items)
                        {
                            yield return ConvertEntityAndOuterKeyPropertiesToGridItem(gridOuterKeyProperties, currentItem);
                        }
                    }

                    Guid beginId = metricLogger.Begin(new GridBulkPersistTime());
                    try
                    {
                        sqlConnectionShim.Open(readConnection);
                        IEnumerable<StockPriceGridItemPTO> existingGridContents;
                        try
                        {
                            existingGridContents = GetGrid(readConnection, gridOuterKeyProperties, transactionTimestamp);
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"Failed to read existing {GridItemEntityName} grid from SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactSql126DateStyle)}'.", e);
                        }
                        {
                            try
                            {
                                comparisonStatistics = gridComparer.Compare(existingGridContents, ConvertEntitiesToGridItems(newGridContents));
                                deletedItemEmitter.Flush();
                                addedItemEmitter.Flush();
                            }
                            catch (Exception e)
                            {
                                Exception compareException = new($"Failed to compare new {GridItemEntityName} grid to existing grid in SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactSql126DateStyle)}'.", e);
                                try
                                {
                                    // As per https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqltransaction.rollback?view=sqlclient-dotnet-core-6.1, exception can occur on rollback
                                    sqlTransactionShim.Rollback(transaction);
                                }
                                catch (Exception rollbackException)
                                {
                                    throw new AggregateException($"Failed to rollback transaction after exception comparing {GridItemEntityName} grid to existing data.", rollbackException, compareException);
                                }
                                throw compareException;
                            }
                        }
                        gridVersion = CreateGrid(readConnection, writeConnection, transaction, gridOuterKeyProperties, transactionTimestamp);
                        sqlTransactionShim.Commit(transaction);

                        sqlConnectionShim.Close(writeConnection);
                        sqlConnectionShim.Close(readConnection);
                    }
                    catch (Exception e)
                    {
                        metricLogger.CancelBegin(beginId, new GridBulkPersistTime());
                        throw new Exception("Failed to persist grid to SQL Server.", e);
                    }
                    metricLogger.End(beginId, new GridBulkPersistTime());
                    metricLogger.Increment(new GridBulkPersisted());
                }
                TeardownConnection(readConnection);
                TeardownConnection(writeConnection);
                if (persistComparisonStrategy == PersistComparisonStrategy.AddAndUpdateOnly)
                {
                    comparisonStatistics = new GridComparisonStatistics(comparisonStatistics.ItemsAddedCount, comparisonStatistics.ItemsUpdatedCount, 0);
                }
                metricLogger.Add(new ComparedGridItemsAdded(), comparisonStatistics.ItemsAddedCount);
                metricLogger.Add(new ComparedGridItemsUpdated(), comparisonStatistics.ItemsUpdatedCount);
                metricLogger.Add(new ComparedGridItemsDeleted(), comparisonStatistics.ItemsDeletedCount);

                return (gridVersion, comparisonStatistics);
            }
        }

        #region Private/Protected Methods


        #region Base Class Method Implementations

        /// <inheritdoc/>
        protected override StockPriceGridOuterKeyProperties GetOuterKeyPropertiesFromDataReader(IDataReader dataReader)
        {
            String tag = (String)dataReader[tagColumnName];
            String dataSource = (String)dataReader[dataSourceColumnName];
            DateOnly date = DateOnly.ParseExact((String)dataReader[dateColumnName], transactSql23DateStyle, DateTimeFormatInfo.InvariantInfo);

            return new StockPriceGridOuterKeyProperties(tag, dataSource, date);
        }

        /// <inheritdoc/>
        protected override StockPriceGridItemPTO GetGridItemPTOFromDataReader(IDataReader dataReader)
        {
            Int64 id = (Int64)dataReader[idColumnName];
            StockPriceGridOuterKeyProperties outerKeyProperties = GetOuterKeyPropertiesFromDataReader(dataReader);
            String company = (String)dataReader[companyColumnName];
            Decimal price = (Decimal)dataReader[priceColumnName];
            (DateTime transactionFrom, DateTime transactionTo) = GetTransactionFromAndToDateTimesFromDataReader(dataReader);

            return new StockPriceGridItemPTO(id, outerKeyProperties.Tag, outerKeyProperties.DataSource, outerKeyProperties.Date, company, price, transactionFrom, transactionTo);
        }

        /// <inheritdoc/>
        protected override void AddGridCommonKeyPropertyQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, GridCommonKeyProperties gridCommonKeyProperties)
        {
            sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridCommonKeyProperties.Tag);
        }

        /// <inheritdoc/>
        protected override void AddGridOuterKeyPropertyQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            AddGridCommonKeyPropertyQueryParameters(sqlCommandShim, command, new GridCommonKeyProperties(gridOuterKeyProperties.Tag));
            sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
            sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactSql23DateStyle));
        }

        /// <inheritdoc/>
        protected override void AddGridItemQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, StockPrice entity)
        {
            sqlCommandShim.AddParameter(command, companyParameterName, SqlDbType.NVarChar, entity.Company);
            sqlCommandShim.AddParameter(command, priceParameterName, SqlDbType.Money, entity.Price);
        }

        /// <inheritdoc/>
        protected override StockPriceGridOuterKeyProperties ExtractOuterKeyPropertiesFromGridItem(StockPriceGridItem gridItem)
        {
            return new StockPriceGridOuterKeyProperties(gridItem.Tag, gridItem.DataSource, gridItem.Date);
        }

        /// <inheritdoc/>
        protected override StockPriceGridItem ConvertEntityAndOuterKeyPropertiesToGridItem(StockPriceGridOuterKeyProperties gridOuterKeyProperties, StockPrice entity)
        {
            return new StockPriceGridItem(gridOuterKeyProperties.Tag, gridOuterKeyProperties.DataSource, gridOuterKeyProperties.Date, entity.Company, entity.Price);
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
                        {dataSourceColumnName}, 
                        CONVERT(nvarchar(30), [{dateColumnName}], 23) AS [{dateColumnName}], 
                        [{versionColumnName}], 
                        CONVERT(nvarchar(30), {transactionTimestampColumnName}, 126) AS {transactionTimestampColumnName} 
                FROM    {GridTableName} ";
            }
        }

        #pragma warning disable 1591

        protected void ValidateBufferSizeLimitParameter(Int32 bufferSizeLimit)
        {
            if (bufferSizeLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(bufferSizeLimit), bufferSizeLimit, $"Parameter '{nameof(bufferSizeLimit)}' with value {bufferSizeLimit} must be greater than 0.");
        }

        #pragma warning restore 1591

        #endregion

        #region Nested Classes

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which persists <see cref="StockPriceGridItem"/> objects to a SQL Server database in bulk, by buffering objects received through the <see cref="BulkAddEmitter.Emit(StockPriceGridItem)"/> method, and writing those objects to SQL Server when the buffer reaches a specified size.
        /// </summary>
        protected class BulkAddEmitter : BulkPersistenceStoredProcedureEmitterBase<StockPriceGridItem>
        {
            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkAddEmitter class.
            /// </summary>
            /// <param name="transactionTimestamp">The timestamp representing when the objects emitted were either inserted or deleted.</param>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItem"/> objects to hold in the buffer.</param>
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

            /// <summary>
            /// Persists all buffered items and then clears the buffer.
            /// </summary>
            protected override void Persist()
            {
                bulkInsertStagingTable.Rows.Clear();
                foreach (StockPriceGridItem currentGridItem in buffer)
                {
                    var row = bulkInsertStagingTable.NewRow();
                    row[gridItemData1ColumnName] = currentGridItem.Tag;
                    row[gridItemData2ColumnName] = currentGridItem.DataSource;
                    row[gridItemData3ColumnName] = currentGridItem.Date.ToString(transactSql23DateStyle);
                    row[gridItemData4ColumnName] = currentGridItem.Company;
                    row[gridItemData5ColumnName] = currentGridItem.Price.ToString(CultureInfo.InvariantCulture);
                    bulkInsertStagingTable.Rows.Add(row);
                }
                var parameters = new List<SqlParameter>()
                {
                    CreateSqlParameterWithValue("@GridItems", SqlDbType.Structured, bulkInsertStagingTable),
                    CreateSqlParameterWithValue(transactionTimestampParameterName, SqlDbType.DateTime2, transactionTimestamp)
                };
                ExecuteStoredProcedureWithDeadlockRetry("BulkInsertStockPrices", parameters);
                bulkInsertStagingTable.Rows.Clear();
                buffer.Clear();
            }

            #endregion
        }

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which soft deletes <see cref="StockPriceGridItemPTO"/> objects in a SQL Server database in bulk, by buffering objects received through the <see cref="BulkDeleteEmitter.Emit(StockPriceGridItem)"/> method, and deleting those objects from SQL Server when the buffer reaches a specified size.
        /// </summary>
        protected class BulkDeleteEmitter : BulkPersistenceStoredProcedureEmitterBase<StockPriceGridItemPTO>
        {
            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkDeleteEmitter class.
            /// </summary>
            /// <param name="transactionTimestamp">The timestamp representing when the objects emitted were either inserted or deleted.</param>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItemPTO"/> objects to hold in the buffer.</param>
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

            #region Private/Protected Methods

            /// <summary>
            /// Deletes all buffered items and then clears the buffer.
            /// </summary>
            protected override void Persist()
            {
                foreach (StockPriceGridItemPTO currentGridItem in buffer)
                {
                    var row = bulkDeleteStagingTable.NewRow();
                    row[idColumnName] = currentGridItem.Id;
                    bulkDeleteStagingTable.Rows.Add(row);
                }
                var parameters = new List<SqlParameter>()
                {
                    CreateSqlParameterWithValue("@GridItemIds", SqlDbType.Structured, bulkDeleteStagingTable),
                    CreateSqlParameterWithValue(transactionTimestampParameterName, SqlDbType.DateTime2, transactionTimestamp)
                };
                ExecuteStoredProcedureWithDeadlockRetry("BulkDeleteStockPrices", parameters);
                bulkDeleteStagingTable.Rows.Clear();
                buffer.Clear();
            }

            #endregion
        }

        #endregion
    }
}
