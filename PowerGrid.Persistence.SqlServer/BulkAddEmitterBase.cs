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
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Grids;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Base for implementations of <see cref="IEmitter{T}"/> which persist <see cref="TGridItem"/> objects to a SQL Server database in bulk, by buffering objects received through the <see cref="IEmitter{T}.Emit(T)"/> method, and writing those objects to SQL Server when the buffer reaches a specified size.
    /// </summary>
    /// <typeparam name="TGridItem"></typeparam>
    public abstract class BulkAddEmitterBase<TGridItem> : BulkPersistenceStoredProcedureEmitterBase<TGridItem>
        where TGridItem : IGridOuterKeyProperties, IGridItem<TGridItem>
    {
        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.BulkAddEmitterBase class.
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
        public BulkAddEmitterBase
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
}
