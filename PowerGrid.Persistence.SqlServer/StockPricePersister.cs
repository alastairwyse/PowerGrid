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

using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Grids;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and writes <see cref="StockPrice"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class StockPricePersister : IGridPersister<StockPrice>
    {
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        protected const String transactionSql23DateStyle = "yyyy-MM-dd";
        protected const String transactionSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";

        /// <summary>The string to use to connect to the SQL Server database.</summary>
        protected String connectionString;
        /// <summary>The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</summary>
        protected Int32 operationTimeout;
        /// <summary>A set of SQL Server database engine error numbers which denote a transient fault.</summary>
        /// <see href="https://docs.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors?view=sql-server-ver16"/>
        /// <see href="https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql"/>
        protected List<Int32> sqlServerTransientErrorNumbers;
        /// <summary>The retry logic to use when connecting to and executing against the SQL Server database.</summary>
        protected SqlRetryLogicOption sqlRetryLogicOption;
        /// <summary>Maps <see cref="SessionDeadlockPriority"/> values to their equivalent SQL Server string value.</summary>
        protected Dictionary<SessionDeadlockPriority, String> deadlockPriorityToStringValueMap;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPricePersister class.
        /// </summary>
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
        }

        /// <inheritdoc/>
        public GridComparisonStatistics PersistGrid(IList<StockPrice> gridItems)
        {
            /*
             DONT wrap in a lock... have that outside
             Create a transaction
             read the current latest grid data
             do a comparison (stream results of read)
               include validator and consistency check 'plugins' in the reading
             write delete etc individual items as they come out
             rollback in case of exception
             commit
             */

            if (gridItems.Count == 0)
                throw new ArgumentException($"Parameter '{nameof(gridItems)}' contained no items.", nameof(gridItems));

            try
            {

            }
            catch (Exception e)
            {
                throw new Exception("Failed to persist grid to SQL Server.", e);
            }
        }

        #region Private/Protected Methods

        /// <summary>
        /// Gets the latest stock price grid version for the specified parameters.
        /// </summary>
        /// <param name="dataSource">The datasource of the stock prices.</param>
        /// <param name="date">The quotes date of the stock prices.</param>
        /// <returns>A tuple containing: The version number of the latest grid (or 0 if no grids exist for the specified parameters), and the transaction timestamp of the grid (or <see cref="DateTime.MinValue"/> if no grids exist for the specified parameters).</returns>
        protected (Int32, DateTime) GetLatestGridVersion(String dataSource, DateOnly date)
        {
            // REFACTORING: 
            //   General steps here in base case
            //   Query can be abstract (or tablename and parameters... other parts of the query should be common)
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

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(query))
            {
                Int32 latestGridVersionNumber = 0;
                DateTime latestGridTransactionTimestamp = DateTime.MinValue.ToUniversalTime();

                PrepareConnectionAndCommand(connection, command);
                using (SqlDataReader dataReader = command.ExecuteReader())
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
        /// <param name="dataSource">The datasource of the stock prices.</param>
        /// <param name="date">The quotes date of the stock prices.</param>
        /// <param name="transactionTimestamp">The transaction timestamp when the grid was created.</param>
        /// <returns>The items in the grid.</returns>
        protected IEnumerable<StockPrice> GetExistingGrid(String dataSource, DateOnly date, DateTime transactionTimestamp)
        {
            String query = @$"
            SELECT DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price
            FROM   StockPrices 
            WHERE  DataSource = 'xx'
              AND  [Date] = CONVERT(date, '{date.ToString(transactionSql23DateStyle)}', 126) 
              AND  CONVERT(datetime2, '{date.ToString(transactionSql126DateStyle)}', 126) BETWEEN TransactionFrom AND TransactionTo;
            ";

            if (String.IsNullOrWhiteSpace(dataSource) == true)
                throw new ArgumentException($"Parameter '{nameof(dataSource)}' must contain a value.", nameof(dataSource));

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(query))
            {
                PrepareConnectionAndCommand(connection, command);
                using (SqlDataReader dataReader = command.ExecuteReader())
                {
                    while (dataReader.Read())
                    {
                        // See SqlServerPersisterUtilities.ExecuteQueryAndConvertColumnWithDeadlockRetry<T>
                        //   Need retry ability but with ability to yield results

                        String firstDataItemAsString = (String)dataReader[columnToConvert1];
                        String secondDataItemAsString = (String)dataReader[columnToConvert2];
                        TReturn1 firstDataItemConverted = returnType1ConversionFromStringFunction.Invoke(firstDataItemAsString);
                        TReturn2 secondDataItemConverted = returnType2ConversionFromStringFunction.Invoke(secondDataItemAsString);
                        yield return new Tuple<TReturn1, TReturn2>(firstDataItemConverted, secondDataItemConverted);
                    }
                }
                TeardownConnectionAndCommand(connection, command);
            }
        }

        /// <summary>
        /// Prepare the specified <see cref="SqlConnection"/> and <see cref="SqlCommand"/> to execute a query against them.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="command">The command which runs the query.</param>
        protected void PrepareConnectionAndCommand(SqlConnection connection, SqlCommand command)
        {
            connection.RetryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption);
            connection.Open();
            command.Connection = connection;
            command.CommandTimeout = operationTimeout;
        }

        /// <summary>
        /// Prepare the specified <see cref="SqlConnection"/> and <see cref="SqlCommand"/> to execute a query against them, and sets the session deadlock priority.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="command">The command which runs the query.</param>
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
        public void ThrowExceptionIfConnectionStringParameterNullOrWhitespace(String connectionStringParameterName, String connectionString)
        {
            if (String.IsNullOrWhiteSpace(connectionString) == true)
                throw new ArgumentException($"Parameter '{connectionStringParameterName}' must contain a value.", nameof(connectionString));
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> is the specified 'operationTimeout' parameter is less than 0.
        /// </summary>
        /// <param name="operationTimeoutParameterName">The name of the parameter.</param>
        /// <param name="operationTimeout">The value of the parameter.</param>
        public void ThrowExceptionIfOperationTimeoutParameterLessThanZero(String operationTimeoutParameterName, Int32 operationTimeout)
        {
            if (operationTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(operationTimeout), $"Parameter '{operationTimeoutParameterName}' with value {operationTimeout} cannot be less than 0.");
        }

        #endregion
    }
}
