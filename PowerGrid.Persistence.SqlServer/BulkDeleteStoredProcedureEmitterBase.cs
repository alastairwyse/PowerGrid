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
    /// Base for classes which implement <see cref="IEmitter{T}"/> and buffer and then delete <see cref="TGridItemPTO"/> objects from a SQL Server database in bulk via stored procedures and <see href="https://learn.microsoft.com/en-us/sql/relational-databases/tables/use-table-valued-parameters-database-engine?view=sql-server-ver17">table-valued parameters.
    /// </summary>
    /// <typeparam name="TGridItemPTO">The type of the object deleted from the SQL Server database.</typeparam>
    public abstract class BulkDeleteStoredProcedureEmitterBase<TGridItemPTO> : BulkPersistenceStoredProcedureEmitterBase<TGridItemPTO>
        where TGridItemPTO : IGridOuterKeyProperties, IPersistenceTransferObject
    {
        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.BulkDeleteStoredProcedureEmitterBase class.
        /// </summary>
        /// <param name="transactionTimestamp">The timestamp representing when the objects emitted were either inserted or deleted.</param>
        /// <param name="bufferSizeLimit">The maximum number of <see cref="TGridItem"/> objects to hold in the buffer.</param>
        /// <param name="sqlConnectionShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</param>
        /// <param name="sqlCommandShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlCommand"/> class.</param>
        /// <param name="sqlConnection">The connection to use to insert or delete.</param>
        /// <param name="sqlTransaction">The transaction to use to insert or delete.</param>
        /// <param name="sqlRetryLogicOption">Parameters for retrying when transient errors occur.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="connectionRetryAction">The action to invoke if an action is retried due to a transient error.</param>
        public BulkDeleteStoredProcedureEmitterBase
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

        #region Abstract Properties and Methods

        /// <summary>
        /// The name of the stored procedure which performs bulk delete for a collection of grid item PTOs.
        /// </summary>
        protected abstract String BulkDeleteStoredProcedureName { get; }

        #endregion

        /// <summary>
        /// Deletes all buffered items and then clears the buffer.
        /// </summary>
        protected override void Persist()
        {
            foreach (TGridItemPTO currentGridItem in buffer)
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
            ExecuteStoredProcedureWithDeadlockRetry(BulkDeleteStoredProcedureName, parameters);
            bulkDeleteStagingTable.Rows.Clear();
            buffer.Clear();
        }

        #endregion
    }
}
