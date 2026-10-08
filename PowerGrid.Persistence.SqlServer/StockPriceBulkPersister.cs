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
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Reads and bulk writes <see cref="StockPrice"/> objects from and to a Microsoft SQL Server database.
    /// </summary>
    public class StockPriceBulkPersister
    {
        /*
            Need to override PersistGrid(TOuterKeyProperties gridOuterKeyProperties, IList<TEntity> items)

            2x lists holding InsertItems and DeleteItems
            Emitters should just add to these two lists
                AND should persist when some buffer limit gets hit
                AND the implementation needs a Flush() or similar method which can be explicitly called when comparison is finished

            Emitter just defines
                void Emit(T instance)

            Is IN clause a good choice for deleting rows?
              https://learn.microsoft.com/en-us/sql/t-sql/language-elements/in-transact-sql?view=sql-server-ver17#remarks
        */

        #region Nested Classes

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which persists <see cref="StockPriceGridItem"/> objects to a SQL Server database in bulk, by buffering objects received through the <see cref="BulkAddEmitter.Emit(StockPriceGridItem)"/> method, and writing those objects to SQL Server when the buffer reaches a specified size.
        /// </summary>
        protected class BulkAddEmitter : BulkPersistenceEmitterBase<StockPriceGridItem>
        {
            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkAddEmitter class.
            /// </summary>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItem"/> objects to hold in the buffer.</param>
            /// <param name="sqlConnectionShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</param>
            /// <param name="sqlConnection">The connection to use to insert or delete.</param>
            public BulkAddEmitter(Int32 bufferSizeLimit, ISqlConnectionShim sqlConnectionShim, SqlConnection sqlConnection)
                : base(bufferSizeLimit, sqlConnectionShim, sqlConnection)
            {
            }

            #region Private/Protected Methods

            /// <summary>
            /// Persists all buffered items and then clears the buffer.
            /// </summary>
            protected override void Persist()
            {

            }

            #endregion
        }

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which soft deletes <see cref="StockPriceGridItemPTO"/> objects in a SQL Server database in bulk, by buffering objects received through the <see cref="BulkDeleteEmitter.Emit(StockPriceGridItem)"/> method, and deleting those objects from SQL Server when the buffer reaches a specified size.
        /// </summary>
        protected class BulkDeleteEmitter : BulkPersistenceEmitterBase<StockPriceGridItemPTO>
        {
            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkDeleteEmitter class.
            /// </summary>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItemPTO"/> objects to hold in the buffer.</param>
            /// <param name="sqlConnectionShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</param>
            /// <param name="sqlConnection">The connection to use to insert or delete.</param>
            public BulkDeleteEmitter(Int32 bufferSizeLimit, ISqlConnectionShim sqlConnectionShim, SqlConnection sqlConnection)
                : base(bufferSizeLimit, sqlConnectionShim, sqlConnection)
            {
            }

            #region Private/Protected Methods

            /// <summary>
            /// Deletes all buffered items and then clears the buffer.
            /// </summary>
            protected override void Persist()
            {
                // TODO: See https://github.com/alastairwyse/ApplicationAccess/blob/main/ApplicationAccess.Persistence.Sql.SqlServer/SqlServerAccessManagerTemporalBulkPersister.cs#L229
            }

            #endregion
        }

        /// <summary>
        /// Base for classes which implement <see cref="IEmitter{T}"/> and buffer and then either insert objects into or delete objects from a SQL Server database in bulk.
        /// </summary>
        /// <typeparam name="T">The type of the object output in the <see cref="IEmitter{T}"/> implementation.</typeparam>
        protected abstract class BulkPersistenceEmitterBase<T> : IEmitter<T>, IDisposable
        {
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

            // TVP table and columns
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
            /// <summary>The maximum number of objects to hold in the buffer.</summary>
            protected Int32 bufferSizeLimit;
            /// <summary>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</summary>
            protected ISqlConnectionShim sqlConnectionShim;
            /// <summary>The connection to use to insert or delete.</summary>
            protected SqlConnection sqlConnection;
            /// <summary>Indicates whether the object has been disposed.</summary>
            protected Boolean disposed;

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkPersistenceEmitterBase class.
            /// </summary>
            /// <param name="bufferSizeLimit">The maximum number of objects to hold in the buffer.</param>
            /// <param name="sqlConnectionShim">Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class.</param>
            /// <param name="sqlConnection">The connection to use to insert or delete.</param>
            public BulkPersistenceEmitterBase(Int32 bufferSizeLimit, ISqlConnectionShim sqlConnectionShim, SqlConnection sqlConnection)
            {
                bulkInsertStagingTable = new DataTable();
                gridItemData1Column = new DataColumn();
                gridItemData2Column = new DataColumn();
                gridItemData3Column = new DataColumn();
                gridItemData4Column = new DataColumn();
                gridItemData5Column = new DataColumn();
                gridItemData6Column = new DataColumn();
                gridItemData7Column = new DataColumn();
                gridItemData8Column = new DataColumn();
                gridItemData9Column = new DataColumn();
                gridItemData10Column = new DataColumn();
                bulkDeleteStagingTable = new DataTable();
                idColumn = new DataColumn();
                buffer = new Queue<T>();
                this.bufferSizeLimit = bufferSizeLimit;
                this.sqlConnectionShim = sqlConnectionShim;
                this.sqlConnection = sqlConnection;
                disposed = false;
            }

            /// <summary>
            /// Flushes any buffered objects by writing them to SQL Server.
            /// </summary>
            public void Flush()
            {
                Persist();
            }

            /// <inheritdoc/>
            public void Emit(T instance)
            {
                buffer.Enqueue(instance);
                if (buffer.Count == bufferSizeLimit)
                {
                    Persist();
                    buffer.Clear();
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

            #endregion

            #region Finalize / Dispose Methods

            /// <summary>
            /// Releases the unmanaged resources used by the SqlServerAccessManagerTemporalBulkPersister.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            #pragma warning disable 1591

            ~BulkPersistenceEmitterBase()
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

        #endregion
    }
}
