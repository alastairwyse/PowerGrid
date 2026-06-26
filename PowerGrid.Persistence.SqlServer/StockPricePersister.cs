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
using ApplicationMetrics;
using ApplicationLogging;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and writes <see cref="StockPrice"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class StockPricePersister : PersisterBase<StockPrice, GridCommonKeyProperties, StockPriceOuterKeyProperties, StockPriceGridItem, StockPriceGridItemPTO>
    {
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 23 date and time style</see>.</summary>
        protected const String transactionSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        protected const String transactionSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";
        /// <summary>The maximum possible <see cref="DateTime"/> value to use as the upper bound for validity period in the persisted temporal model.</summary>
        protected readonly DateTime temporalMaximumDateTime = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

        /// <summary>The string to use to connect to the SQL Server database.</summary>
        protected String connectionString;
        /// <summary>The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</summary>
        protected Int32 operationTimeout;
        /// <summary>Provider for the current date and time.</summary>
        protected IDateTimeProvider dateTimeProvider;
        /// <summary>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</summary>
        protected ISqlConnectionShim sqlConnectionShim;
        /// <summary>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlTransaction"/> class.</summary>
        protected ISqlTransactionShim sqlTransactionShim;
        /// <summary>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlCommand"/> class.</summary>
        protected ISqlCommandShim sqlCommandShim;
        /// <summary>A set of SQL Server database engine error numbers which denote a transient fault.</summary>
        /// <see href="https://docs.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors?view=sql-server-ver16"/>
        /// <see href="https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql"/>
        protected List<Int32> sqlServerTransientErrorNumbers;
        /// <summary>The retry logic to use when connecting to and executing against the SQL Server database.</summary>
        protected SqlRetryLogicOption sqlRetryLogicOption;
        /// <summary>Maps <see cref="SessionDeadlockPriority"/> values to their equivalent SQL Server string value.</summary>
        protected IDictionary<SessionDeadlockPriority, String> deadlockPriorityToStringValueMap;
        /// <summary>The action to invoke if an action is retried due to a transient error.</summary>
        protected EventHandler<SqlRetryingEventArgs> connectionRetryAction;

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
            : base(logger)
        {
            ThrowExceptionIfConnectionStringParameterNullOrWhitespace(nameof(connectionString), connectionString);
            ThrowExceptionIfOperationTimeoutParameterLessThanZero(nameof(operationTimeout), operationTimeout);
            if (retryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(retryCount), $"Parameter '{nameof(retryCount)}' with value {retryCount} cannot be less than 0.");
            if (retryCount > 59)
                throw new ArgumentOutOfRangeException(nameof(retryCount), $"Parameter '{nameof(retryCount)}' with value {retryCount} cannot be greater than 59.");
            if (retryInterval < 0)
                throw new ArgumentOutOfRangeException(nameof(retryInterval), $"Parameter '{nameof(retryInterval)}' with value {retryInterval} cannot be less than 0.");
            if (retryInterval > 120)
                throw new ArgumentOutOfRangeException(nameof(retryInterval), $"Parameter '{nameof(retryInterval)}' with value {retryInterval} cannot be greater than 120.");
            if (operationTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(operationTimeout), $"Parameter '{nameof(operationTimeout)}' with value {operationTimeout} cannot be less than 0.");

            this.connectionString = connectionString;
            this.operationTimeout = operationTimeout;
            dateTimeProvider = new DefaultDateTimeProvider();
            sqlConnectionShim = new DefaultSqlConnectionShim();
            sqlTransactionShim = new DefaultSqlTransactionShim();
            sqlCommandShim = new DefaultSqlCommandShim();
            // Setup retry logic
            sqlServerTransientErrorNumbers = GenerateSqlServerTransientErrorNumbers();
            sqlRetryLogicOption = new SqlRetryLogicOption();
            sqlRetryLogicOption.NumberOfTries = retryCount + 1;  // According to documentation... "1 means to execute one time and if an error is encountered, don't retry"
            sqlRetryLogicOption.MinTimeInterval = TimeSpan.FromSeconds(0);
            sqlRetryLogicOption.MaxTimeInterval = TimeSpan.FromSeconds(120);
            sqlRetryLogicOption.DeltaTime = TimeSpan.FromSeconds(retryInterval);
            sqlRetryLogicOption.TransientErrors = sqlServerTransientErrorNumbers;
            deadlockPriorityToStringValueMap = new Dictionary<SessionDeadlockPriority, String>()
            {
                { SessionDeadlockPriority.Low, "LOW"},
                { SessionDeadlockPriority.Normal, "NORMAL"},
                { SessionDeadlockPriority.High, "HIGH"},
            };
            deadlockPriorityToStringValueMap = deadlockPriorityToStringValueMap.ToFrozenDictionary();
            connectionRetryAction = (Object sender, SqlRetryingEventArgs eventArgs) =>
            {
                Exception lastException = eventArgs.Exceptions[eventArgs.Exceptions.Count - 1];
                Int32 retryDelayInSeconds = eventArgs.Delay.Seconds;
                if (typeof(SqlException).IsAssignableFrom(lastException.GetType()) == true)
                {
                    var se = (SqlException)lastException;
                    logger.Log(this, LogLevel.Warning, $"SQL Server error with number {se.Number} occurred when executing command.  Retrying in {retryDelayInSeconds} seconds (retry {eventArgs.RetryCount} of {retryCount}).", se);
                }
                else
                {
                    logger.Log(this, LogLevel.Warning, $"Exception occurred when executing command.  Retrying in {retryDelayInSeconds} seconds (retry {eventArgs.RetryCount} of {retryCount}).", lastException);
                }
                metricLogger.Increment(new SqlCommandExecutionRetried());
            };
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
            : this(connectionString, retryCount, retryInterval, operationTimeout, logger)
        {
            this.metricLogger = metricLogger;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="mockDateTimeProvider">A mock <see cref="IDateTimeProvider"/></param>
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
        ) : this(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.sqlConnectionShim = sqlConnectionShim;
            this.sqlTransactionShim = sqlTransactionShim;
            this.sqlCommandShim = sqlCommandShim;
        }

        /// <inheritdoc/>
        public override (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(StockPriceOuterKeyProperties gridOuterKeyProperties, IList<StockPrice> items)
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
                            throw new Exception($"Failed to read existing stock price grid from SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactionSql126DateStyle)}'.", e);
                        }
                        {
                            try
                            {
                                comparisonStatistics = gridComparer.Compare(existingGridContents, ConvertStockPricesToStockPriceGridItems(newGridContents));
                            }
                            catch (Exception e)
                            {
                                Exception compareException = new($"Failed to compare new stock price grid to existing grid in SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactionSql126DateStyle)}'.", e); 
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
        public override IEnumerable<StockPriceGridItemPTO> GetGrid(StockPriceOuterKeyProperties gridOuterKeyProperties, Int32 version)
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
        public override IList<GridVersionAndTransactionTimestamp> GetGridDetails(StockPriceOuterKeyProperties gridOuterKeyProperties)
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
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactionSql23DateStyle));
                    List<GridVersionAndTransactionTimestamp> returnList = new();

                    using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                    {
                        while (dataReader.Read())
                        {
                            Int32 version = (Int32)dataReader["Version"];
                            DateTime transactionTimestamp = DateTime.ParseExact((String)dataReader["TransactionTimestamp"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
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
        public override IList<Tuple<StockPriceOuterKeyProperties, GridVersionAndTransactionTimestamp>> GetGridDetails(GridCommonKeyProperties gridCommonKeyProperties)
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
                    List<Tuple<StockPriceOuterKeyProperties, GridVersionAndTransactionTimestamp>> returnList = new();

                    using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                    {
                        while (dataReader.Read())
                        {
                            String dataSource = (String)dataReader["DataSource"];
                            DateOnly date = DateOnly.ParseExact((String)dataReader["Date"], transactionSql23DateStyle, DateTimeFormatInfo.InvariantInfo);
                            Int32 version = (Int32)dataReader["Version"];
                            DateTime transactionTimestamp = DateTime.ParseExact((String)dataReader["TransactionTimestamp"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                            transactionTimestamp = DateTime.SpecifyKind(transactionTimestamp, DateTimeKind.Utc);
                            returnList.Add(Tuple.Create(new StockPriceOuterKeyProperties(gridCommonKeyProperties.Tag, dataSource, date), new GridVersionAndTransactionTimestamp(version, transactionTimestamp)));
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
        public override void SoftDeleteLatestGrid(StockPriceOuterKeyProperties gridOuterKeyProperties)
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
                        sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactionSql23DateStyle));
                        sqlCommandShim.AddParameter(command, currentDateTimeParameterName, SqlDbType.NVarChar, deleteTimestamp.ToString(transactionSql126DateStyle));
                        sqlCommandShim.AddParameter(command, deleteDateTimeParameterName, SqlDbType.NVarChar, deleteTimestamp.AddTicks(-1).ToString(transactionSql126DateStyle));
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
        public override void HardDeleteGrids(StockPriceOuterKeyProperties gridOuterKeyProperties)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void HardDeleteGrids(GridCommonKeyProperties gridCommonKeyProperties)
        {
            throw new NotImplementedException();
        }

        #region Private/Protected Methods

        /// <summary>
        /// Gets the latest stock price grid version for the specified parameters.
        /// </summary>
        /// <param name="connection">The connection to use to retrieve the grid version.</param>
        /// <param name="gridOuterKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of the grid version to retrieve.</param>
        /// <returns>A tuple containing: the version number of the latest grid (or 0 if no grids exist for the specified parameters), and the transaction timestamp of the grid (or <see cref="DateTime.MinValue"/> if no grids exist for the specified parameters).</returns>
        protected (Int32 Version, DateTime TransactionTimestamp) GetLatestGridVersion(SqlConnection connection, StockPriceOuterKeyProperties gridOuterKeyProperties)
        {
            // REFACTORING: 
            //   General steps here in base case
            //   Query can be abstract (or tablename and parameters... other parts of the insertStatement should be common)
            //   Use AppAccess SqlServerPersisterUtilities and ReadQueryGeneratorBase classes for influence in how to split platform-agnostic SQL into base classes

            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            String query = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = {tagParameterName} 
              AND   DataSource = {dataSourceParameterName} 
              AND   [Date] = CONVERT(date, {dateParameterName}, 23) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   Tag = {tagParameterName} 
                        AND   DataSource = {dataSourceParameterName} 
                        AND   [Date] = CONVERT(date, {dateParameterName}, 23) 
                    );
            ";

            using (var command = new SqlCommand())
            {
                try
                {
                    sqlCommandShim.SetCommandText(command, query);
                    PrepareCommand(connection, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                    sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactionSql23DateStyle));
                    Int32 latestGridVersionNumber = 0;
                    DateTime latestGridTransactionTimestamp = DateTime.MinValue.ToUniversalTime();

                    using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                    {
                        Boolean alreadyReadResult = false;
                        while (dataReader.Read())
                        {
                            if (alreadyReadResult == true)
                            {
                                throw new Exception($"Read multiple results from SQL Server when attempting to retrieve latest stock price grid version for {gridOuterKeyProperties.ToString()}.");
                            }
                            latestGridVersionNumber = (Int32)dataReader["Version"];
                            latestGridTransactionTimestamp = DateTime.ParseExact((String)dataReader["TransactionTimestamp"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                            latestGridTransactionTimestamp = DateTime.SpecifyKind(latestGridTransactionTimestamp, DateTimeKind.Utc);
                            alreadyReadResult = true;
                        }
                    }

                    return (latestGridVersionNumber, latestGridTransactionTimestamp);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to read latest stock price grid version for {gridOuterKeyProperties.ToString()} from SQL Server.", e);
                }
            }
        }

        /// <summary>
        /// Gets the transaction timestamp for the stock price grid with the specified parameters and version.
        /// </summary>
        /// <param name="connection">The connection to use to retrieve the grid version.</param>
        /// <param name="gridOuterKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of the grid to retrieve.</param>
        /// <param name="version">The version number of the grid.</param>
        /// <returns>The transaction timestamp of the grid.</returns>
        /// <exception cref="Exception">A grid with the specified parameters does not exist.</exception>
        protected DateTime GetGridTransactionTimestamp(SqlConnection connection, StockPriceOuterKeyProperties gridOuterKeyProperties, Int32 version)
        {
            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            const String versionParameterName = "@Version";
            String query = @$"
            SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = {tagParameterName} 
              AND   DataSource = {dataSourceParameterName} 
              AND   [Date] = CONVERT(date, {dateParameterName}, 23) 
              AND   [Version] = {versionParameterName};
            ";

            using (var command = new SqlCommand())
            {
                DateTime transactionTimestamp = DateTime.MinValue;
                Boolean alreadyReadResult = false;
                try
                {
                    sqlCommandShim.SetCommandText(command, query);
                    PrepareCommand(connection, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                    sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactionSql23DateStyle));
                    sqlCommandShim.AddParameter(command, versionParameterName, SqlDbType.Int, version);
                    using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                    {
                        while (dataReader.Read())
                        {
                            if (alreadyReadResult == true)
                            {
                                throw new Exception($"Read multiple results from SQL Server when attempting to retrieve stock price grid version for {gridOuterKeyProperties.ToString()}, and version {version}.");
                            }
                            transactionTimestamp = DateTime.ParseExact((String)dataReader["TransactionTimestamp"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                            alreadyReadResult = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to read stock price grid for {gridOuterKeyProperties.ToString()}, and version {version} from SQL Server.", e);
                }
                if (alreadyReadResult == false)
                {
                    throw new Exception($"Stock price grid for {gridOuterKeyProperties.ToString()}, and version {version} did not exist.");
                }

                return transactionTimestamp;
            }
        }

        /// <summary>
        /// Gets the contents of a stock price grid.
        /// </summary>
        /// <param name="connection">The connection to use to retrieve the grid.</param>
        /// <param name="gridOuterKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of the grid to retrieve.</param>
        /// <param name="transactionTimestamp">The transaction timestamp when the grid was created.</param>
        /// <returns>The items in the grid.</returns>
        protected IEnumerable<StockPriceGridItemPTO> GetGrid(SqlConnection connection, StockPriceOuterKeyProperties gridOuterKeyProperties, DateTime transactionTimestamp)
        {
            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            const String transactionTimestampParameterName = "@TransactionTimestamp";
            String query = @$"
            SELECT Id, 
                   Tag, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  Tag = {tagParameterName}
              AND  DataSource = {dataSourceParameterName}
              AND  [Date] = CONVERT(date, {dateParameterName}, 23) 
              AND  CONVERT(datetime2, {transactionTimestampParameterName}, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY Tag, 
                      DataSource, 
                      [Date], 
                      Company;
            ";

            using (var command = new SqlCommand())
            {
                IDataReader dataReader = null;
                try
                {
                    sqlCommandShim.SetCommandText(command, query);
                    PrepareCommand(connection, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                    sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactionSql23DateStyle));
                    sqlCommandShim.AddParameter(command, transactionTimestampParameterName, SqlDbType.NVarChar, transactionTimestamp.ToString(transactionSql126DateStyle));
                    dataReader = sqlCommandShim.ExecuteReader(command);
                }
                catch (Exception e)
                {
                    if (dataReader != null)
                    {
                        dataReader.Dispose();
                    }
                    throw new Exception($"Failed to read stock price grid for {gridOuterKeyProperties.ToString()}, and transaction timestamp '{transactionTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}' from SQL Server.", e);
                }
                while (dataReader.Read())
                {
                    Int64 currentId = (Int64)dataReader["Id"];
                    String currentTag = (String)dataReader["Tag"];
                    String currentDataSource = (String)dataReader["DataSource"];
                    DateOnly currentDate = DateOnly.ParseExact((String)dataReader["Date"], transactionSql23DateStyle, DateTimeFormatInfo.InvariantInfo);
                    String currentCompany = (String)dataReader["Company"];
                    Decimal currentPrice = (Decimal)dataReader["Price"];
                    DateTime currentTransactionFrom = DateTime.ParseExact((String)dataReader["TransactionFrom"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                    currentTransactionFrom = DateTime.SpecifyKind(currentTransactionFrom, DateTimeKind.Utc);
                    DateTime currentTransactionTo = DateTime.ParseExact((String)dataReader["TransactionTo"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                    currentTransactionTo = DateTime.SpecifyKind(currentTransactionTo, DateTimeKind.Utc);

                    yield return new StockPriceGridItemPTO(currentId, currentTag, currentDataSource, currentDate, currentCompany, currentPrice, currentTransactionFrom, currentTransactionTo);
                }
                // Can't do below in a try/finally as it results in 'cannot yield a value in the body of a try block with a catch clause'
                dataReader.Dispose();
            }
        }

        // REFACTORING: 
        //   All below methods could go to base class, once PTO type is included in generic signature.  Just queries would defined in derived class.

        /// <summary>
        /// Adds an item to the current/latest grid.
        /// </summary>
        /// <param name="connection">The connection to use to insert.</param>
        /// <param name="transaction">The transaction to execute the add operation in.</param>
        /// <param name="item">The item to add.</param>
        /// <param name="insertDateTime">The UTC date and time the addition occurred.</param>
        protected void InsertGridItem(SqlConnection connection, SqlTransaction transaction, StockPriceGridItem item, DateTime insertDateTime)
        {
            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            const String companyParameterName = "@Company";
            const String priceParameterName = "@Price";
            const String insertDateTimeParameterName = "@InsertDateTime";
            const String temporalMaximumDateTimeParameterName = "@TemporalMaximumDateTime";
            String insertStatement = @$"
            INSERT 
            INTO    StockPrices 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        Company, 
                        Price, 
                        TransactionFrom, 
                        TransactionTo 
                    )
            VALUES  (
                        {tagParameterName}, 
                        {dataSourceParameterName}, 
                        CONVERT(date, {dateParameterName}, 23), 
                        {companyParameterName}, 
                        {priceParameterName}, 
                        CONVERT(datetime2, {insertDateTimeParameterName}, 126), 
                        CONVERT(datetime2, {temporalMaximumDateTimeParameterName}, 126)
                    );
            ";

            try
            {
                using (var command = new SqlCommand())
                {
                    sqlCommandShim.SetCommandText(command, insertStatement);
                    PrepareCommand(connection, transaction, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, item.Tag);
                    sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, item.DataSource);
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, item.Date.ToString(transactionSql23DateStyle));
                    sqlCommandShim.AddParameter(command, companyParameterName, SqlDbType.NVarChar, item.Company);
                    sqlCommandShim.AddParameter(command, priceParameterName, SqlDbType.Money, item.Price);
                    sqlCommandShim.AddParameter(command, insertDateTimeParameterName, SqlDbType.NVarChar, insertDateTime.ToString(transactionSql126DateStyle));
                    sqlCommandShim.AddParameter(command, temporalMaximumDateTimeParameterName, SqlDbType.NVarChar, temporalMaximumDateTime.ToString(transactionSql126DateStyle));
                    ExecuteNonQueryWithDeadlockRetry(connection, transaction, command);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to insert {item.ToString()} into SQL Server.", e);
            }
        }

        /// <summary>
        /// Updates an existing item in the current/latest grid.
        /// </summary>
        /// <param name="connection">The connection to use to update.</param>
        /// <param name="transaction">The transaction to execute the update operation in.</param>
        /// <param name="supersededItem">The item superseded in the existing grid as part of the update.</param>
        /// <param name="newItem">The new item to insert into the grid as part of the update.</param>
        /// <param name="udpateDateTime">The UTC date and time the update occurred.</param>
        protected void UpdateGridItem(SqlConnection connection, SqlTransaction transaction, StockPriceGridItemPTO supersededItem, StockPriceGridItem newItem, DateTime udpateDateTime)
        {
            try
            {
                DeleteGridItem(connection, transaction, supersededItem, udpateDateTime);
                InsertGridItem(connection, transaction, newItem, udpateDateTime);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to update stock price with id '{supersededItem.Id}' in SQL Server.", e);
            }
        }

        /// <summary>
        /// Deletes an existing item from the current/latest grid.
        /// </summary>
        /// <param name="connection">The connection to use to delete.</param>
        /// <param name="transaction">The transaction to execute the delete operation in.</param>
        /// <param name="item">The item to delete.</param>
        /// <param name="deleteDateTime">The UTC date and time the delete occurred.</param>
        protected void DeleteGridItem(SqlConnection connection, SqlTransaction transaction, StockPriceGridItemPTO item, DateTime deleteDateTime)
        {
            const String idParameterName = "@Id";
            const String deleteDateTimeParameterName = "@DeleteDateTime";
            String deleteStatement = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, {deleteDateTimeParameterName}, 126)
            WHERE   Id = {idParameterName};
            ";

            try
            {
                using (var command = new SqlCommand())
                {
                    sqlCommandShim.SetCommandText(command, deleteStatement);
                    PrepareCommand(connection, transaction, command);
                    sqlCommandShim.AddParameter(command, idParameterName, SqlDbType.BigInt, item.Id);
                    sqlCommandShim.AddParameter(command, deleteDateTimeParameterName, SqlDbType.NVarChar, deleteDateTime.AddTicks(-1).ToString(transactionSql126DateStyle));
                    ExecuteNonQueryWithDeadlockRetry(connection, transaction, command);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to delete stock price with id '{item.Id}' in SQL Server.", e);
            }
        }

        /// <summary>
        /// Creates a new grid.
        /// </summary>
        /// <param name="readConnection">The connection to use to read existing data.</param>
        /// <param name="writeConnection">The connection to use to write the grid.</param>
        /// <param name="transaction">The transaction to execute the write operation in.</param>
        /// <param name="gridOuterKeyProperties">The <see cref="IGridItemOuterKeyProperties">outer key properties</see> of the grid to create.</param>
        /// <param name="createDateTime">The timestamp when the grid was created.</param>
        /// <returns>The version number of the new grid.</returns>
        protected Int32 CreateGrid(SqlConnection readConnection, SqlConnection writeConnection, SqlTransaction transaction, StockPriceOuterKeyProperties gridOuterKeyProperties, DateTime createDateTime)
        {
            const String tagParameterName = "@Tag";
            const String dataSourceParameterName = "@DataSource";
            const String dateParameterName = "@Date";
            String maxVersionQuery = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = {tagParameterName}
              AND   DataSource = {dataSourceParameterName}
              AND   [Date] = CONVERT(date, {dateParameterName}, 23);
            ";
            Int32 gridVersionNumber = 1;
            using (var command = new SqlCommand())
            {
                try
                {
                    sqlCommandShim.SetCommandText(command, maxVersionQuery);
                    PrepareCommand(readConnection, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                    sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactionSql23DateStyle));
                    using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                    {
                        while (dataReader.Read())
                        {
                            if (dataReader["MaxVersion"] != DBNull.Value)
                            {
                                gridVersionNumber = (Int32)dataReader["MaxVersion"] + 1;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to retrieve latest grid version number while inserting stock price grid for {gridOuterKeyProperties.ToString()} into SQL Server.", e);
                }
            }

            const String versionParameterName = "@Version";
            const String createDateTimeParameterName = "@CreateDateTime";
            String insertStatement = $@"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        {tagParameterName}, 
                        {dataSourceParameterName}, 
                        CONVERT(date, {dateParameterName}, 23), 
                        {versionParameterName}, 
                        CONVERT(datetime2, {createDateTimeParameterName}, 126)
                    );
            ";
            try
            {
                using (var command = new SqlCommand())
                {
                    sqlCommandShim.SetCommandText(command, insertStatement);
                    PrepareCommand(writeConnection, transaction, command);
                    sqlCommandShim.AddParameter(command, tagParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Tag);
                    sqlCommandShim.AddParameter(command, dataSourceParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.DataSource);
                    sqlCommandShim.AddParameter(command, dateParameterName, SqlDbType.NVarChar, gridOuterKeyProperties.Date.ToString(transactionSql23DateStyle));
                    sqlCommandShim.AddParameter(command, versionParameterName, SqlDbType.Int, gridVersionNumber);
                    sqlCommandShim.AddParameter(command, createDateTimeParameterName, SqlDbType.NVarChar, createDateTime.ToString(transactionSql126DateStyle));
                    ExecuteNonQueryWithDeadlockRetry(writeConnection, transaction, command);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to insert stock price grid for {gridOuterKeyProperties.ToString()} and version {gridVersionNumber} into SQL Server.", e);
            }

            return gridVersionNumber;
        }

        /// <summary>
        /// Attempts to execute a non-query SQL command catching any deadlock (<see href="https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/mssqlserver-1205-database-engine-error?view=sql-server-ver16">1205</see>) exceptions and retrying according to the specified retry logic.
        /// </summary>
        /// <param name="connection">The connection to use to execute the command.</param>
        /// <param name="transaction">The transaction to execute the command under.</param>
        /// <param name="command">The SQL command to executeg.</param>
        protected void ExecuteNonQueryWithDeadlockRetry(SqlConnection connection, SqlTransaction transaction, SqlCommand command)
        {
            const Int32 deadlockErrorNumber = 1205;

            Int32 retryCount = sqlRetryLogicOption.NumberOfTries - 1;
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    sqlCommandShim.ExecuteNonQuery(command);
                    break;
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Errors.Count > 0 && sqlException.Errors[0].Number == deadlockErrorNumber)
                    {
                        exceptions.Add(sqlException);
                        if (retryCount > 0)
                        {
                            var retryEventArgs = new SqlRetryingEventArgs(sqlRetryLogicOption.NumberOfTries - retryCount, new TimeSpan(0), exceptions);
                            connectionRetryAction.Invoke(this, retryEventArgs);
                            retryCount--;
                        }
                        else
                        {
                            String exceptionMessage = $"The number of deadlock retries has exceeded the maximum of {sqlRetryLogicOption.NumberOfTries} attempt(s).";
                            throw new AggregateException(exceptionMessage, exceptions);
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Prepares the specified <see cref="SqlConnection"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        protected void PrepareConnection(SqlConnection connection)
        {
            sqlConnectionShim.SetRetryLogicProvider(connection, SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            sqlConnectionShim.GetRetryLogicProvider(connection).Retrying += connectionRetryAction;
        }

        /// <summary>
        /// Prepares the specified <see cref="SqlConnection"/> and sets the session deadlock priority.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="deadlockPriority">The <see cref="SessionDeadlockPriority"/> to assign to the session.</param>
        protected virtual void PrepareConnection(SqlConnection connection, SessionDeadlockPriority deadlockPriority)
        {
            PrepareConnection(connection);
            String setDeadlockPriorityStatement = $"SET DEADLOCK_PRIORITY {deadlockPriorityToStringValueMap[deadlockPriority]};";
            using (var setDeadlockPriorityCommand = new SqlCommand())
            {
                sqlCommandShim.SetCommandText(setDeadlockPriorityCommand, setDeadlockPriorityStatement);
                PrepareCommand(connection, setDeadlockPriorityCommand);
                sqlCommandShim.ExecuteNonQuery(setDeadlockPriorityCommand);
            }
        }

        /// <summary>
        /// Prepares the specified <see cref="SqlCommand"/>.
        /// </summary>
        /// <param name="connection">The connection to SQL Server.</param>
        /// <param name="command">The command.</param>
        protected void PrepareCommand(SqlConnection connection, SqlCommand command)
        {
            sqlCommandShim.SetConnection(command, connection);
            sqlCommandShim.SetCommandTimeout(command, operationTimeout);
        }

        /// <summary>
        /// Prepares the specified <see cref="SqlCommand"/>.
        /// </summary>
        /// <param name="connection">The connection to SQL Server.</param>
        /// <param name="transaction">The transaction to execute the command under.</param>
        /// <param name="command">The command.</param>
        protected void PrepareCommand(SqlConnection connection, SqlTransaction transaction, SqlCommand command)
        {
            PrepareCommand(connection, command);
            sqlCommandShim.SetTransaction(command, transaction);
        }

        /// <summary>
        /// Performs teardown/deconstruct operations on the the specified <see cref="SqlConnection"/> after utilizing it.
        /// </summary>
        /// <param name="connection">The connection to SQL Server.</param>
        protected void TeardownConnection(SqlConnection connection)
        {
            sqlConnectionShim.GetRetryLogicProvider(connection).Retrying -= connectionRetryAction;
        }

        /// <summary>
        /// Returns a list of SQL Server error numbers which indicate errors which are transient (i.e. could be recovered from after retry).
        /// </summary>
        /// <returns>The list of SQL Server error numbers.</returns>
        /// <remarks>See <see href="https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql">Troubleshooting connectivity issues and other errors with Azure SQL Database and Azure SQL Managed Instance</see></remarks> 
        protected List<Int32> GenerateSqlServerTransientErrorNumbers()
        {
            // Below obtained from https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql
            var returnList = new List<Int32>() { 26, 40, 615, 926, 4060, 4221, 10053, 10928, 10929, 11001, 40197, 40501, 40613, 40615, 40544, 40549, 49918, 49919, 49920 };
            // These are additional error numbers encountered during testing
            returnList.AddRange(new List<Int32>() { -2, 53, 121 });

            return returnList;
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> is the specified 'connectionString' parameter is null or whitespace.
        /// </summary>
        /// <param name="connectionStringParameterName">The name of the parameter.</param>
        /// <param name="connectionString">The value of the parameter.</param>
        protected void ThrowExceptionIfConnectionStringParameterNullOrWhitespace(String connectionStringParameterName, String connectionString)
        {
            if (String.IsNullOrWhiteSpace(connectionString) == true)
                throw new ArgumentException($"Parameter '{connectionStringParameterName}' must contain a value.", nameof(connectionString));
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> is the specified 'operationTimeout' parameter is less than 0.
        /// </summary>
        /// <param name="operationTimeoutParameterName">The name of the parameter.</param>
        /// <param name="operationTimeout">The value of the parameter.</param>
        protected void ThrowExceptionIfOperationTimeoutParameterLessThanZero(String operationTimeoutParameterName, Int32 operationTimeout)
        {
            if (operationTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(operationTimeout), $"Parameter '{operationTimeoutParameterName}' with value {operationTimeout} cannot be less than 0.");
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which performs an action against a SQL Server database.
        /// </summary>
        /// <typeparam name="T">The type of object emitted and used in the database operation.</typeparam>
        protected class DataBaseOperationEmitter<T> : IEmitter<T>
        {
            // REFACTORING: Can we put this into a base class?  SqlConnection would have to become a generic type.

            /// <summary>The connection to use to perform the operation.</summary>
            protected SqlConnection connection;
            /// <summary>The transaction to perform the operation under.</summary>
            protected SqlTransaction transaction;
            /// <summary>The date and time that the operation occurred.</summary>
            protected DateTime operationDateTime;
            /// <summary>An action which performs the database operation.  Accepts 3 parameters: the connection to use to perform the operation, the transaction to perform the operation under, the object used in the database operation, and the date and time that the operation occurred.</summary>
            protected Action<SqlConnection, SqlTransaction, T, DateTime> operationAction;

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister+DataBaseOperationEmitter class.
            /// </summary>
            /// <param name="connection">The connection to use to perform the operation.</param>
            /// <param name="transaction">The transaction to perform the operation under.</param>
            /// <param name="operationDateTime">The date and time that the operation occurred.</param>
            /// <param name="operationAction">An action which performs the database operation.  Accepts 3 parameters: the connection to use to perform the operation, the transaction to perform the operation under, the object used in the database operation, and the date and time that the operation occurred.</param>
            public DataBaseOperationEmitter(SqlConnection connection, SqlTransaction transaction, DateTime operationDateTime, Action<SqlConnection, SqlTransaction, T, DateTime> operationAction)
            {
                this.connection = connection;
                this.transaction = transaction;
                this.operationDateTime = operationDateTime;
                this.operationAction = operationAction;
            }

            /// <inheritdoc/>
            public void Emit(T instance)
            {
                operationAction(connection, transaction, instance, operationDateTime);
            }
        }

        #endregion
    }
}
