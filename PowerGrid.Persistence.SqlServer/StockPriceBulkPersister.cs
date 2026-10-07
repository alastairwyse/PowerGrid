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
            public BulkAddEmitter(Int32 bufferSizeLimit)
                : base(bufferSizeLimit)
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
            public BulkDeleteEmitter(Int32 bufferSizeLimit)
                : base(bufferSizeLimit)
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
        /// Base for classes which implement <see cref="IEmitter{T}"/> and buffer and then persist objects to a SQL Server database in bulk.
        /// </summary>
        /// <typeparam name="T">The type of the object output in the <see cref="IEmitter{T}"/> implementation.</typeparam>
        protected abstract class BulkPersistenceEmitterBase<T> : IEmitter<T>
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

            #pragma warning restore 1591

            // TODO: Add data table and columns for insert/delete TVP types/tables

            /// <summary>The buffer for objects emitted objects.</summary>
            protected Queue<T> buffer;
            /// <summary>The maximum number of objects to hold in the buffer.</summary>
            protected Int32 bufferSizeLimit;

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkPersistenceEmitterBase class.
            /// </summary>
            /// <param name="bufferSizeLimit">The maximum number of objects to hold in the buffer.</param>
            public BulkPersistenceEmitterBase(Int32 bufferSizeLimit)
            {
                buffer = new Queue<T>();
                this.bufferSizeLimit = bufferSizeLimit;
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

            #endregion
        }

        #endregion
    }
}
