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

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Defines methods which interface to a <see cref="SqlTransaction"/> instance.
    /// </summary>
    /// <remarks>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlTransaction"/> class for use in unit testing.</remarks>
    public interface ISqlTransactionShim
    {
        /// <summary>
        /// Commits the specified <see cref="SqlTransaction"/>.
        /// </summary>
        public void Commit(SqlTransaction sqlTransaction);

        /// <summary>
        /// Rolls back the specified <see cref="SqlTransaction"/>.
        /// </summary>
        public void Rollback(SqlTransaction sqlTransaction);
    }
}
