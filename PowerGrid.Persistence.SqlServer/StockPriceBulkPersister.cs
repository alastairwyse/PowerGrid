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
        protected class BulkAddEmitter : IEmitter<StockPriceGridItem>
        {
            /// <summary>The buffer for <see cref="StockPriceGridItem"/> objects.</summary>
            protected Queue<StockPriceGridItem> stockPriceBuffer;
            /// <summary>The maximum number of <see cref="StockPriceGridItem"/> objects to hold in the buffer.</summary>
            protected Int32 bufferSizeLimit;

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkAddEmitter class.
            /// </summary>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItem"/> objects to hold in the buffer.</param>
            public BulkAddEmitter(Int32 bufferSizeLimit)
            {
                stockPriceBuffer = new Queue<StockPriceGridItem>();
                this.bufferSizeLimit = bufferSizeLimit;
            }

            /// <summary>
            /// Flushes any buffered <see cref="StockPriceGridItem"/> objects by writing them to SQL Server.
            /// </summary>
            public void Flush()
            {
                // Call the Persist() method.

                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public void Emit(StockPriceGridItem instance)
            {
                // Put in 'stockPriceBuffer' and then call Persist() method (calling DB SP) if 'bufferSizeLimit' is reached.

                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// An implementation of <see cref="IEmitter{T}"/> which soft deletes <see cref="StockPriceGridItemPTO"/> objects in a SQL Server database in bulk, by buffering objects received through the <see cref="BulkDeleteEmitter.Emit(StockPriceGridItem)"/> method, and deleting those objects from SQL Server when the buffer reaches a specified size.
        /// </summary>
        protected class BulkDeleteEmitter : IEmitter<StockPriceGridItemPTO>
        {
            /// <summary>The buffer for <see cref="StockPriceGridItem"/> objects.</summary>
            protected Queue<StockPriceGridItem> stockPriceBuffer;
            /// <summary>The maximum number of <see cref="StockPriceGridItem"/> objects to hold in the buffer.</summary>
            protected Int32 bufferSizeLimit;

            /// <summary>
            /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister+BulkDeleteEmitter class.
            /// </summary>
            /// <param name="bufferSizeLimit">The maximum number of <see cref="StockPriceGridItemPTO"/> objects to hold in the buffer.</param>
            public BulkDeleteEmitter(Int32 bufferSizeLimit)
            {
                stockPriceBuffer = new Queue<StockPriceGridItem>();
                this.bufferSizeLimit = bufferSizeLimit;
            }

            /// <summary>
            /// Flushes any buffered <see cref="StockPriceGridItemPTO"/> objects by deleting them from SQL Server.
            /// </summary>
            public void Flush()
            {
                // Call the Persist() method.

                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public void Emit(StockPriceGridItemPTO instance)
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    }
}
