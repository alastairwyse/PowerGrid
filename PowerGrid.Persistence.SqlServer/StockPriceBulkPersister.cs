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
using PowerGrid.Grids;

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
    }
}
