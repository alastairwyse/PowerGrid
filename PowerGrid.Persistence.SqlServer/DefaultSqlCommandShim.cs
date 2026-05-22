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
using System.Data;
using Microsoft.Data.SqlClient;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Default implementation of <see cref="ISqlCommandShim"/>.
    /// </summary>
    public class DefaultSqlCommandShim : ISqlCommandShim
    {
        /// <inheritdoc/>
        public void SetConnection(SqlCommand sqlCommand, SqlConnection connection)
        {
            sqlCommand.Connection = connection;
        }

        /// <inheritdoc/>
        public void SetTransaction(SqlCommand sqlCommand, SqlTransaction transaction)
        {
            sqlCommand.Transaction = transaction;
        }

        /// <inheritdoc/>
        public void SetCommandText(SqlCommand sqlCommand, String commandText)
        {
            sqlCommand.CommandText = commandText;
        }

        /// <inheritdoc/>
        public void SetCommandTimeout(SqlCommand sqlCommand, int commandTimeout)
        {
            sqlCommand.CommandTimeout = commandTimeout;
        }

        /// <inheritdoc/>
        public void SetCommandType(SqlCommand sqlCommand, CommandType commandType)
        {
            sqlCommand.CommandType = commandType;
        }

        /// <inheritdoc/>
        public IDataReader ExecuteReader(SqlCommand sqlCommand)
        {
            return sqlCommand.ExecuteReader();
        }

        /// <inheritdoc/>
        public Int32 ExecuteNonQuery(SqlCommand sqlCommand)
        {
            return sqlCommand.ExecuteNonQuery();
        }

        /// <inheritdoc/>
        public void AddParameter(SqlCommand sqlCommand, String parameterName, SqlDbType sqlDbType, Object value)
        {
            sqlCommand.Parameters.Add(parameterName, sqlDbType);
            sqlCommand.Parameters[parameterName].Value = value;
        }
    }
}
