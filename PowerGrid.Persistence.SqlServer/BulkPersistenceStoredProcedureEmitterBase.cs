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
using Microsoft.Data.SqlClient;
using PowerGrid.Core;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Base for classes which implement <see cref="IEmitter{T}"/> and buffer and then either insert objects into or delete objects from a SQL Server database in bulk via stored procedures and <see href="https://learn.microsoft.com/en-us/sql/relational-databases/tables/use-table-valued-parameters-database-engine?view=sql-server-ver17">table-valued parameters</see>.
    /// </summary>
    /// <typeparam name="T">The type of the object output in the <see cref="IEmitter{T}"/> implementation.</typeparam>
    public abstract class BulkPersistenceStoredProcedureEmitterBase<T> : IEmitter<T>, IDisposable
    {
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 23 date and time style</see>.</summary>
        protected const String transactSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 24 time style</see>.</summary>
        protected const String transactSql24TimeStyle = "HH:mm:ss";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        protected const String transactSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";

        #pragma warning disable 1591

        // Names of columns in TVP tables used for bulk insert and delete
        protected const String gridItemData1ColumnName = "GridItemData1";
        protected const String gridItemData2ColumnName = "GridItemData2";
        protected const String gridItemData3ColumnName = "GridItemData3";
        protected const String gridItemData4ColumnName = "GridItemData4";
        protected const String gridItemData5ColumnName = "GridItemData5";
        protected const String gridItemData6ColumnName = "GridItemData6";
        protected const String gridItemData7ColumnName = "GridItemData7";
        protected const String gridItemData8ColumnName = "GridItemData8";
        protected const String gridItemData9ColumnName = "GridItemData9";
        protected const String gridItemData10ColumnName = "GridItemData10";
        protected const String idColumnName = "Id";

        // Stored procedure parameter names
        protected const String transactionTimestampParameterName = "@TransactionTimestamp";

        // TVP tables and columns
        protected DataTable bulkInsertStagingTable;
        protected DataColumn gridItemData1Column;
        protected DataColumn gridItemData2Column;
        protected DataColumn gridItemData3Column;
        protected DataColumn gridItemData4Column;
        protected DataColumn gridItemData5Column;
        protected DataColumn gridItemData6Column;
        protected DataColumn gridItemData7Column;
        protected DataColumn gridItemData8Column;
        protected DataColumn gridItemData9Column;
        protected DataColumn gridItemData10Column;
        protected DataTable bulkDeleteStagingTable;
        protected DataColumn idColumn;

        #pragma warning restore 1591

        /// <summary>The buffer for objects emitted objects.</summary>
        protected Queue<T> buffer;
        /// <summary>The timestamp representing when the objects emitted were either inserted or deleted.</summary>
        protected DateTime transactionTimestamp;
        /// <summary>The maximum number of objects to hold in the buffer.</summary>
        protected Int32 bufferSizeLimit;
        /// <summary>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</summary>
        protected ISqlConnectionShim sqlConnectionShim;
        /// <summary>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlCommand"/> class.</summary>
        protected ISqlCommandShim sqlCommandShim;
        /// <summary>The connection to use to insert or delete.</summary>
        protected SqlConnection sqlConnection;
        /// <summary>The transaction to use to insert or delete.</summary>
        protected SqlTransaction sqlTransaction;
        /// <summary>Parameters for retrying when transient errors occur.</summary>
        protected SqlRetryLogicOption sqlRetryLogicOption;
        /// <summary>The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</summary>
        protected Int32 operationTimeout;
        /// <summary>The action to invoke if an action is retried due to a transient error.</summary>
        protected EventHandler<SqlRetryingEventArgs> connectionRetryAction;
        /// <summary>Indicates whether the object has been disposed.</summary>
        protected Boolean disposed;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.BulkPersistenceStoredProcedureEmitterBase class.
        /// </summary>
        /// <param name="transactionTimestamp">The timestamp representing when the objects emitted were either inserted or deleted.</param>
        /// <param name="bufferSizeLimit">The maximum number of objects to hold in the buffer.</param>
        /// <param name="sqlConnectionShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</param>
        /// <param name="sqlCommandShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlCommand"/> class.</param>
        /// <param name="sqlConnection">The connection to use to insert or delete.</param>
        /// <param name="sqlTransaction">The transaction to use to insert or delete.</param>
        /// <param name="sqlRetryLogicOption">Parameters for retrying when transient errors occur.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="connectionRetryAction">The action to invoke if an action is retried due to a transient error.</param>
        public BulkPersistenceStoredProcedureEmitterBase
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
        {
            bulkInsertStagingTable = new DataTable();
            gridItemData1Column = new DataColumn(gridItemData1ColumnName, typeof(String));
            gridItemData2Column = new DataColumn(gridItemData2ColumnName, typeof(String));
            gridItemData3Column = new DataColumn(gridItemData3ColumnName, typeof(String));
            gridItemData4Column = new DataColumn(gridItemData4ColumnName, typeof(String));
            gridItemData5Column = new DataColumn(gridItemData5ColumnName, typeof(String));
            gridItemData6Column = new DataColumn(gridItemData6ColumnName, typeof(String));
            gridItemData7Column = new DataColumn(gridItemData7ColumnName, typeof(String));
            gridItemData8Column = new DataColumn(gridItemData8ColumnName, typeof(String));
            gridItemData9Column = new DataColumn(gridItemData9ColumnName, typeof(String));
            gridItemData10Column = new DataColumn(gridItemData10ColumnName, typeof(String));
            bulkInsertStagingTable.Columns.Add(gridItemData1Column);
            bulkInsertStagingTable.Columns.Add(gridItemData2Column);
            bulkInsertStagingTable.Columns.Add(gridItemData3Column);
            bulkInsertStagingTable.Columns.Add(gridItemData4Column);
            bulkInsertStagingTable.Columns.Add(gridItemData5Column);
            bulkInsertStagingTable.Columns.Add(gridItemData6Column);
            bulkInsertStagingTable.Columns.Add(gridItemData7Column);
            bulkInsertStagingTable.Columns.Add(gridItemData8Column);
            bulkInsertStagingTable.Columns.Add(gridItemData9Column);
            bulkInsertStagingTable.Columns.Add(gridItemData10Column);
            bulkDeleteStagingTable = new DataTable();
            idColumn = new DataColumn(idColumnName, typeof(Int64));
            bulkDeleteStagingTable.Columns.Add(idColumn);
            this.transactionTimestamp = transactionTimestamp;
            buffer = new Queue<T>();
            this.bufferSizeLimit = bufferSizeLimit;
            this.sqlConnectionShim = sqlConnectionShim;
            this.sqlCommandShim = sqlCommandShim;
            this.sqlConnection = sqlConnection;
            this.sqlTransaction = sqlTransaction;
            this.sqlRetryLogicOption = sqlRetryLogicOption;
            this.operationTimeout = operationTimeout;
            disposed = false;
        }

        /// <summary>
        /// Flushes any buffered objects by writing them to SQL Server.
        /// </summary>
        public void Flush()
        {
            if (buffer.Count > 0)
            {
                Persist();
            }
        }

        /// <inheritdoc/>
        public void Emit(T instance)
        {
            buffer.Enqueue(instance);
            if (buffer.Count == bufferSizeLimit)
            {
                Persist();
            }
        }

        #region Private/Protected Methods

        /// <summary>
        /// Persists/processes all buffered objects and then clears the buffer.
        /// </summary>
        protected abstract void Persist();

        /// <summary>
        /// Creates a <see cref="SqlParameter" />.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="parameterType">The type of the parameter.</param>
        /// <param name="parameterValue">The value of the parameter.</param>
        /// <returns>The created parameter.</returns>
        protected SqlParameter CreateSqlParameterWithValue(String parameterName, SqlDbType parameterType, Object parameterValue)
        {
            var returnParameter = new SqlParameter(parameterName, parameterType);
            returnParameter.Value = parameterValue;

            return returnParameter;
        }

        /// <summary>
        /// Attempts to execute a stored procedure which does not return a result set, catching any deadlock (<see href="https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/mssqlserver-1205-database-engine-error?view=sql-server-ver16">1205</see>) exceptions and retrying according to the retry logic specified in member <see cref=""/>.
        /// </summary>
        /// <param name="procedureName">The name of the stored procedure.</param>
        /// <param name="parameters">The parameters to pass to the stored procedure.</param>
        protected void ExecuteStoredProcedureWithDeadlockRetry(String procedureName, IEnumerable<SqlParameter> parameters)
        {
            const Int32 deadlockErrorNumber = 1205;

            Int32 retryCount = sqlRetryLogicOption.NumberOfTries - 1;
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    using (var command = new SqlCommand(procedureName))
                    {
                        sqlCommandShim.SetCommandType(command, CommandType.StoredProcedure);
                        foreach (SqlParameter currentParameter in parameters)
                        {
                            sqlCommandShim.AddParameter(command, currentParameter.ParameterName, currentParameter.SqlDbType, currentParameter.Value);
                        }
                        sqlCommandShim.SetConnection(command, sqlConnection);
                        sqlCommandShim.SetTransaction(command, sqlTransaction);
                        sqlCommandShim.SetCommandTimeout(command, operationTimeout);
                        sqlCommandShim.ExecuteNonQuery(command);
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

        #endregion

        #region Finalize / Dispose Methods

        /// <summary>
        /// Releases the unmanaged resources used by the BulkPersistenceStoredProcedureEmitterBase.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #pragma warning disable 1591

        ~BulkPersistenceStoredProcedureEmitterBase()
        {
            Dispose(false);
        }

        #pragma warning restore 1591

        /// <summary>
        /// Provides a method to free unmanaged resources used by this class.
        /// </summary>
        /// <param name="disposing">Whether the method is being called as part of an explicit Dispose routine, and hence whether managed resources should also be freed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                    gridItemData1Column.Dispose();
                    gridItemData2Column.Dispose();
                    gridItemData3Column.Dispose();
                    gridItemData4Column.Dispose();
                    gridItemData5Column.Dispose();
                    gridItemData6Column.Dispose();
                    gridItemData7Column.Dispose();
                    gridItemData8Column.Dispose();
                    gridItemData9Column.Dispose();
                    gridItemData10Column.Dispose();
                    bulkInsertStagingTable.Dispose();
                    idColumn.Dispose();
                    bulkDeleteStagingTable.Dispose();
                }
                // Free your own state (unmanaged objects).

                // Set large fields to null.

                disposed = true;
            }
        }

        #endregion
    }
}
