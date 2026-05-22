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
    /// Default implementation of <see cref="ISqlCommandShim"/>.
    /// </summary>
    public class DefaultSqlConnectionShim : ISqlConnectionShim
    {
        /// <inheritdoc/>
        public void SetRetryLogicProvider(SqlConnection sqlConnection, SqlRetryLogicBaseProvider retryLogicProvider)
        {
            sqlConnection.RetryLogicProvider = retryLogicProvider;
        }

        /// <inheritdoc/>
        public SqlRetryLogicBaseProvider GetRetryLogicProvider(SqlConnection sqlConnection)
        {
            return sqlConnection.RetryLogicProvider;
        }

        /// <inheritdoc/>
        public void Open(SqlConnection sqlConnection)
        {
            sqlConnection.Open();
        }

        /// <inheritdoc/>
        public SqlTransaction BeginTransaction(SqlConnection sqlConnection)
        {
            return sqlConnection.BeginTransaction();
        }

        /// <inheritdoc/>
        public void Close(SqlConnection sqlConnection)
        {
            sqlConnection.Close();
        }
    }
}
