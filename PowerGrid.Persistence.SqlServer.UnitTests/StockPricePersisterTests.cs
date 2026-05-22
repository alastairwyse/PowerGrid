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
using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using NSubstitute;
using PowerGrid.Core;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;

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
        private List<SqlRetryingEventArgs> connectionRetryActionInvocationParameters;
        private EventHandler<SqlRetryingEventArgs> connectionRetryAction;
        private IDateTimeProvider mockDateTimeProvider;
        private ISqlConnectionShim mockSqlConnectionShim;
        private ISqlTransactionShim mockSqlTransactionShim;
        private ISqlCommandShim mockSqlCommandShim;
        private StockPricePersisterWithProtectedMembers testStockPricePersister;

        [SetUp]
        protected void SetUp()
        {
            mockDateTimeProvider = Substitute.For<IDateTimeProvider>();
            mockSqlConnectionShim = Substitute.For<ISqlConnectionShim>();
            mockSqlTransactionShim = Substitute.For<ISqlTransactionShim>();
            mockSqlCommandShim = Substitute.For<ISqlCommandShim>();
            utils = new TestUtilities();
            testStockPricePersister = new StockPricePersisterWithProtectedMembers(testConnectionString, 5, 10, 0, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
        }

        [Test]
        public void PersistGrid_GridItemsParameterEmpty()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testStockPricePersister.PersistGrid(new List<StockPrice>());
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'gridItems' contained no items."));
            Assert.That(e.ParamName == "gridItems");
        }

        [Test]
        public void PersistGrid_NewGridItemPriceLessThan0()
        {
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String sonyCompany = "Sony";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(testDataSource, testDate, canonCompany, -1),
                new StockPrice(testDataSource, testDate, hitachiCompany, 4732)
            };
            List<StockPricePTO> existingGridItems = new()
            {
                new StockPricePTO(1, testDataSource, testDate, hitachiCompany, 4733, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPricePTO(2, testDataSource, testDate, sonyCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedReadExistingGridCommandText = @$"
            SELECT Id, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY DataSource, 
                      [Date], 
                      Company;
            ";
            String expectedMaxIdQueryText = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedGridInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockDateTimeProvider.UtcNow().Returns<DateTime>(transactionTimeStamp);
            mockSqlConnectionShim.BeginTransaction(Arg.Any<SqlConnection>()).Returns<SqlTransaction>((SqlTransaction)null);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                // Call to get existing grid contents
                true, true, false,
                // Call to get existing grid max id
                true, false
            );
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id, existingGridItems[1].Id);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactionSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxId"].Returns<Object>(1);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.PersistGrid(testGridItems);
            });
            
            Assert.That(e.Message, Does.StartWith($"Failed to persist grid to SQL Server."));
            Assert.That(e.InnerException.Message, Does.StartWith($"Failed to compare new stock price grid to existing grid in SQL Server for data source 'Bloomberg', date '2026-05-16', and transaction time '2026-05-16T16:36:41.0000023'."));
            Assert.That(e.InnerException.InnerException is GridContentsValidationException<StockPrice>);
            GridContentsValidationException<StockPrice> innerInnerException = (GridContentsValidationException<StockPrice>)e.InnerException.InnerException;
            Assert.That(innerInnerException.Message, Does.StartWith($"Failed to validate item in grid."));
            Assert.That(innerInnerException.GridItem == testGridItems[0]);
        }

        [Test]
        public void PersistGrid_DuplicateGridItems()
        {
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String sonyCompany = "Sony";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(testDataSource, testDate, canonCompany, 4732),
                new StockPrice(testDataSource, testDate, canonCompany, 4733)
            };
            List<StockPricePTO> existingGridItems = new()
            {
                new StockPricePTO(1, testDataSource, testDate, hitachiCompany, 4733, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPricePTO(2, testDataSource, testDate, sonyCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedReadExistingGridCommandText = @$"
            SELECT Id, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY DataSource, 
                      [Date], 
                      Company;
            ";
            String expectedMaxIdQueryText = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedGridInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockDateTimeProvider.UtcNow().Returns<DateTime>(transactionTimeStamp);
            mockSqlConnectionShim.BeginTransaction(Arg.Any<SqlConnection>()).Returns<SqlTransaction>((SqlTransaction)null);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                // Call to get existing grid contents
                true, true, false,
                // Call to get existing grid max id
                true, false
            );
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id, existingGridItems[1].Id);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactionSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxId"].Returns<Object>(1);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.PersistGrid(testGridItems);
            });

            Assert.That(e.Message, Does.StartWith($"Failed to persist grid to SQL Server."));
            Assert.That(e.InnerException.Message, Does.StartWith($"Failed to compare new stock price grid to existing grid in SQL Server for data source 'Bloomberg', date '2026-05-16', and transaction time '2026-05-16T16:36:41.0000023'."));
            Assert.That(e.InnerException.InnerException is GridContentsDuplicateItemsException<StockPrice>);
            GridContentsDuplicateItemsException<StockPrice> innerInnerException = (GridContentsDuplicateItemsException<StockPrice>)e.InnerException.InnerException;
            Assert.That(innerInnerException.Message, Does.StartWith($"Grid contains items with duplicate key values."));
            Assert.That(innerInnerException.GridItem == testGridItems[1]);
        }

        [Test]
        public void PersistGrid()
        {
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String sonyCompany = "Sony";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(testDataSource, testDate, canonCompany, 4441),
                new StockPrice(testDataSource, testDate, hitachiCompany, 4732)
            };
            List<StockPricePTO> existingGridItems = new()
            {
                new StockPricePTO(1, testDataSource, testDate, hitachiCompany, 4733, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPricePTO(2, testDataSource, testDate, sonyCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedReadExistingGridCommandText = @$"
            SELECT Id, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY DataSource, 
                      [Date], 
                      Company;
            ";
            String expectedMaxIdQueryText = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedGridInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockDateTimeProvider.UtcNow().Returns<DateTime>(transactionTimeStamp);
            mockSqlConnectionShim.BeginTransaction(Arg.Any<SqlConnection>()).Returns<SqlTransaction>((SqlTransaction)null);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                // Call to get existing grid contents
                true, true, false, 
                // Call to get existing grid max id
                true, false
            );
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id, existingGridItems[1].Id);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactionSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxId"].Returns<Object>(1);

            GridComparisonStatistics resultStatistics = testStockPricePersister.PersistGrid(testGridItems);

            mockSqlConnectionShim.Received(2).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(4).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(2).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
            mockSqlCommandShim.Received(8).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(8).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(6).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlConnectionShim.Received(1).BeginTransaction(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(5).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(5).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactionSql126DateStyle));
            mockSqlTransactionShim.Received(1).Commit(null);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
            mockSqlCommandShim.Received(6).SetTransaction(Arg.Any<SqlCommand>(), null);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridInsertStatementText);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactionSql126DateStyle));
            Assert.That(resultStatistics.ItemsAddedCount == 1);
            Assert.That(resultStatistics.ItemsUpdatedCount == 1);
            Assert.That(resultStatistics.ItemsDeletedCount == 1);
        }

        [Test]
        public void GetLatestGridVersion_DataSourceParameterNull()
        {
            const String testDataSource = null;
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<ArgumentException>(delegate
                {
                    testStockPricePersister.GetLatestGridVersion(connection, testDataSource, testDate);
                });

                Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
                Assert.That(e.ParamName == "dataSource");
            }
        }

        [Test]
        public void GetLatestGridVersion_DataSourceParameterWhitespace()
        {
            const String testDataSource = " ";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<ArgumentException>(delegate
                {
                    testStockPricePersister.GetLatestGridVersion(connection, testDataSource, testDate);
                });

                Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
                Assert.That(e.ParamName == "dataSource");
            }
        }

        [Test]
        public void GetLatestGridVersion_ExceptionReading()
        {
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 126) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   DataSource = @DataSource
                        AND   [Date] = CONVERT(date, @Date, 126) 
                    );
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.GetLatestGridVersion(connection, testDataSource, testDate);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to read latest stock price grid version for 'Reuters', and date '2026-05-16' from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetLatestGridVersion()
        {
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 126) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   DataSource = @DataSource
                        AND   [Date] = CONVERT(date, @Date, 126) 
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-16T13:39:41.0000013");

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testStockPricePersister.GetLatestGridVersion(connection, testDataSource, testDate);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                Assert.That(versionNumberResult == 3);
                Assert.That(transactionTimestampResult == utils.CreateDataTimeFromString("2026-05-16 13:39:41.0000013"));
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetLatestGridVersion_NoVersionExists()
        {
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 126) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   DataSource = @DataSource
                        AND   [Date] = CONVERT(date, @Date, 126) 
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testStockPricePersister.GetLatestGridVersion(connection, testDataSource, testDate);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                Assert.That(versionNumberResult == 0);
                Assert.That(transactionTimestampResult == DateTime.MinValue);
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetLatestGridVersion_MultipleRecordsReturned()
        {
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 126) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   DataSource = @DataSource
                        AND   [Date] = CONVERT(date, @Date, 126) 
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, true);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-16T13:39:41.0000013");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.GetLatestGridVersion(connection, testDataSource, testDate);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                Assert.That(e.Message, Does.StartWith($"Failed to read latest stock price grid version for 'Reuters', and date '2026-05-16' from SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Read multiple results from SQL Server when attempting to retrieve latest stock price grid version for data source 'Reuters' and date '2026-05-16'."));
            }
        }

        [Test]
        public void GetExistingGrid_DataSourceParameterNull()
        {
            const String testDataSource = null;
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-05-16 11:45:40.0000012");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<ArgumentException>(delegate
                {
                    List<StockPricePTO> results = new(testStockPricePersister.GetExistingGrid(connection, testDataSource, testDate, testTransactionTimestamp));
                });

                Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
                Assert.That(e.ParamName == "dataSource");
            }
        }

        [Test]
        public void GetExistingGrid_DataSourceParameterWhitespace()
        {
            const String testDataSource = " ";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-05-16 11:45:40.0000012");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<ArgumentException>(delegate
                {
                    List<StockPricePTO> results = new(testStockPricePersister.GetExistingGrid(connection, testDataSource, testDate, testTransactionTimestamp));
                });

                Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
                Assert.That(e.ParamName == "dataSource");
            }
        }

        [Test]
        public void GetExistingGrid_ExceptionReading()
        {
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-05-16 11:45:40.0000012");
            String expectedCommandText = @$"
            SELECT Id, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY DataSource, 
                      [Date], 
                      Company;
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    List<StockPricePTO> results = new(testStockPricePersister.GetExistingGrid(connection, testDataSource, testDate, testTransactionTimestamp));
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to read stock price grid for datasource 'Bloomberg', date '2026-05-16', and transaction timestamp '2026-05-16 11:45:40.0000012' from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetExistingGrid_GridDoesntExist()
        {
            // TODO: Implement when there's a public GetGrid() method

            throw new NotImplementedException();
        }

        [Test]
        public void GetExistingGrid()
        {
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-05-16 11:45:40.0000012");
            String expectedCommandText = @$"
            SELECT Id, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY DataSource, 
                      [Date], 
                      Company;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Id"].Returns<Object>(1L);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactionSql23DateStyle));
            mockDataReader["Company"].Returns<Object>("Canon");
            mockDataReader["Price"].Returns<Object>(new Decimal(4215));
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");

            using (var connection = new SqlConnection(testConnectionString))
            {
                List<StockPricePTO> results = new(testStockPricePersister.GetExistingGrid(connection, testDataSource, testDate, testTransactionTimestamp));

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, testTransactionTimestamp.ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                Assert.That(results.Count == 1);
                Assert.That(results[0].Id == 1);
                Assert.That(results[0].DataSource == testDataSource);
                Assert.That(results[0].Date == testDate);
                Assert.That(results[0].Company == "Canon");
                Assert.That(results[0].Price == 4215);
                Assert.That(results[0].TransactionFrom == utils.CreateDataTimeFromString("2026-05-15 09:05:40.0000012"));
                Assert.That(results[0].TransactionFrom.Kind == DateTimeKind.Utc);
                Assert.That(results[0].TransactionTo == utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
                Assert.That(results[0].TransactionTo.Kind == DateTimeKind.Utc);
            }
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
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Company", SqlDbType.NVarChar, testCompany);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Price", SqlDbType.Money, testItem.Price);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@InsertDateTime", SqlDbType.NVarChar, testInsertDateTime.ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TemporalMaximumDateTime", SqlDbType.NVarChar, DateTime.MaxValue.ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
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
                mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Id", SqlDbType.BigInt, testSupersededItemItem.Id);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-14 10:51:21.0000010").ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
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
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Id", SqlDbType.BigInt, testItem.Id);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-14 22:23:13.0000005").ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
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


        [Test]
        public void CreateGrid_DataSourceParameterNull()
        {
            const String testDataSource = null;
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021"); 

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<ArgumentException>(delegate
                {
                    testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testDataSource, testDate, testCreateDateTime);
                });

                Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
                Assert.That(e.ParamName == "dataSource");
            }
        }

        [Test]
        public void CreateGrid_DataSourceParameterWhitespace()
        {
            const String testDataSource = " ";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<ArgumentException>(delegate
                {
                    testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testDataSource, testDate, testCreateDateTime);
                });

                Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
                Assert.That(e.ParamName == "dataSource");
            }
        }

        [Test]
        public void CreateGrid_ExceptionRetrievingLatestGridVersion()
        {
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            String expectedMaxIdQueryText = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testDataSource, testDate, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                Assert.That(e.Message, Does.StartWith($"Failed to retrieve latest grid version number while inserting stock price grid for datasource 'Refinitiv', date '2026-05-16' into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_ExceptionInserting()
        {
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            String expectedMaxIdQueryText = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["MaxId"].Returns<Object>(1);
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testDataSource, testDate, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021").ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(e.Message, Does.StartWith($"Failed to insert stock price grid for datasource 'Refinitiv', date '2026-05-16', and version 2 into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_GridAlreadyExists()
        {
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            String expectedMaxIdQueryText = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["MaxId"].Returns<Object>(1);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                Int64 result = testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testDataSource, testDate, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021").ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(result == 2);
            }
        }

        [Test]
        public void CreateGrid_NoGridExists()
        {
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            String expectedMaxIdQueryText = @$"
            SELECT  MAX(Id) AS MaxId
            FROM    StockPriceGrids 
            WHERE   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                Int64 result = testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testDataSource, testDate, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactionSql23DateStyle));
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 1);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021").ToString(transactionSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(result == 1);
            }
        }

        [Test]
        public void ExecuteNonQueryWithDeadlockRetry_DeadlockExceptionAndSuccessOnFirstRetry()
        {
            SqlException deadLockException = GetSqlException(1205, "Transaction (Process ID 123) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.", 2);
            Boolean firstCall = true;
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) =>
            {
                if (firstCall == true)
                {
                    firstCall = false;
                    throw deadLockException;
                }
            });

            using (var connection = new SqlConnection(testConnectionString))
            using (var command = new SqlCommand())
            {
                testStockPricePersister.ExecuteNonQueryWithDeadlockRetry(connection, null, command);

                mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        [Test]
        public void ExecuteNonQueryWithDeadlockRetry_DeadlockExceptionAndFailureOnAllRetries()
        {

            SqlException deadLockException = GetSqlException(1205, "Transaction (Process ID 123) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.", 2);
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw deadLockException);

            using (var connection = new SqlConnection(testConnectionString))
            using (var command = new SqlCommand())
            {
                var e = Assert.Throws<AggregateException>(delegate
                {
                    testStockPricePersister.ExecuteNonQueryWithDeadlockRetry(connection, null, command);
                });
                
                mockSqlCommandShim.Received(6).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        #region Private/Protected Methods

        // Base of Below courtesy of https://blog.jonathanchannon.com/2014-01-02-unit-testing-with-sqlexception/ (required a few tweaks to get to the pass the right params to SqlError constructor)
        private SqlException GetSqlException(Int32 errorNumber, String errorMessage, Int32 constructorIndex)
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

        private T ConstructObject<T>(params object[] parameters)
        {
            ConstructorInfo constructor = typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];

            return (T)constructor.Invoke(parameters);
        }

        #endregion

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

            public new (Int32, DateTime) GetLatestGridVersion(SqlConnection connection, String dataSource, DateOnly date)
            {
                return base.GetLatestGridVersion(connection, dataSource, date);
            }

            public new IEnumerable<StockPricePTO> GetExistingGrid(SqlConnection connection, String dataSource, DateOnly date, DateTime transactionTimestamp)
            {
                return base.GetExistingGrid(connection, dataSource, date, transactionTimestamp);
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

            public new Int64 CreateGrid(SqlConnection readConnection, SqlConnection writeConnection, SqlTransaction transaction, String dataSource, DateOnly date, DateTime createDateTime)
            {
                return base.CreateGrid(readConnection, writeConnection, transaction, dataSource, date, createDateTime);
            }

            public new void ExecuteNonQueryWithDeadlockRetry(SqlConnection connection, SqlTransaction transaction, SqlCommand command)
            {
                base.ExecuteNonQueryWithDeadlockRetry(connection, transaction, command);
            }
        }

        #pragma warning restore 1591

        #endregion
    }
}
