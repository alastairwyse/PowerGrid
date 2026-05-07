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
    /// Defines methods which interface to a <see cref="SqlCommand"/> instance.
    /// </summary>
    /// <remarks>Acts as a <see href="https://en.wikipedia.org/wiki/Shim_(computing)">shim</see> to the <see cref="SqlCommand"/> class for use in unit testing.</remarks>
    public interface ISqlCommandShim
    {
        /// <summary>
        /// Sets the Transact-SQL statement, table name or stored procedure to execute at the data source.
        /// </summary>
        /// <param name="sqlCommand">The <see cref="SqlCommand"/> to set the text on.</param>
        /// <param name="commandText">The command text.</param>
        public void SetCommandText(SqlCommand sqlCommand, String commandText);

        /// <summary>
        /// Sets a value indicating how the command text is to be interpreted.
        /// </summary>
        /// <param name="sqlCommand">The <see cref="SqlCommand"/> to set the type on.</param>
        /// <param name="commandType">The command type.</param>
        public void SetCommandType(SqlCommand sqlCommand, CommandType commandType);

        /// <summary>
        /// Returns an <see cref="IDataReader"/> implementation from the specified <see cref="SqlCommand"/>.
        /// </summary>
        /// <param name="sqlCommand">The <see cref="SqlCommand"/> to retrieve the <see cref="IDataReader"/> from.</param>
        /// <returns>An <see cref="IDataReader"/> implementation.</returns>
        public IDataReader ExecuteReader(SqlCommand sqlCommand);

        /// <summary>
        /// Executes a Transact-SQL statement against the connection and returns the number of rows affected.
        /// </summary>
        /// <param name="sqlCommand">The <see cref="SqlCommand"/> to execute against.</param>
        /// <returns>The number of rows affected.</returns>
        public Int32 ExecuteNonQuery(SqlCommand sqlCommand);

        /// <summary>
        /// Adds a parameter to the specified <see cref="SqlCommand"/>.
        /// </summary>
        /// <param name="sqlCommand">The <see cref="SqlCommand"/> to add the parameter to.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="sqlDbType">The data type of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        public void AddParameter(SqlCommand sqlCommand, String parameterName, SqlDbType sqlDbType, Object value);
    }
}
