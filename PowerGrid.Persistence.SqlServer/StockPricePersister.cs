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
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using ApplicationLogging;
using ApplicationMetrics;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and writes <see cref="StockPrice"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class StockPricePersister : PersisterBase<StockPrice, GridCommonKeyProperties, StockPriceGridOuterKeyProperties, StockPriceGridItem, StockPriceGridItemPTO>
    {
        #region TEMP StockPricePersisterRefactoring

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

        /// <inheritdoc/>
        protected override String GridItemTableName
        {
            get { return "StockPrices"; }
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
                FROM    StockPriceGrids 
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
                SELECT  [{versionColumnName}] AS [{versionColumnName}], 
                        CONVERT(nvarchar(30), {transactionTimestampColumnName} , 126) AS {transactionTimestampColumnName}
                FROM    StockPriceGrids 
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
                FROM    StockPriceGrids 
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
                       CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                       CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM   StockPrices 
                WHERE  {tagColumnName} = {tagParameterName}
                  AND  {dataSourceColumnName} = {dataSourceParameterName}
                  AND  [{dateColumnName}] = CONVERT(date, {dateParameterName}, 23) 
                  AND  CONVERT(datetime2, {transactionTimestampParameterName}, 126) BETWEEN TransactionFrom AND TransactionTo
                ORDER  BY {companyColumnName} 
                COLLATE {transactSqlCollation};";
            }
        }

        /// <inheritdoc/>
        protected override String GridInsertStatementSqlText 
        { 
            get
            {
                return $@"
                INSERT 
                INTO    StockPriceGrids 
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
                INTO    StockPrices 
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
        protected override StockPriceGridItemPTO GetGridItemPTOFromDataReader(IDataReader dataReader)
        {
            Int64 id = (Int64)dataReader[idColumnName];
            String tag = (String)dataReader[tagColumnName];
            String dataSource = (String)dataReader[dataSourceColumnName];
            DateOnly date = DateOnly.ParseExact((String)dataReader[dateColumnName], transactSql23DateStyle, DateTimeFormatInfo.InvariantInfo);
            String company = (String)dataReader[companyColumnName];
            Decimal price = (Decimal)dataReader[priceColumnName];
            (DateTime transactionFrom, DateTime transactionTo) = GetTransactionFromAndToDateTimesFromDataReader(dataReader);

            return new StockPriceGridItemPTO(id, tag, dataSource, date, company, price, transactionFrom, transactionTo);
        }

        /// <inheritdoc/>
        protected override void AddGridOuterKeyPropertyQueryParameters(ISqlCommandShim sqlCommandShim, SqlCommand command, StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
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

        #endregion

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        public StockPricePersister
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
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public StockPricePersister
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
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister class.
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
        public StockPricePersister
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
        public override (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(StockPriceGridOuterKeyProperties gridOuterKeyProperties, IList<StockPrice> items)
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
                    Action<SqlConnection, SqlTransaction, StockPriceGridItem, DateTime> addedItemEmitterOperationAction = (SqlConnection connection, SqlTransaction transaction, StockPriceGridItem addedStockPrice, DateTime transactionDateTime) =>
                    {
                        InsertGridItem(connection, transaction, addedStockPrice, transactionDateTime);
                    };
                    DataBaseOperationEmitter<StockPriceGridItem> addedItemEmitter = new(writeConnection, transaction, transactionTimestamp, addedItemEmitterOperationAction);
                    Action<SqlConnection, SqlTransaction, Tuple<StockPriceGridItemPTO, StockPriceGridItem>, DateTime> updatedItemsEmitterOperationAction = (SqlConnection connection, SqlTransaction transaction, Tuple<StockPriceGridItemPTO, StockPriceGridItem> updatedStockPrices, DateTime transactionDateTime) =>
                    {
                        UpdateGridItem(connection, transaction, updatedStockPrices.Item1, updatedStockPrices.Item2, transactionDateTime);
                    };
                    DataBaseOperationEmitter<Tuple<StockPriceGridItemPTO, StockPriceGridItem>> updatedItemsEmitter = new(writeConnection, transaction, transactionTimestamp, updatedItemsEmitterOperationAction);
                    Action<SqlConnection, SqlTransaction, StockPriceGridItemPTO, DateTime> deletedItemEmitterOperationAction = (SqlConnection connection, SqlTransaction transaction, StockPriceGridItemPTO deletedStockPrice, DateTime transactionDateTime) =>
                    {
                        DeleteGridItem(connection, transaction, deletedStockPrice, transactionDateTime);
                    };
                    DataBaseOperationEmitter<StockPriceGridItemPTO> deletedItemEmitter = new(writeConnection, transaction, transactionTimestamp, deletedItemEmitterOperationAction);
                    GridComparer<StockPriceGridItemPTO, StockPriceGridItem> gridComparer = new(addedItemEmitter, updatedItemsEmitter, deletedItemEmitter);

                    // Setup IEnumerable 'chains' 
                    //   Create a GridContentsValidator to check that the price is >= 0
                    GridContentsValidator<StockPrice> newItemValidator = new();
                    Action<StockPrice> newItemValidationAction = (StockPrice stockPrice) =>
                    {
                        if (stockPrice.Price < 0)
                            throw new GridContentsValidationException<StockPrice>($"{typeof(StockPrice).Name} with {gridOuterKeyProperties.ToString()}, and {nameof(StockPrice.Company)} '{stockPrice.Company}' has negative {nameof(StockPrice.Price)} {stockPrice.Price}.", stockPrice);
                    };
                    GridContentsDuplicateChecker<StockPrice> newItemsDuplicateChecker = new();

                    // Order of below chain is 1 validate, 2 order, 3 dup check
                    IEnumerable<StockPrice> newGridContents = newItemsDuplicateChecker.CheckForDuplicates
                    (
                        newItemValidator.ValidateItems
                        (
                            items,
                            newItemValidationAction
                        ).Order(Comparer<StockPrice>.Create
                        (
                            (StockPrice first, StockPrice second) => { return first.KeyCompareTo(second); }
                        ))
                    );
                    IEnumerable<StockPriceGridItem> ConvertStockPricesToStockPriceGridItems(IEnumerable<StockPrice> items)
                    {
                        foreach (StockPrice currentItem in items)
                        {
                            yield return new StockPriceGridItem(gridOuterKeyProperties.Tag, gridOuterKeyProperties.DataSource, gridOuterKeyProperties.Date, currentItem.Company, currentItem.Price);
                        } 
                    }

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
                            throw new Exception($"Failed to read existing stock price grid from SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactSql126DateStyle)}'.", e);
                        }
                        {
                            try
                            {
                                comparisonStatistics = gridComparer.Compare(existingGridContents, ConvertStockPricesToStockPriceGridItems(newGridContents));
                            }
                            catch (Exception e)
                            {
                                Exception compareException = new($"Failed to compare new stock price grid to existing grid in SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactSql126DateStyle)}'.", e); 
                                try
                                {
                                    // As per https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqltransaction.rollback?view=sqlclient-dotnet-core-6.1, exception can occur on rollback
                                    sqlTransactionShim.Rollback(transaction);
                                }
                                catch (Exception rollbackException)
                                {
                                    throw new AggregateException("Failed to rollback transaction after exception comparing stock price grid to existing data.", rollbackException, compareException);
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
                        throw new Exception("Failed to persist grid to SQL Server.", e);
                    }
                }
                TeardownConnection(readConnection);
                TeardownConnection(writeConnection);

                return (gridVersion, comparisonStatistics);
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<StockPriceGridItemPTO> GetGrid(StockPriceGridOuterKeyProperties gridOuterKeyProperties, Int32 version)
        {
            if (version < 1)
                throw new ArgumentOutOfRangeException(nameof(version), $"Parameter '{nameof(version)}' with value {version} must be greater than 0.");

            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    sqlConnectionShim.Open(connection);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to connect to SQL Server.", e);
                }
                DateTime transactionTimestamp = GetGridTransactionTimestamp(connection, gridOuterKeyProperties, version);

                foreach (StockPriceGridItemPTO currentItem in GetGrid(connection, gridOuterKeyProperties, transactionTimestamp))
                {
                    yield return currentItem;
                }
            } 
        }

        /// <inheritdoc/>
        public override IList<GridVersionAndTransactionTimestamp> GetGridDetails(StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            String query = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = {tagParameterName} 
              AND   DataSource = {dataSourceParameterName} 
              AND   [Date] = CONVERT(date, {dateParameterName}, 23);
            ";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand())
            {
                try
                {
                    PrepareConnection(connection);
                    sqlConnectionShim.Open(connection);
                    sqlCommandShim.SetCommandText(command, query);
                    PrepareCommand(connection, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                    sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactSql23DateStyle));
                    List<GridVersionAndTransactionTimestamp> returnList = new();

                    using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                    {
                        while (dataReader.Read())
                        {
                            Int32 version = (Int32)dataReader["Version"];
                            DateTime transactionTimestamp = DateTime.ParseExact((String)dataReader["TransactionTimestamp"], transactSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                            transactionTimestamp = DateTime.SpecifyKind(transactionTimestamp, DateTimeKind.Utc);
                            returnList.Add(new GridVersionAndTransactionTimestamp(version, transactionTimestamp));
                        }
                    }
                    sqlConnectionShim.Close(connection);

                    return returnList;
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to read grid details for {gridOuterKeyProperties.ToString()} from SQL Server.", e);
                }
            }
        }

        /// <inheritdoc/>
        public override IList<Tuple<StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> GetGridDetails(GridCommonKeyProperties gridCommonKeyProperties)
        {
            const String tagParameterName = "@Tag";
            String query = @$"
            SELECT  DataSource, 
                    CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                    [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp 
            FROM    StockPriceGrids 
            WHERE   Tag = {tagParameterName};
            ";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand())
            {
                try
                {
                    PrepareConnection(connection);
                    sqlConnectionShim.Open(connection);
                    sqlCommandShim.SetCommandText(command, query);
                    PrepareCommand(connection, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridCommonKeyProperties.Tag);
                    List<Tuple<StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> returnList = new();

                    using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                    {
                        while (dataReader.Read())
                        {
                            String dataSource = (String)dataReader["DataSource"];
                            DateOnly date = DateOnly.ParseExact((String)dataReader["Date"], transactSql23DateStyle, DateTimeFormatInfo.InvariantInfo);
                            Int32 version = (Int32)dataReader["Version"];
                            DateTime transactionTimestamp = DateTime.ParseExact((String)dataReader["TransactionTimestamp"], transactSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                            transactionTimestamp = DateTime.SpecifyKind(transactionTimestamp, DateTimeKind.Utc);
                            returnList.Add(Tuple.Create(new StockPriceGridOuterKeyProperties(gridCommonKeyProperties.Tag, dataSource, date), new GridVersionAndTransactionTimestamp(version, transactionTimestamp)));
                        }
                    }
                    sqlConnectionShim.Close(connection);

                    return returnList;
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to read grid details for {gridCommonKeyProperties.ToString()} from SQL Server.", e);
                }
            }
        }

        /// <inheritdoc/>
        public override void SoftDeleteLatestGrid(StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            const String currentDateTimeParameterName = "@CurrentDateTime";
            const String deleteDateTimeParameterName = "@DeleteDateTime";
            String deleteStatement = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, {deleteDateTimeParameterName}, 126)
            WHERE   Tag = {tagParameterName} 
              AND   DataSource = {dataSourceParameterName} 
              AND   [Date] = CONVERT(date, {dateParameterName}, 23) 
              AND   CONVERT(datetime2, {currentDateTimeParameterName}, 126) BETWEEN TransactionFrom AND TransactionTo;
            ";

            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    PrepareConnection(connection);
                    sqlConnectionShim.Open(connection);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to connect to SQL Server.", e);
                }
                (Int32 version, DateTime transactionTimestamp) = GetLatestGridVersion(connection, gridOuterKeyProperties);
                if (version == 0)
                {
                    throw new Exception($"Stock price grid for {gridOuterKeyProperties.ToString()} does not exist.");
                }

                using (var command = new SqlCommand())
                using (SqlTransaction transaction = sqlConnectionShim.BeginTransaction(connection))
                {
                    try
                    {
                        DateTime deleteTimestamp = dateTimeProvider.UtcNow();
                        sqlCommandShim.SetCommandText(command, deleteStatement);
                        PrepareCommand(connection, transaction, command);
                        sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                        sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                        sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactSql23DateStyle));
                        sqlCommandShim.AddParameter(command, currentDateTimeParameterName, SqlDbType.NVarChar, deleteTimestamp.ToString(transactSql126DateStyle));
                        sqlCommandShim.AddParameter(command, deleteDateTimeParameterName, SqlDbType.NVarChar, deleteTimestamp.AddTicks(-1).ToString(transactSql126DateStyle));
                        ExecuteNonQueryWithDeadlockRetry(connection, transaction, command);
                        sqlTransactionShim.Commit(transaction);
                        sqlConnectionShim.Close(connection);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Failed to delete latest grid items for {gridOuterKeyProperties.ToString()} in SQL Server.", e);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override void HardDeleteGrids(StockPriceGridOuterKeyProperties gridOuterKeyProperties)
        {
            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            String stockPriceGridsDeleteStatement = @$"
            DELETE 
            FROM    StockPriceGrids 
            WHERE   Tag = {tagParameterName} 
              AND   DataSource = {dataSourceParameterName} 
              AND   [Date] = CONVERT(date, {dateParameterName}, 23);
            ";
            String stockPricesDeleteStatement = @$"
            DELETE 
            FROM    StockPrices 
            WHERE   Tag = {tagParameterName} 
              AND   DataSource = {dataSourceParameterName} 
              AND   [Date] = CONVERT(date, {dateParameterName}, 23);
            ";

            using (var connection = new SqlConnection(connectionString))
            using (var stockPriceGridsDeleteCommand = new SqlCommand())
            using (var stockPricesDeleteCommand = new SqlCommand())
            {
                try
                {
                    PrepareConnection(connection);
                    sqlConnectionShim.Open(connection);
                    using (SqlTransaction transaction = sqlConnectionShim.BeginTransaction(connection))
                    {
                        sqlCommandShim.SetCommandText(stockPriceGridsDeleteCommand, stockPriceGridsDeleteStatement);
                        PrepareCommand(connection, transaction, stockPriceGridsDeleteCommand);
                        sqlCommandShim.AddParameter(stockPriceGridsDeleteCommand, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                        sqlCommandShim.AddParameter(stockPriceGridsDeleteCommand, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                        sqlCommandShim.AddParameter(stockPriceGridsDeleteCommand, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactSql23DateStyle));
                        ExecuteNonQueryWithDeadlockRetry(connection, transaction, stockPriceGridsDeleteCommand);
                        sqlCommandShim.SetCommandText(stockPricesDeleteCommand, stockPricesDeleteStatement);
                        PrepareCommand(connection, transaction, stockPricesDeleteCommand);
                        sqlCommandShim.AddParameter(stockPricesDeleteCommand, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                        sqlCommandShim.AddParameter(stockPricesDeleteCommand, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                        sqlCommandShim.AddParameter(stockPricesDeleteCommand, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactSql23DateStyle));
                        ExecuteNonQueryWithDeadlockRetry(connection, transaction, stockPricesDeleteCommand);
                        sqlTransactionShim.Commit(transaction);
                        sqlConnectionShim.Close(connection);
                    }

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to delete grids for {gridOuterKeyProperties.ToString()} in SQL Server.", e);
                }
            }
        }

        /// <inheritdoc/>
        public override void HardDeleteGrids(GridCommonKeyProperties gridCommonKeyProperties)
        {
            const String tagParameterName = "@Tag";
            String stockPriceGridsDeleteStatement = @$"
            DELETE 
            FROM    StockPriceGrids 
            WHERE   Tag = {tagParameterName};
            ";
            String stockPricesDeleteStatement = @$"
            DELETE 
            FROM    StockPrices 
            WHERE   Tag = {tagParameterName};
            ";

            using (var connection = new SqlConnection(connectionString))
            using (var stockPriceGridsDeleteCommand = new SqlCommand())
            using (var stockPricesDeleteCommand = new SqlCommand())
            {
                try
                {
                    PrepareConnection(connection);
                    sqlConnectionShim.Open(connection);
                    using (SqlTransaction transaction = sqlConnectionShim.BeginTransaction(connection))
                    {
                        sqlCommandShim.SetCommandText(stockPriceGridsDeleteCommand, stockPriceGridsDeleteStatement);
                        PrepareCommand(connection, transaction, stockPriceGridsDeleteCommand);
                        sqlCommandShim.AddParameter(stockPriceGridsDeleteCommand, tagParameterName, SqlDbType.NVarChar, gridCommonKeyProperties.Tag);
                        ExecuteNonQueryWithDeadlockRetry(connection, transaction, stockPriceGridsDeleteCommand);
                        sqlCommandShim.SetCommandText(stockPricesDeleteCommand, stockPricesDeleteStatement);
                        PrepareCommand(connection, transaction, stockPricesDeleteCommand);
                        sqlCommandShim.AddParameter(stockPricesDeleteCommand, tagParameterName, SqlDbType.NVarChar, gridCommonKeyProperties.Tag);
                        ExecuteNonQueryWithDeadlockRetry(connection, transaction, stockPricesDeleteCommand);
                        sqlTransactionShim.Commit(transaction);
                        sqlConnectionShim.Close(connection);
                    }

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to delete grids for {gridCommonKeyProperties.ToString()} in SQL Server.", e);
                }
            }
        }

        #region Private/Protected Methods

        #endregion
    }
}
