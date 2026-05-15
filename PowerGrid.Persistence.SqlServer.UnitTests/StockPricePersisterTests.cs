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
using PowerGrid.Core;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using NUnit.Framework;
using NSubstitute;

namespace PowerGrid.Persistence.SqlServer.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Persistence.SqlServer.StockPricePersister class.
    /// </summary>
    public class StockPricePersisterTests
    {
        // Note:
        //   Have found creative ways to mock SQL Server dependencies using 'shim' interfaces.
        //   However, Microsoft.Data.SqlClient.SqlTransaction has been problematic, because it has no public constructor, and to instantiate one via a SqlConnection requires the connection to be open.
        //   Hence the best I've been able to do so far is to pass the transaction as null, and check the corresponding ISqlTransactionShim methods receive null, e.g.
        //     testStockPricePersister.InsertGridItem(connection, null, testItem, testDeleteDateTime);
        //     mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
        //   Obviously this isn't perfect, as it won't catch if the code under test is passing null in error
        //   But, IMO it's a small price to pay, as opposed to having no units tests at all.

        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 23 date and time style</see>.</summary>
        private const String transactionSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        private const String transactionSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";
        private const String testConnectionString = "Server=127.0.0.1;Database=PowerGrid;User Id=user;Password=pwd=%X9sjQb;Encrypt=false;Authentication=SqlPassword";

        private TestUtilities utils;
        private IDateTimeProvider mockDateTimeProvider;
        private ISqlConnectionShim mockSqlConnectionShim;
        private ISqlTransactionShim mockSqlTransactionShim;
        private ISqlCommandShim mockSqlCommandShim;
        private StockPricePersisterWithProtectedMembers testStockPricePersister;

        [SetUp]
        protected void SetUp()
        {
            mockDateTimeProvider = Substitute.For<IDateTimeProvider>();
            mockSqlCommandShim = Substitute.For<ISqlCommandShim>();
            utils = new TestUtilities();
            testStockPricePersister = new StockPricePersisterWithProtectedMembers(testConnectionString, 5, 10, 0, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
        }

        [Test]
        public void InsertGridItem()
        {
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-08");
            const String testCompany = "Hitachi";
            StockPrice testItem = new(testDataSource, testDate, testCompany, 4732);
            DateTime testInsertDateTime = utils.CreateDataTimeFromString("2026-05-08 17:44:12.0000005");
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
                        CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                    );
            ";

            using (var connection = new SqlConnection(testConnectionString))
            {
                testStockPricePersister.InsertGridItem(connection, null, testItem, testInsertDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Company", SqlDbType.NVarChar, testCompany);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Price", SqlDbType.Money, testItem.Price);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@InsertDateTime", SqlDbType.NVarChar, testInsertDateTime.ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TemporalMaximumDateTime", SqlDbType.NVarChar, DateTime.MaxValue.ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
                mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        [Test]
        public void InsertGridItem_ExceptionInserting()
        {
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-08");
            const String testCompany = "Hitachi";
            StockPrice testItem = new(testDataSource, testDate, testCompany, 4732);
            DateTime testInsertDateTime = utils.CreateDataTimeFromString("2026-05-08 17:44:12.0000005");
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
                        CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                    );
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.InsertGridItem(connection, null, testItem, testInsertDateTime);
                });
                
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to insert stock price with datasource '{testDataSource}', date '{testDate.ToString(transactionSql23DateStyle)}', and company '{testCompany}' into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void UpdateGridItem()
        {
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-13");
            const String testCompany = "Toyota";
            StockPrice testNewItem = new(testDataSource, testDate, testCompany, 3210);
            StockPricePTO testSupersededItemItem = new(124, testDataSource, testDate, testCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testUpdateDateTime = utils.CreateDataTimeFromString("2026-05-14 10:51:21.0000011");
            String expectedInsertCommandText = @$"
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
                        CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                    );
            ";
            String expectedDeleteCommandText = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;
            ";

            using (var connection = new SqlConnection(testConnectionString))
            {
                testStockPricePersister.UpdateGridItem(connection, null, testSupersededItemItem, testNewItem, testUpdateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
                mockSqlCommandShim.Received(4).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(4).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(4).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Id", SqlDbType.BigInt, testSupersededItemItem.Id);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-14 10:51:21.0000010").ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(2).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
                mockSqlCommandShim.Received(4).ExecuteNonQuery(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertCommandText);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Company", SqlDbType.NVarChar, testCompany);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Price", SqlDbType.Money, testNewItem.Price);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@InsertDateTime", SqlDbType.NVarChar, testUpdateDateTime.ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TemporalMaximumDateTime", SqlDbType.NVarChar, DateTime.MaxValue.ToString(transactionSql126DateStyle));
            }
        }

        [Test]
        public void UpdateGridItem_ExceptionUpdating()
        {
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-13");
            const String testCompany = "Toyota";
            StockPrice testNewItem = new(testDataSource, testDate, testCompany, 3210);
            StockPricePTO testSupersededItem = new(124, testDataSource, testDate, testCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testUpdateDateTime = utils.CreateDataTimeFromString("2026-05-14 10:51:21.0000011");
            String expectedInsertCommandText = @$"
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
                        CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                    );
            ";
            String expectedDeleteCommandText = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.UpdateGridItem(connection, null, testSupersededItem, testNewItem, testUpdateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to update stock price with id '{testSupersededItem.Id}' in SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Failed to delete stock price with id '{testSupersededItem.Id}' in SQL Server."));
                Assert.That(e.InnerException.InnerException == mockException);
            }
        }

        [Test]
        public void DeleteGridItem()
        {
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-14");
            const String testCompany = "Sony";
            StockPricePTO testItem = new(123, testDataSource, testDate, testCompany, 4732, utils.CreateDataTimeFromString("2026-03-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testDeleteDateTime = utils.CreateDataTimeFromString("2026-05-14 22:23:13.0000006");
            String expectedCommandText = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;
            ";

            using (var connection = new SqlConnection(testConnectionString))
            {
                testStockPricePersister.DeleteGridItem(connection, null, testItem, testDeleteDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Id", SqlDbType.BigInt, testItem.Id);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-14 22:23:13.0000005").ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
                mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        [Test]
        public void DeleteGridItem_ExceptionDeleting()
        {
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-14");
            const String testCompany = "Sony";
            StockPricePTO testItem = new(123, testDataSource, testDate, testCompany, 4732, utils.CreateDataTimeFromString("2026-03-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testDeleteDateTime = utils.CreateDataTimeFromString("2026-05-14 22:23:13.0000006");
            String expectedCommandText = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.DeleteGridItem(connection, null, testItem, testDeleteDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to delete stock price with id '{testItem.Id}' in SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
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
                ISqlConnectionShim sqlConnectionShim,
                ISqlTransactionShim sqlTransactionShim,
                ISqlCommandShim sqlCommandShim
            ) : base(connectionString, retryCount, retryInterval, operationTimeout, dateTimeProvider, sqlConnectionShim, sqlTransactionShim, sqlCommandShim)
            {
            }

            public new void InsertGridItem(SqlConnection connection, SqlTransaction transaction, StockPrice item, DateTime insertDateTime)
            {
                base.InsertGridItem(connection, transaction, item, insertDateTime);
            }

            public new void UpdateGridItem(SqlConnection connection, SqlTransaction transaction, StockPricePTO supersededItem, StockPrice newItem, DateTime udpateDateTime)
            {
                base.UpdateGridItem(connection, transaction, supersededItem, newItem, udpateDateTime);
            }

            public new void DeleteGridItem(SqlConnection connection, SqlTransaction transaction, StockPricePTO item, DateTime deleteDateTime)
            {
                base.DeleteGridItem(connection, transaction, item, deleteDateTime);
            }
        }

        #pragma warning restore 1591

        #endregion
    }
}
