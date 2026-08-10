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
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Persistence;
using PowerGrid.Persistence.SqlServer.Metrics;
using ApplicationLogging;
using ApplicationMetrics;
using ApplicationMetrics.MetricLoggers;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Base for classes which write and read grids to and from Microsoft SQL Server databases.
    /// </summary>
    /// <typeparam name="TEntity">The type of data held in each item in the grid.</typeparam>
    /// <typeparam name="TOuterKeyProperties">The <see cref="IGridOuterKeyProperties">outer key properties</see> of the items in the grid.</typeparam>
    /// <typeparam name="TGridItem">The items in the grid (i.e. where each item includes the <see cref="IGridOuterKeyProperties">outer key properties</see>).</typeparam>
    /// <typeparam name="TGridItemPTO">The <see cref="IPersistenceTransferObject">persistence transfer object</see> equivalent of <see cref="TGridItem"/>.</typeparam>
    public abstract class PersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO> : Persistence.PersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TEntity : IGridItem<TEntity>
        where TCommonKeyProperties : Core.IGridCommonKeyProperties
        where TOuterKeyProperties : IGridOuterKeyProperties
        where TGridItem : TEntity, IGridOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridOuterKeyProperties, IGridItem<TGridItem>, IPersistenceTransferObject
    {
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 23 date and time style</see>.</summary>
        protected const String transactSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        protected const String transactSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";
        /// <summary>The type of collation to use when ordering results returned from SQL Server.</summary>
        protected const String transactSqlCollation = "Latin1_General_BIN2";
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
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        public PersisterBase
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
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public PersisterBase
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger, IMetricLogger metricLogger
        )
            : this(connectionString, retryCount, retryInterval, operationTimeout, logger)
        {
            this.metricLogger = metricLogger;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
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
        public PersisterBase
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

        #region Private/Protected Methods

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
