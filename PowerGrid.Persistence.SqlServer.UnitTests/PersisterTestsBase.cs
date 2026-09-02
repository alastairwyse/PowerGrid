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
using System.Reflection;
using Microsoft.Data.SqlClient;
using PowerGrid.Core.UnitTests;
using ApplicationLogging;
using ApplicationMetrics;
using NSubstitute;
using NUnit.Framework;

namespace PowerGrid.Persistence.SqlServer.UnitTests
{
    /// <summary>
    /// Base for unit tests for Persister classes.
    /// </summary>
    public abstract class PersisterTestsBase
    {
        /// <summary>DateOnly format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 23 date and time style</see>.</summary>
        protected const String transactSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        protected const String transactSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";
        /// <summary>TimeOnly format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 24 time style</see>.</summary>
        protected const String transactSql24TimeStyle = "HH:mm:ss";
        protected const String testConnectionString = "Server=127.0.0.1;Database=PowerGrid;User Id=user;Password=pwd=%X9sjQb;Encrypt=false;Authentication=SqlPassword";

        protected TestUtilities utils;
        protected List<SqlRetryingEventArgs> connectionRetryActionInvocationParameters;
        protected EventHandler<SqlRetryingEventArgs> connectionRetryAction;
        protected IApplicationLogger mockLogger;
        protected IMetricLogger mockMetricLogger;
        protected IDateTimeProvider mockDateTimeProvider;
        protected ISqlConnectionShim mockSqlConnectionShim;
        protected ISqlTransactionShim mockSqlTransactionShim;
        protected ISqlCommandShim mockSqlCommandShim;

        [SetUp]
        protected virtual void SetUp()
        {
            mockLogger = Substitute.For<IApplicationLogger>();
            mockMetricLogger = Substitute.For<IMetricLogger>();
            mockDateTimeProvider = Substitute.For<IDateTimeProvider>();
            mockSqlConnectionShim = Substitute.For<ISqlConnectionShim>();
            mockSqlTransactionShim = Substitute.For<ISqlTransactionShim>();
            mockSqlCommandShim = Substitute.For<ISqlCommandShim>();
            utils = new TestUtilities();
        }

        #region Private/Protected Methods

        // Base of Below courtesy of https://blog.jonathanchannon.com/2014-01-02-unit-testing-with-sqlexception/ (required a few tweaks to get to the pass the right params to SqlError constructor)
        protected SqlException GetSqlException(Int32 errorNumber, String errorMessage, Int32 constructorIndex)
        {
            SqlErrorCollection collection = ConstructObject<SqlErrorCollection>();
            var underlyingException = new Exception("Mock underlying deadlock exception");
            SqlError error = ConstructObject<SqlError>(errorNumber, (byte)56, (byte)13, "server name", errorMessage, "proc", 442, 1, underlyingException);

            typeof(SqlErrorCollection)
                .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(collection, new object[] { error });

            var e = typeof(SqlException)
                .GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static, null, CallingConventions.ExplicitThis, new[] { typeof(SqlErrorCollection), typeof(string) }, new ParameterModifier[] { })
                .Invoke(null, new object[] { collection, "11.0.0" }) as SqlException;

            return e;
        }

        protected T ConstructObject<T>(params object[] parameters)
        {
            ConstructorInfo constructor = typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];

            return (T)constructor.Invoke(parameters);
        }

        #endregion
    }
}
