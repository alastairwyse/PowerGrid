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
    /// <remarks>Acts as a<see href = "https://en.wikipedia.org/wiki/Shim_(computing)"> shim</see> to the <see cref="SqlCommand"/> class for use in unit testing.</remarks>
    public interface ISqlCommandShim
    {
        /// <summary>
        /// Returns an <see cref="IDataReader"/> implementation from the specified <see cref="SqlCommand"/>.
        /// </summary>
        /// <param name="sqlCommand">The <see cref="SqlCommand"/>.</param>
        /// <returns>An <see cref="IDataReader"/> implementation.</returns>
        public IDataReader ExecuteReader(SqlCommand sqlCommand);
    }
}
