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
    /// Defines methods which interface to a <see cref="SqlConnection"/> instance.
    /// </summary>
    /// <remarks>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlConnection"/> class for use in unit testing.</remarks>
    public interface ISqlConnectionShim
    {
        /// <summary>
        /// Sets the <see cref="SqlRetryLogicBaseProvider"/> bound to the connection.
        /// </summary>
        /// <param name="sqlConnection">The <see cref="SqlConnection"/> to set the <see cref="SqlRetryLogicBaseProvider"/> on.</param>
        /// <param name="sqlRetryLogicBaseProvider">The  cref="SqlRetryLogicBaseProvider"/>.</param>
        public void SetRetryLogicProvider(SqlConnection sqlConnection, SqlRetryLogicBaseProvider retryLogicProvider);

        /// <summary>
        /// Gets the <see cref="SqlRetryLogicBaseProvider"/> bound to the connection.
        /// </summary>
        /// <param name="sqlConnection">The <see cref="SqlConnection"/> to get the <see cref="SqlRetryLogicBaseProvider"/> from.</param>
        /// <returns>The <see cref="SqlRetryLogicBaseProvider"/>.</returns>
        public SqlRetryLogicBaseProvider GetRetryLogicProvider(SqlConnection sqlConnection);

        /// <summary>
        /// Opens the specified <see cref="SqlConnection"/>.
        /// </summary>
        /// <param name="sqlConnection">The <see cref="SqlConnection"/> to open.</param>
        public void Open(SqlConnection sqlConnection);

        /// <summary>
        /// Starts a database transaction in the specified <see cref="SqlConnection"/>.
        /// </summary>
        /// <param name="sqlConnection">The <see cref="SqlConnection"/> to begin the transaction on..</param>
        /// <returns>An object representing the new transaction.</returns>
        public SqlTransaction BeginTransaction(SqlConnection sqlConnection);

        /// <summary>
        /// Closes the specified <see cref="SqlConnection"/>.
        /// </summary>
        /// <param name="sqlConnection">The <see cref="SqlConnection"/> to close.</param>
        public void Close(SqlConnection sqlConnection);
    }
}
