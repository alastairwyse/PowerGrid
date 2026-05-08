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
using PowerGrid.Core;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using NUnit.Framework;
using NSubstitute;

namespace PowerGrid.Persistence.SqlServer.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Persistence.SqlServer.StockPricePersister class.
    /// </summary>
    public class StockPricePersisterTests
    {
        private const String testConnectionString = "Server=127.0.0.1;Database=PowerGrid;User Id=user;Password=pwd=%X9sjQb;Encrypt=false;Authentication=SqlPassword";

        private TestUtilities utils;
        private IDateTimeProvider mockDateTimeProvider;
        private ISqlCommandShim mockSqlCommandShim;
        private StockPricePersisterWithProtectedMembers testStockPricePersister;

        [SetUp]
        protected void SetUp()
        {
            mockDateTimeProvider = Substitute.For<IDateTimeProvider>();
            mockSqlCommandShim = Substitute.For<ISqlCommandShim>();
            utils = new TestUtilities();
            testStockPricePersister = new StockPricePersisterWithProtectedMembers(testConnectionString, 5, 10, 0, mockDateTimeProvider, mockSqlCommandShim);
        }

        [Test]
        public void InsertGridItem()
        {
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-08");
            const String testCompany = "Hitachi";
            StockPrice testItem = new(testDataSource, testDate, testCompany, 4732);
            DateTime testInsertDateTime = utils.CreateDataTimeFromString("2026-05-08 17:44:12.0000005");

            using (var connection = new SqlConnection(testConnectionString))
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                testStockPricePersister.InsertGridItem(connection, transaction, testItem, testInsertDateTime);
            }

            String expectedCommandText = @$"
            INSERT 
            INTO    StockPrices 
                    (
                        DataSource, 
                        [Date], 
                        Company, 
                        Price, 
                        TransactionFrom, 
                        TransactionTo 
                    )
            VALUES  (
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Company, 
                        @Price, 
                        CONVERT(datetime2, @InsertDateTime, 126), 
                        dbo.GetTemporalMaxDate()
                    );
            ";
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
        }

        #region Nested Classes

        #pragma warning disable 1591

        /// <summary>
        /// Version of the StockPricePersister class where private and protected methods are exposed as public so that they can be unit tested.
        /// </summary>
        protected class StockPricePersisterWithProtectedMembers : StockPricePersister
        {
            public StockPricePersisterWithProtectedMembers
            (
                String connectionString,
                Int32 retryCount,
                Int32 retryInterval,
                Int32 operationTimeout,
                IDateTimeProvider dateTimeProvider,
                ISqlCommandShim sqlCommandShim
            ) : base(connectionString, retryCount, retryInterval, operationTimeout, dateTimeProvider, sqlCommandShim)
            {
            }

            public new void InsertGridItem(SqlConnection connection, SqlTransaction transaction, StockPrice item, DateTime insertDateTime)
            {
                base.InsertGridItem(connection, transaction, item, insertDateTime);
            }
        }

        #pragma warning restore 1591

        #endregion
    }
}
