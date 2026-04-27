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
using PowerGrid.Persistence.Models.PersistenceTransferObjects;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and writes <see cref="StockPrice"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class StockPricePersister : IGridPersister<StockPricePTO, StockPrice>
    {
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 23 date and time style</see>.</summary>
        protected const String transactionSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        protected const String transactionSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";

        /// <summary>The string to use to connect to the SQL Server database.</summary>
        protected String connectionString;
        /// <summary>The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</summary>
        protected Int32 operationTimeout;
        /// <summary>Provider for the current date and time.</summary>
        protected IDateTimeProvider dateTimeProvider;
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

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        public StockPricePersister
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout
        )
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
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="mockDateTimeProvider">A mock <see cref="IDateTimeProvider"/></param>
        /// <param name="sqlCommandShim">A mock <see cref="ISqlCommandShim"/>.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public StockPricePersister
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IDateTimeProvider dateTimeProvider, 
            ISqlCommandShim sqlCommandShim
        ) : this(connectionString, retryCount, retryInterval, operationTimeout)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.sqlCommandShim = sqlCommandShim;
        }

        /// <inheritdoc/>
        public GridComparisonStatistics PersistGrid(IList<StockPrice> gridItems)
        {
            if (gridItems.Count == 0)
                throw new ArgumentException($"Parameter '{nameof(gridItems)}' contained no items.", nameof(gridItems));

            using (var connection = new SqlConnection(connectionString))
            {
                DateTime transactionTimestamp = dateTimeProvider.UtcNow();

                // Create IEmitter implementations for comparer
                Action<SqlConnection, StockPrice, DateTime> addedItemEmitterOperationAction = (SqlConnection connection, StockPrice addedStockPrice, DateTime transactionDateTime) =>
                {
                    InsertGridItem(connection, addedStockPrice, transactionDateTime);
                };
                DataBaseOperationEmitter<StockPrice> addedItemEmitter = new(connection, transactionTimestamp, addedItemEmitterOperationAction);
                Action<SqlConnection, Tuple<StockPricePTO, StockPrice>, DateTime> updatedItemsEmitterOperationAction = (SqlConnection connection, Tuple<StockPricePTO, StockPrice> updatedStockPrices, DateTime transactionDateTime) =>
                {
                    UpdateGridItem(connection, updatedStockPrices.Item1, updatedStockPrices.Item2, transactionDateTime);
                };
                DataBaseOperationEmitter<Tuple<StockPricePTO, StockPrice>> updatedItemsEmitter = new(connection, transactionTimestamp, updatedItemsEmitterOperationAction);
                Action<SqlConnection, StockPricePTO, DateTime> deletedItemEmitterOperationAction = (SqlConnection connection, StockPricePTO deletedStockPrice, DateTime transactionDateTime) =>
                {
                    DeleteGridItem(connection, deletedStockPrice, transactionDateTime);
                };
                DataBaseOperationEmitter<StockPricePTO> deletedItemEmitter = new(connection, transactionTimestamp, deletedItemEmitterOperationAction);
                GridComparer<StockPricePTO, StockPrice> gridComparer = new(addedItemEmitter, updatedItemsEmitter, deletedItemEmitter);

                // Setup IEnumerable 'chains' 
                //   Create a GridContentsValidator to check that all elements of 'gridItems' have the same data source and date
                GridContentsValidator<StockPrice> newGridContentsValidator = new();
                String expectedDataSource = gridItems[0].DataSource;
                DateOnly expectedDate = gridItems[0].Date;
                Action<StockPrice> newGridContentsValidationAction = (StockPrice stockPrice) =>
                {
                    if (stockPrice.DataSource != expectedDataSource)
                        throw new GridContentsValidationException<StockPrice>($"{typeof(StockPrice).Name} with {nameof(StockPrice.DataSource)} '{stockPrice.DataSource}' found in grid which which was expected to contain {nameof(StockPrice.DataSource)} '{expectedDataSource}'.", stockPrice);
                    if (stockPrice.Date != expectedDate)
                        throw new GridContentsValidationException<StockPrice>($"{typeof(StockPrice).Name} with {nameof(StockPrice.Date)} '{stockPrice.Date.ToString(transactionSql23DateStyle)}' found in grid which which was expected to contain {nameof(StockPrice.Date)} '{expectedDate.ToString(transactionSql23DateStyle)}'.", stockPrice);
                };
                GridContentsDuplicateChecker<StockPrice> newGridDuplicateChecker = new();
                // Order of below chain is 1 validate, 2 order, 3 dup check
                IEnumerable<StockPrice> newGridContents = newGridDuplicateChecker.CheckForDuplicates
                (
                    newGridContentsValidator.ValidateItems
                    (
                        gridItems, 
                        newGridContentsValidationAction
                    ).Order()
                );

                GridComparisonStatistics comparisonStatistics;
                try
                {
                    IEnumerable<StockPricePTO> existingGridContents;
                    try
                    {
                        existingGridContents = GetExistingGrid(connection, expectedDataSource, expectedDate, transactionTimestamp);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Failed to read existing stock price grid from SQL Server for data source '{expectedDataSource}', date '{expectedDate.ToString(transactionSql23DateStyle)}', and transaction time '{transactionTimestamp.ToString(transactionSql126DateStyle)}'.", e);
                    }
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            comparisonStatistics = gridComparer.Compare(existingGridContents, newGridContents);
                            transaction.Commit();
                        }
                        catch (Exception e)
                        {
                            transaction.Rollback();
                            throw new Exception($"Failed to compare new stock price grid to existing grid in SQL Server for data source '{expectedDataSource}', date '{expectedDate.ToString(transactionSql23DateStyle)}', and transaction time '{transactionTimestamp.ToString(transactionSql126DateStyle)}'.", e);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to persist grid to SQL Server.", e);
                }

                return comparisonStatistics;
            }
        }

        #region Private/Protected Methods

        /// <summary>
        /// Gets the latest stock price grid version for the specified parameters.
        /// </summary>
        /// <param name="connection">The connection to use to retrieve the grid.</param>
        /// <param name="dataSource">The datasource of the stock prices.</param>
        /// <param name="date">The quotes date of the stock prices.</param>
        /// <returns>A tuple containing: The version number of the latest grid (or 0 if no grids exist for the specified parameters), and the transaction timestamp of the grid (or <see cref="DateTime.MinValue"/> if no grids exist for the specified parameters).</returns>
        protected (Int32, DateTime) GetLatestGridVersion(SqlConnection connection, String dataSource, DateOnly date)
        {
            // REFACTORING: 
            //   General steps here in base case
            //   Query can be abstract (or tablename and parameters... other parts of the insertStatement should be common)
            //   Use AppAccess SqlServerPersisterUtilities and ReadQueryGeneratorBase classes for influence in how to split platform-agnostic SQL into base classes

            String query = @$"
            SELECT  [Version] AS Version, 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   DataSource = '{dataSource}' 
              AND   [Date] = CONVERT(date, '{date.ToString(transactionSql126DateStyle)}', 126) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   DataSource = '{dataSource}' 
                        AND   [Date] = CONVERT(date, '{date.ToString(transactionSql126DateStyle)}', 126) 
                    );
            ";

            if (String.IsNullOrWhiteSpace(dataSource) == true)
                throw new ArgumentException($"Parameter '{nameof(dataSource)}' must contain a value.", nameof(dataSource));

            using (var command = new SqlCommand(query))
            {
                Int32 latestGridVersionNumber = 0;
                DateTime latestGridTransactionTimestamp = DateTime.MinValue.ToUniversalTime();

                PrepareConnectionAndCommand(connection, command);
                using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                {
                    Boolean alreadyReadResult = false;
                    while (dataReader.Read())
                    {
                        if (alreadyReadResult == true)
                        {
                            throw new Exception($"Read multiple results from SQL Server when attempting to retrieve latest stock price grid version for data source '{dataSource}' and date '{date.ToString(transactionSql23DateStyle)}'.");
                        }
                        latestGridVersionNumber = (Int32)dataReader["Version"];
                        latestGridTransactionTimestamp = DateTime.ParseExact((String)dataReader["TransactionTimestamp"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                        latestGridTransactionTimestamp = DateTime.SpecifyKind(latestGridTransactionTimestamp, DateTimeKind.Utc);
                        alreadyReadResult = true;
                    }
                }
                TeardownConnectionAndCommand(connection, command);

                return (latestGridVersionNumber, latestGridTransactionTimestamp);
            }
        }

        /// <summary>
        /// Gets the contents of a stock price grid.
        /// </summary>
        /// <param name="connection">The connection to use to retrieve the grid.</param>
        /// <param name="dataSource">The datasource of the stock prices.</param>
        /// <param name="date">The quotes date of the stock prices.</param>
        /// <param name="transactionTimestamp">The transaction timestamp when the grid was created.</param>
        /// <returns>The items in the grid.</returns>
        protected IEnumerable<StockPricePTO> GetExistingGrid(SqlConnection connection, String dataSource, DateOnly date, DateTime transactionTimestamp)
        {
            String query = @$"
            SELECT Id, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   TransactionFrom, 
                   TransactionTo 
            FROM   StockPrices 
            WHERE  DataSource = 'xx'
              AND  [Date] = CONVERT(date, '{date.ToString(transactionSql23DateStyle)}', 126) 
              AND  CONVERT(datetime2, '{date.ToString(transactionSql126DateStyle)}', 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY DataSource, 
                      [Date], 
                      Company;
            ";

            if (String.IsNullOrWhiteSpace(dataSource) == true)
                throw new ArgumentException($"Parameter '{nameof(dataSource)}' must contain a value.", nameof(dataSource));

            using (var command = new SqlCommand(query))
            {
                PrepareConnectionAndCommand(connection, command);
                using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                {
                    while (dataReader.Read())
                    {
                        Int64 currentId = (Int64)dataReader["Id"];
                        String currentDataSource = (String)dataReader["DataSource"];
                        DateOnly currentDate = DateOnly.ParseExact((String)dataReader["Date"], transactionSql23DateStyle, DateTimeFormatInfo.InvariantInfo);
                        String currentCompany = (String)dataReader["Company"];
                        Decimal currentPrice = Decimal.Parse((String)dataReader["Price"]);
                        DateTime currentTransactionFrom = DateTime.ParseExact((String)dataReader["TransactionFrom"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                        currentTransactionFrom = DateTime.SpecifyKind(currentTransactionFrom, DateTimeKind.Utc);
                        DateTime currentTransactionTo = DateTime.ParseExact((String)dataReader["TransactionTo"], transactionSql126DateStyle, DateTimeFormatInfo.InvariantInfo);
                        currentTransactionTo = DateTime.SpecifyKind(currentTransactionFrom, DateTimeKind.Utc);

                        yield return new StockPricePTO(currentId, currentDataSource, currentDate, currentCompany, currentPrice, currentTransactionFrom, currentTransactionTo);
                    }
                }
                TeardownConnectionAndCommand(connection, command);
            }
        }

        // REFACTORING: 
        //   All below methods could go to base class, once PTO type is included in generic signature.  Just queries would defined in derived class.

        /// <summary>
        /// Adds an item to the current/latest grid.
        /// </summary>
        /// <param name="connection">The connection to use to insert.</param>
        /// <param name="item">The item to add.</param>
        /// <param name="insertDateTime">The UTC date and time the addition occurred.</param>
        protected void InsertGridItem(SqlConnection connection, StockPrice item, DateTime insertDateTime)
        {
            String insertStatement = @$"
            INSERT 
            INTO    StockPrices 
                    (
                        DataSource, 
                        [Date], 
                        Company, 
                        Price, 
                        TransactionFrom, 
                        TransactionTo 
                    )
            VALUES  (
                        '{item.DataSource}', 
                        CONVERT(date, '{item.Date.ToString(transactionSql23DateStyle)}', 23), 
                        '{item.Company}', 
                        {item.Price}, 
                        CONVERT(date, '{insertDateTime.ToString(transactionSql126DateStyle)}', 126) , 
                        dbo.GetTemporalMaxDate()
                    );
            ";

            try
            {
                ExecuteNonQueryWithDeadlockRetry(connection, insertStatement);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to insert stock price with datasource '{item.DataSource}', date '{item.Date.ToString(transactionSql23DateStyle)}', and company '{item.Company}' into SQL Server.", e);
            }
        }

        /// <summary>
        /// Updates an existing item in the current/latest grid.
        /// </summary>
        /// <param name="connection">The connection to use to update.</param>
        /// <param name="supersededItem">The item superseded in the existing grid as part of the update.</param>
        /// <param name="newItem">The new item to insert into the grid as part of the update.</param>
        /// <param name="udpateDateTime">The UTC date and time the update occurred.</param>
        protected void UpdateGridItem(SqlConnection connection, StockPricePTO supersededItem, StockPrice newItem, DateTime udpateDateTime)
        {
            try
            {
                DeleteGridItem(connection, supersededItem, udpateDateTime);
                InsertGridItem(connection, newItem, udpateDateTime);
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
        /// <param name="item">The item to delete.</param>
        /// <param name="deleteDateTime">The UTC date and time the delete occurred.</param>
        protected void DeleteGridItem(SqlConnection connection, StockPricePTO item, DateTime deleteDateTime)
        {
            String deleteStatement = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = dbo.SubtractTemporalMinimumTimeUnit(CONVERT(datetime2, '{deleteDateTime.ToString(transactionSql126DateStyle)}', 126))
            WHERE   Id = {item.Id};
            ";

            try
            {
                ExecuteNonQueryWithDeadlockRetry(connection, deleteStatement);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to delete stock price with id '{item.Id}' in SQL Server.", e);
            }
        }

        protected Int32 CreateGrid(SqlConnection connection, String dataSource, DateOnly date, DateTime createDateTime)
        {
            String maxIdQuery = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = '{dataSource}'
              AND   [Date] = CONVERT(date, '{date.ToString(transactionSql23DateStyle)}', 23);
            ";

            Int32 gridVersionNumber = 1;
            using (var command = new SqlCommand(maxIdQuery))
            {
                PrepareConnectionAndCommand(connection, command);
                using (IDataReader dataReader = sqlCommandShim.ExecuteReader(command))
                {
                    while (dataReader.Read())
                    {
                        if (dataReader["MaxId"] != DBNull.Value)
                        {
                            gridVersionNumber = (Int32)dataReader["MaxId"] + 1;
                        }
                    }
                }
                TeardownConnectionAndCommand(connection, command);
            }

            String insertStatement = $@"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        '{dataSource}', 
                        CONVERT(date, '{date.ToString(transactionSql23DateStyle)}', 23), 
                        {gridVersionNumber}, 
                        CONVERT(datetime2, '{createDateTime.ToString(transactionSql126DateStyle)}', 126)
                    );
            ";

            try
            {
                ExecuteNonQueryWithDeadlockRetry(connection, insertStatement);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to insert stock price grid for datasource '{dataSource}', date '{date.ToString(transactionSql23DateStyle)}', and version {gridVersionNumber} into SQL Server.", e);
            }

            return gridVersionNumber;
        }

        /// <summary>
        /// Attempts to execute a non-insertStatement SQL command catching any deadlock (<see href="https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/mssqlserver-1205-database-engine-error?view=sql-server-ver16">1205</see>) exceptions and retrying according to the specified retry logic.
        /// </summary>
        /// <param name="connection">The connection to use to execute the command.</param>
        /// <param name="commandText">The SQL command as a string.</param>
        protected void ExecuteNonQueryWithDeadlockRetry(SqlConnection connection, String commandText)
        {
            const Int32 deadlockErrorNumber = 1205;

            Int32 retryCount = sqlRetryLogicOption.NumberOfTries - 1;
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    using (var command = new SqlCommand(commandText))
                    {
                        connection.RetryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption);
                        //connection.RetryLogicProvider.Retrying += connectionRetryAction;
                        connection.Open();
                        command.Connection = connection;
                        command.CommandTimeout = operationTimeout;
                        /* REFACTORING: Commenting, as not sure SQL Server will like if you change deadlock priority whilst it's in the middle of the read
                        String setDeadlockPriorityStatement = $"SET DEADLOCK_PRIORITY {deadlockPriorityToStringValueMap[SessionDeadlockPriority.Low]};";
                        using (var setDeadlockPriorityCommand = new SqlCommand(setDeadlockPriorityStatement))
                        {
                            setDeadlockPriorityCommand.Connection = connection;
                            setDeadlockPriorityCommand.CommandTimeout = operationTimeout;
                            setDeadlockPriorityCommand.ExecuteNonQuery();
                        }
                        */
                        command.ExecuteNonQuery();
                        break;
                     }
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Errors.Count > 0 && sqlException.Errors[0].Number == deadlockErrorNumber)
                    {
                        exceptions.Add(sqlException);
                        if (retryCount > 0)
                        {
                            var retryEventArgs = new SqlRetryingEventArgs(sqlRetryLogicOption.NumberOfTries - retryCount, new TimeSpan(0), exceptions);
                            //connectionRetryAction.Invoke(this, retryEventArgs);
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
        /// Prepare the specified <see cref="SqlConnection"/> and <see cref="SqlCommand"/> to execute a insertStatement against them.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="command">The command which runs the insertStatement.</param>
        protected void PrepareConnectionAndCommand(SqlConnection connection, SqlCommand command)
        {
            connection.RetryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption);
            connection.Open();
            command.Connection = connection;
            command.CommandTimeout = operationTimeout;
        }

        /// <summary>
        /// Prepare the specified <see cref="SqlConnection"/> and <see cref="SqlCommand"/> to execute a insertStatement against them, and sets the session deadlock priority.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="command">The command which runs the insertStatement.</param>
        /// <param name="deadlockPriority">The <see cref="SessionDeadlockPriority"/> to assign to the session.</param>
        protected virtual void PrepareConnectionAndCommand(SqlConnection connection, SqlCommand command, SessionDeadlockPriority deadlockPriority)
        {
            PrepareConnectionAndCommand(connection, command);
            String setDeadlockPriorityStatement = $"SET DEADLOCK_PRIORITY {deadlockPriorityToStringValueMap[deadlockPriority]};";
            using (var setDeadlockPriorityCommand = new SqlCommand(setDeadlockPriorityStatement))
            {
                setDeadlockPriorityCommand.Connection = connection;
                setDeadlockPriorityCommand.CommandTimeout = operationTimeout;
                setDeadlockPriorityCommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Performs teardown/deconstruct operations on the the specified <see cref="SqlConnection"/> and <see cref="SqlCommand"/> after utilizing them.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="command">The command.</param>
        protected void TeardownConnectionAndCommand(SqlConnection connection, SqlCommand command)
        {
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
            /// <summary>The date and time that the operation occurred.</summary>
            protected DateTime operationDateTime;
            /// <summary>An action which performs the database operation.  Accepts 3 parameters: the connection to use to perform the operation, the object used in the database operation, and the date and time that the operation occurred.</summary>
            protected Action<SqlConnection, T, DateTime> operationAction;

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister+DataBaseOperationEmitter class.
            /// </summary>
            /// <param name="connection">The connection to use to perform the operation.</param>
            /// <param name="operationDateTime">The date and time that the operation occurred.</param>
            /// <param name="operationAction">An action which performs the database operation.  Accepts 3 parameters: the connection to use to perform the operation, the object used in the database operation, and the date and time that the operation occurred.</param>
            public DataBaseOperationEmitter(SqlConnection connection, DateTime operationDateTime, Action<SqlConnection, T, DateTime> operationAction)
            {
                this.connection = connection;
                this.operationDateTime = operationDateTime;
                this.operationAction = operationAction;
            }

            /// <inheritdoc/>
            public void Emit(T instance)
            {
                operationAction(connection, instance, operationDateTime);
            }
        }

        #endregion
    }
}
