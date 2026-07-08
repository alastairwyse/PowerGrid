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
using PowerGrid.Core;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using ApplicationLogging;
using ApplicationMetrics;
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
        private const String transactSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        private const String transactSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";
        private const String testConnectionString = "Server=127.0.0.1;Database=PowerGrid;User Id=user;Password=pwd=%X9sjQb;Encrypt=false;Authentication=SqlPassword";

        private TestUtilities utils;
        private List<SqlRetryingEventArgs> connectionRetryActionInvocationParameters;
        private EventHandler<SqlRetryingEventArgs> connectionRetryAction;
        private IApplicationLogger mockLogger;
        private IMetricLogger mockMetricLogger;
        private IDateTimeProvider mockDateTimeProvider;
        private ISqlConnectionShim mockSqlConnectionShim;
        private ISqlTransactionShim mockSqlTransactionShim;
        private ISqlCommandShim mockSqlCommandShim;
        private StockPricePersisterWithProtectedMembers testStockPricePersister;

        [SetUp]
        protected void SetUp()
        {
            mockLogger = Substitute.For<IApplicationLogger>();
            mockMetricLogger = Substitute.For<IMetricLogger>();
            mockDateTimeProvider = Substitute.For<IDateTimeProvider>();
            mockSqlConnectionShim = Substitute.For<ISqlConnectionShim>();
            mockSqlTransactionShim = Substitute.For<ISqlTransactionShim>();
            mockSqlCommandShim = Substitute.For<ISqlCommandShim>();
            utils = new TestUtilities();
            testStockPricePersister = new StockPricePersisterWithProtectedMembers(testConnectionString, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
        }

        [Test]
        public void PersistGrid_GridItemsParameterEmpty()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-29");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);

            var e = Assert.Throws<ArgumentException>(delegate
            {
                testStockPricePersister.PersistGrid(testOuterKeyProperties, new List<StockPrice>());
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'items' contained no items."));
            Assert.That(e.ParamName == "items");
        }

        [Test]
        public void PersistGrid_ExceptionConnectingToSqlServer()
        {
            const String testTag = "Calibration";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-25");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate); 
            List<StockPrice> testGridItems = new()
            {
                new StockPrice("Hitachi", 4732)
            };
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlConnectionShim.When((shim) => shim.Open(Arg.Any<SqlConnection>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            Assert.That(e.Message, Does.StartWith($"Failed to connect to SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void PersistGrid_NewGridItemPriceLessThan0()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String sonyCompany = "Sony";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(canonCompany, -1),
                new StockPrice(hitachiCompany, 4732)
            };
            List<StockPriceGridItemPTO> existingGridItems = new()
            {
                new StockPriceGridItemPTO(1L, testTag, testDataSource, testDate, hitachiCompany, 4733, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(2L, testTag, testDataSource, testDate, sonyCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedReadExistingGridCommandText = @$"
            SELECT Id, 
                   Tag, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  Tag = @Tag 
              AND  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY Company COLLATE Latin1_General_BIN2;
            ";
            String expectedMaxIdQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedGridInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @Tag, 
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
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });
            
            Assert.That(e.Message, Does.StartWith($"Failed to persist grid to SQL Server."));
            Assert.That(e.InnerException.Message, Does.StartWith($"Failed to compare new stock price grid to existing grid in SQL Server for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-16' }}, and transaction time '2026-05-16T16:36:41.0000023'."));
            Assert.That(e.InnerException.InnerException is GridContentsValidationException<StockPrice>);
            GridContentsValidationException<StockPrice> innerInnerException = (GridContentsValidationException<StockPrice>)e.InnerException.InnerException;
            Assert.That(innerInnerException.Message, Does.StartWith($"Failed to validate item in grid."));
            Assert.That(innerInnerException.GridItem == testGridItems[0]);
            Assert.That(innerInnerException.InnerException.Message == $"StockPrice with StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-16' }}, and Company 'Canon' has negative Price -1.");
        }

        [Test]
        public void PersistGrid_DuplicateGridItems()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String sonyCompany = "Sony";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(canonCompany, 4732),
                new StockPrice(canonCompany, 4733)
            };
            List<StockPriceGridItemPTO> existingGridItems = new()
            {
                new StockPriceGridItemPTO(1L, testTag, testDataSource, testDate, hitachiCompany, 4733, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(2L, testTag, testDataSource, testDate, sonyCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedReadExistingGridCommandText = @$"
            SELECT Id, 
                   Tag, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  Tag = @Tag 
              AND  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY Company COLLATE Latin1_General_BIN2;
            ";
            String expectedMaxIdQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedGridInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @Tag, 
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
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

            Assert.That(e.Message, Does.StartWith($"Failed to persist grid to SQL Server."));
            Assert.That(e.InnerException.Message, Does.StartWith($"Failed to compare new stock price grid to existing grid in SQL Server for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-16' }}, and transaction time '2026-05-16T16:36:41.0000023'."));
            Assert.That(e.InnerException.InnerException is GridContentsDuplicateItemsException<StockPrice>);
            GridContentsDuplicateItemsException<StockPrice> innerInnerException = (GridContentsDuplicateItemsException<StockPrice>)e.InnerException.InnerException;
            Assert.That(innerInnerException.Message, Does.StartWith($"Grid contains items with duplicate key values."));
            Assert.That(innerInnerException.GridItem == testGridItems[1]);
        }

        [Test]
        public void PersistGrid()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String sonyCompany = "Sony";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(canonCompany, 4441),
                new StockPrice(hitachiCompany, 4732)
            };
            List<StockPriceGridItemPTO> existingGridItems = new()
            {
                new StockPriceGridItemPTO(1L, testTag, testDataSource, testDate, hitachiCompany, 4733, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(2L, testTag, testDataSource, testDate, sonyCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedReadExistingGridCommandText = @$"
            SELECT Id, 
                   Tag, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  Tag = @Tag 
              AND  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY Company COLLATE Latin1_General_BIN2;
            ";
            String expectedMaxIdQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedGridInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @Tag, 
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
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testStockPricePersister.PersistGrid(testOuterKeyProperties, testGridItems);

            mockSqlConnectionShim.Received(2).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(4).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(2).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
            mockSqlCommandShim.Received(8).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(8).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(6).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlConnectionShim.Received(1).BeginTransaction(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(5).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(5).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(5).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactSql126DateStyle));
            mockSqlTransactionShim.Received(1).Commit(null);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
            mockSqlCommandShim.Received(5).SetTransaction(Arg.Any<SqlCommand>(), null);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridInsertStatementText);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactSql126DateStyle));
            Assert.That(resultVersion == 2);
            Assert.That(resultStatistics.ItemsAddedCount == 1);
            Assert.That(resultStatistics.ItemsUpdatedCount == 1);
            Assert.That(resultStatistics.ItemsDeletedCount == 1);
        }

        [Test]
        public void GetGrid_VersionParameterLessThan1()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);

            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                new List<StockPriceGridItemPTO>(testStockPricePersister.GetGrid(testOuterKeyProperties, 0));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'version' with value 0 must be greater than 0."));
            Assert.That(e.ParamName == "version");
        }

        [Test]
        public void GetGrid_ExceptionConnectingToSqlServer()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-25");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            var mockException = new Exception("Mock exception");
            mockSqlConnectionShim.When((shim) => shim.Open(Arg.Any<SqlConnection>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                List<StockPriceGridItemPTO> results = new(testStockPricePersister.GetGrid(testOuterKeyProperties, 1));
            });

            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            Assert.That(e.Message, Does.StartWith($"Failed to connect to SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGrid()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-05");
            Int32 testVersion = 13;
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-06-03 23:54:31.0000202");
            String expectedVersionQueryCommandText = @$"
            SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = @Version;
            ";
            String expectedGridQueryCommandText = @$"
            SELECT Id, 
                   Tag, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  Tag = @Tag
              AND  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY Company COLLATE Latin1_General_BIN2;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false, true, false);
            // Mock returns for grid version query
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-03T23:54:31.0000202");
            // Mock returns for grid contents query
            mockDataReader["Id"].Returns<Object>(1L);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>("Canon");
            mockDataReader["Price"].Returns<Object>(new Decimal(4216));
            mockDataReader["TransactionFrom"].Returns<Object>("2026-06-05T11:12:41.0000303");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");

            List<StockPriceGridItemPTO> results = new(testStockPricePersister.GetGrid(testOuterKeyProperties, testVersion));

            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedVersionQueryCommandText);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridQueryCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, testTransactionTimestamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            Assert.That(results.Count == 1);
            Assert.That(results[0].Id == 1);
            Assert.That(results[0].Tag == testTag);
            Assert.That(results[0].DataSource == testDataSource);
            Assert.That(results[0].Date == testDate);
            Assert.That(results[0].Company == "Canon");
            Assert.That(results[0].Price == 4216);
            Assert.That(results[0].TransactionFrom == utils.CreateDataTimeFromString("2026-06-05 11:12:41.0000303"));
            Assert.That(results[0].TransactionFrom.Kind == DateTimeKind.Utc);
            Assert.That(results[0].TransactionTo == utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            Assert.That(results[0].TransactionTo.Kind == DateTimeKind.Utc);
        }

        [Test]
        public void GetGridDetailsStockPriceGridOuterKeyPropertiesOverload_ExceptionReading()
        {
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-21");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.GetGridDetails(testOuterKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            Assert.That(e.Message, Does.StartWith($"Failed to read grid details for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Reuters', Date = '2026-06-21' }} from SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGridDetailsStockPriceGridOuterKeyPropertiesOverload()
        {
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-21");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                true, true, false
            );
            mockDataReader["Version"].Returns<Object>(1, 2);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-30T13:02:53.1837676", "2026-06-09T13:02:03.9134273");

            IList<GridVersionAndTransactionTimestamp> result = testStockPricePersister.GetGridDetails(testOuterKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
            mockDataReader.Received(3).Read();
            mockSqlConnectionShim.Received(1).Close(Arg.Any<SqlConnection>());
            Assert.That(result.Count == 2);
            Assert.That(result[0].Version == 1);
            Assert.That(result[0].TransactionTimestamp == utils.CreateDataTimeFromString("2026-05-30 13:02:53.1837676"));
            Assert.That(result[1].Version == 2);
            Assert.That(result[1].TransactionTimestamp == utils.CreateDataTimeFromString("2026-06-09 13:02:03.9134273"));
        }

        [Test]
        public void GetGridDetailsGridCommonKeyPropertiesOverload_ExceptionReading()
        {
            const String testTag = "Market";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedCommandText = @$"
            SELECT  DataSource, 
                    CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                    [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag;
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.GetGridDetails(testCommonKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            Assert.That(e.Message, Does.StartWith($"Failed to read grid details for GridCommonKeyProperties {{ Tag = 'Market' }} from SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGridDetailsGridCommonKeyPropertiesOverload()
        {
            const String testTag = "Calibrated";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedCommandText = @$"
            SELECT  DataSource, 
                    CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                    [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag;
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                true, true, true, false
            );
            mockDataReader["DataSource"].Returns<Object>("Bloomberg", "Bloomberg", "Reuters");
            mockDataReader["Date"].Returns<Object>("2026-05-30", "2026-05-30", "2026-05-31");
            mockDataReader["Version"].Returns<Object>(1, 2, 1);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-30T13:02:53.1837676", "2026-06-09T13:02:03.9134273", "2026-06-23T21:55:56.9750913");

            IList<Tuple<StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> result = testStockPricePersister.GetGridDetails(testCommonKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
            mockDataReader.Received(4).Read();
            mockSqlConnectionShim.Received(1).Close(Arg.Any<SqlConnection>());
            Assert.That(result.Count == 3);
            Assert.That(result[0].Item1.Tag == testTag);
            Assert.That(result[0].Item1.DataSource == "Bloomberg");
            Assert.That(result[0].Item1.Date == utils.CreateDateOnlyFromString("2026-05-30"));
            Assert.That(result[0].Item2.Version == 1);
            Assert.That(result[0].Item2.TransactionTimestamp == utils.CreateDataTimeFromString("2026-05-30 13:02:53.1837676"));
            Assert.That(result[1].Item1.Tag == testTag);
            Assert.That(result[1].Item1.DataSource == "Bloomberg");
            Assert.That(result[1].Item1.Date == utils.CreateDateOnlyFromString("2026-05-30"));
            Assert.That(result[1].Item2.Version == 2);
            Assert.That(result[1].Item2.TransactionTimestamp == utils.CreateDataTimeFromString("2026-06-09 13:02:03.9134273"));
            Assert.That(result[2].Item1.Tag == testTag);
            Assert.That(result[2].Item1.DataSource == "Reuters");
            Assert.That(result[2].Item1.Date == utils.CreateDateOnlyFromString("2026-05-31"));
            Assert.That(result[2].Item2.Version == 1);
            Assert.That(result[2].Item2.TransactionTimestamp == utils.CreateDataTimeFromString("2026-06-23 21:55:56.9750913"));
        }

        [Test]
        public void SoftDeleteLatestGrid_ExceptionConnectingToSqlServer()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-25");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlConnectionShim.When((shim) => shim.Open(Arg.Any<SqlConnection>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.SoftDeleteLatestGrid(testOuterKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            Assert.That(e.Message, Does.StartWith($"Failed to connect to SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void SoftDeleteLatestGrid_GridDoesntExist()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-26");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.SoftDeleteLatestGrid(testOuterKeyProperties);
            });

            Assert.That(e.Message, Does.StartWith($"Stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-06-26' }} does not exist."));
        }

        [Test]
        public void SoftDeleteLatestGrid_ExceptionDeleting()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-26");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime testDeleteTimestamp = utils.CreateDataTimeFromString("2026-06-26 22:04:21.0000032");
            String expectedDeleteCommandText = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   CONVERT(datetime2, @CurrentDateTime, 126) BETWEEN TransactionFrom AND TransactionTo;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-26T21:56:42.0000031");
            mockDateTimeProvider.UtcNow().Returns<DateTime>(testDeleteTimestamp); 
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.SoftDeleteLatestGrid(testOuterKeyProperties);
            });
            
            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CurrentDateTime", SqlDbType.NVarChar, testDeleteTimestamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, testDeleteTimestamp.AddTicks(-1).ToString(transactSql126DateStyle));
            Assert.That(e.Message, Does.StartWith($"Failed to delete latest grid items for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-06-26' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void SoftDeleteLatestGrid()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-26");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime testDeleteTimestamp = utils.CreateDataTimeFromString("2026-06-26 22:04:21.0000032");
            String expectedDeleteCommandText = @$"
            UPDATE  StockPrices 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   CONVERT(datetime2, @CurrentDateTime, 126) BETWEEN TransactionFrom AND TransactionTo;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-26T21:56:42.0000031");
            mockDateTimeProvider.UtcNow().Returns<DateTime>(testDeleteTimestamp);

            testStockPricePersister.SoftDeleteLatestGrid(testOuterKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CurrentDateTime", SqlDbType.NVarChar, testDeleteTimestamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, testDeleteTimestamp.AddTicks(-1).ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlTransactionShim.Received(1).Commit(Arg.Any<SqlTransaction>());
            mockSqlConnectionShim.Close(Arg.Any<SqlConnection>());
        }

        [Test]
        public void HardDeleteGridsStockPriceGridOuterKeyPropertiesOverload_ExceptionDeleting()
        {
            const String testTag = "Calibration";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-27");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedStockPriceGridsDeleteCommandText = @$"
            DELETE 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);
            
            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.HardDeleteGrids(testOuterKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedStockPriceGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            Assert.That(e.Message, Does.StartWith($"Failed to delete grids for StockPriceGridOuterKeyProperties {{ Tag = 'Calibration', DataSource = 'Bloomberg', Date = '2026-06-27' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void HardDeleteGridsStockPriceGridOuterKeyPropertiesOverload()
        {
            const String testTag = "Calibration";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-27");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedStockPriceGridsDeleteCommandText = @$"
            DELETE 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedStockPricesDeleteCommandText = @$"
            DELETE 
            FROM    StockPrices 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));

            testStockPricePersister.HardDeleteGrids(testOuterKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedStockPriceGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedStockPricesDeleteCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlTransactionShim.Received(1).Commit(Arg.Any<SqlTransaction>());
            mockSqlConnectionShim.Close(Arg.Any<SqlConnection>());
        }

        [Test]
        public void HardDeleteGridsCommonKeyPropertiesOverload_ExceptionDeleting()
        {
            const String testTag = "Calibration";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedStockPriceGridsDeleteCommandText = @$"
            DELETE 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag;
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPricePersister.HardDeleteGrids(testCommonKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedStockPriceGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            Assert.That(e.Message, Does.StartWith($"Failed to delete grids for GridCommonKeyProperties {{ Tag = 'Calibration' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void HardDeleteGridsCommonKeyPropertiesOverload()
        {
            const String testTag = "Calibration";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedStockPriceGridsDeleteCommandText = @$"
            DELETE 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag;
            ";
            String expectedStockPricesDeleteCommandText = @$"
            DELETE 
            FROM    StockPrices 
            WHERE   Tag = @Tag;
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));

            testStockPricePersister.HardDeleteGrids(testCommonKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedStockPriceGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedStockPricesDeleteCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlTransactionShim.Received(1).Commit(Arg.Any<SqlTransaction>());
            mockSqlConnectionShim.Close(Arg.Any<SqlConnection>());
        }

        [Test]
        public void GetLatestGridVersion_ExceptionReading()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   Tag = @Tag 
                        AND   DataSource = @DataSource 
                        AND   [Date] = CONVERT(date, @Date, 23) 
                    );
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.GetLatestGridVersion(connection, testOuterKeyProperties);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to read latest stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-16' }} from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetLatestGridVersion_NoVersionExists()
        {
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   Tag = @Tag 
                        AND   DataSource = @DataSource 
                        AND   [Date] = CONVERT(date, @Date, 23) 
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testStockPricePersister.GetLatestGridVersion(connection, testOuterKeyProperties);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                Assert.That(versionNumberResult == 0);
                Assert.That(transactionTimestampResult == DateTime.MinValue);
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetLatestGridVersion_MultipleRecordsReturned()
        {
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   Tag = @Tag 
                        AND   DataSource = @DataSource 
                        AND   [Date] = CONVERT(date, @Date, 23) 
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
                    testStockPricePersister.GetLatestGridVersion(connection, testOuterKeyProperties);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                Assert.That(e.Message, Does.StartWith($"Failed to read latest stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Reuters', Date = '2026-05-16' }} from SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Read multiple results from SQL Server when attempting to retrieve latest stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Reuters', Date = '2026-05-16' }}."));
            }
        }

        [Test]
        public void GetLatestGridVersion()
        {
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  [Version] AS [Version], 
                    CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = 
                    (
                      SELECT  MAX([Version])
                      FROM    StockPriceGrids 
                      WHERE   Tag = @Tag 
                        AND   DataSource = @DataSource 
                        AND   [Date] = CONVERT(date, @Date, 23) 
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-16T13:39:41.0000013");

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testStockPricePersister.GetLatestGridVersion(connection, testOuterKeyProperties);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                Assert.That(versionNumberResult == 3);
                Assert.That(transactionTimestampResult == utils.CreateDataTimeFromString("2026-05-16 13:39:41.0000013"));
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_MultipleRecordsReturned()
        {
            const String testTag = "Calibrated";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-01");
            Int32 testVersion = 9;
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = @Version;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-01T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                Assert.That(e.Message, Does.StartWith($"Failed to read stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 9 from SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Read multiple results from SQL Server when attempting to retrieve stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 9."));
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_GridDoesntExist()
        {
            const String testTag = "Calibrated";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-01");
            Int32 testVersion = 8;
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = @Version;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-01T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                Assert.That(e.Message, Does.StartWith($"Stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 8 did not exist."));
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_ExceptionReading()
        {
            const String testTag = "Calibrated";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-01");
            Int32 testVersion = 7;
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = @Version;
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                Assert.That(e.Message, Does.StartWith($"Failed to read stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 7 from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetGridTransactionTimestamp()
        {
            const String testTag = "Calibrated";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-01");
            Int32 testVersion = 8;
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
            SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23) 
              AND   [Version] = @Version;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-01T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                DateTime result = testStockPricePersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                Assert.That(utils.CreateDataTimeFromString("2026-06-01 23:02:45.0000101") == result);
            }
        }

        [Test]
        public void GetExistingGrid_ExceptionReading()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-05-16 11:45:40.0000012");
            String expectedCommandText = @$"
            SELECT Id, 
                   Tag, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  Tag = @Tag
              AND  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY Company COLLATE Latin1_General_BIN2;
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    List<StockPriceGridItemPTO> results = new(testStockPricePersister.GetGrid(connection, testOuterKeyProperties, testTransactionTimestamp));
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to read stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-16' }}, and transaction timestamp '2026-05-16 11:45:40.0000012' from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetGridTransactionTimestampOverload()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-05-16 11:45:40.0000012");
            String expectedCommandText = @$"
            SELECT Id, 
                   Tag, 
                   DataSource, 
                   CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                   Company, 
                   Price, 
                   CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                   CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
            FROM   StockPrices 
            WHERE  Tag = @Tag
              AND  DataSource = @DataSource
              AND  [Date] = CONVERT(date, @Date, 23) 
              AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo
            ORDER  BY Company COLLATE Latin1_General_BIN2;
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Id"].Returns<Object>(1L);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>("Canon");
            mockDataReader["Price"].Returns<Object>(new Decimal(4215));
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");

            using (var connection = new SqlConnection(testConnectionString))
            {
                List<StockPriceGridItemPTO> results = new(testStockPricePersister.GetGrid(connection, testOuterKeyProperties, testTransactionTimestamp));

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, testTransactionTimestamp.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                Assert.That(results.Count == 1);
                Assert.That(results[0].Id == 1);
                Assert.That(results[0].Tag == testTag);
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
        public void InsertGridItem_ExceptionInserting()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-08");
            const String testCompany = "Hitachi";
            StockPriceGridItem testItem = new(testTag, testDataSource, testDate, testCompany, 4732);
            DateTime testInsertDateTime = utils.CreateDataTimeFromString("2026-05-08 17:44:12.0000005");
            String expectedCommandText = @$"
            INSERT 
            INTO    StockPrices 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        Company, 
                        Price, 
                        TransactionFrom, 
                        TransactionTo 
                    )
            VALUES  (
                        @Tag, 
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
                Assert.That(e.Message, Does.StartWith($"Failed to insert StockPriceGridItem {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-08', Company = 'Hitachi', Price = 4732 }} into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void InsertGridItem()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-08");
            const String testCompany = "Hitachi";
            StockPriceGridItem testItem = new(testTag, testDataSource, testDate, testCompany, 4732);
            DateTime testInsertDateTime = utils.CreateDataTimeFromString("2026-05-08 17:44:12.0000005");
            String expectedCommandText = @$"
            INSERT 
            INTO    StockPrices 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        Company, 
                        Price, 
                        TransactionFrom, 
                        TransactionTo 
                    )
            VALUES  (
                        @Tag, 
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
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Company", SqlDbType.NVarChar, testCompany);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Price", SqlDbType.Money, testItem.Price);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@InsertDateTime", SqlDbType.NVarChar, testInsertDateTime.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TemporalMaximumDateTime", SqlDbType.NVarChar, DateTime.MaxValue.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        [Test]
        public void UpdateGridItem_ExceptionUpdating()
        {
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-13");
            const String testCompany = "Toyota";
            StockPriceGridItem testNewItem = new(testTag, testDataSource, testDate, testCompany, 3210);
            StockPriceGridItemPTO testSupersededItem = new(124, testTag, testDataSource, testDate, testCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testUpdateDateTime = utils.CreateDataTimeFromString("2026-05-14 10:51:21.0000011");
            String expectedInsertCommandText = @$"
            INSERT 
            INTO    StockPrices 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        Company, 
                        Price, 
                        TransactionFrom, 
                        TransactionTo 
                    )
            VALUES  (
                        @Tag, 
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
        public void UpdateGridItem()
        {
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-13");
            const String testCompany = "Toyota";
            StockPriceGridItem testNewItem = new(testTag, testDataSource, testDate, testCompany, 3210);
            StockPriceGridItemPTO testSupersededItemItem = new(124, testTag, testDataSource, testDate, testCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testUpdateDateTime = utils.CreateDataTimeFromString("2026-05-14 10:51:21.0000011");
            String expectedInsertCommandText = @$"
            INSERT 
            INTO    StockPrices 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        Company, 
                        Price, 
                        TransactionFrom, 
                        TransactionTo 
                    )
            VALUES  (
                        @Tag, 
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
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-14 10:51:21.0000010").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertCommandText);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Company", SqlDbType.NVarChar, testCompany);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Price", SqlDbType.Money, testNewItem.Price);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@InsertDateTime", SqlDbType.NVarChar, testUpdateDateTime.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TemporalMaximumDateTime", SqlDbType.NVarChar, DateTime.MaxValue.ToString(transactSql126DateStyle));
            }
        }
        
        [Test]
        public void DeleteGridItem_ExceptionDeleting()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-14");
            const String testCompany = "Sony";
            StockPriceGridItemPTO testItem = new(123, testTag, testDataSource, testDate, testCompany, 4732, utils.CreateDataTimeFromString("2026-03-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
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
        public void DeleteGridItem()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-14");
            const String testCompany = "Sony";
            StockPriceGridItemPTO testItem = new(123, testTag, testDataSource, testDate, testCompany, 4732, utils.CreateDataTimeFromString("2026-03-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
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
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-14 22:23:13.0000005").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        [Test]
        public void CreateGrid_ExceptionRetrievingLatestGridVersion()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxIdQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                Assert.That(e.Message, Does.StartWith($"Failed to retrieve latest grid version number while inserting stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-05-16' }} into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_ExceptionInserting()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxIdQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @Tag, 
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["MaxVersion"].Returns<Object>(1);
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(e.Message, Does.StartWith($"Failed to insert stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-05-16' }} and version 2 into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_GridAlreadyExists()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxIdQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @Tag, 
                        @DataSource, 
                        CONVERT(date, @Date, 23), 
                        @Version, 
                        CONVERT(datetime2, @CreateDateTime, 126)
                    );
            ";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["MaxVersion"].Returns<Object>(1);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                Int32 result = testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(result == 2);
            }
        }

        [Test]
        public void CreateGrid_NoGridExists()
        {
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxIdQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            String expectedInsertStatementText = @$"
            INSERT 
            INTO    StockPriceGrids 
                    (
                        Tag, 
                        DataSource, 
                        [Date], 
                        [Version], 
                        TransactionTimestamp
                    )
            VALUES  (
                        @Tag, 
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
                Int32 result = testStockPricePersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 1);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021").ToString(transactSql126DateStyle));
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
                IApplicationLogger logger,
                IMetricLogger metricLogger,
                IDateTimeProvider dateTimeProvider,
                ISqlConnectionShim sqlConnectionShim,
                ISqlTransactionShim sqlTransactionShim,
                ISqlCommandShim sqlCommandShim
            ) : base(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger, dateTimeProvider, sqlConnectionShim, sqlTransactionShim, sqlCommandShim)
            {
            }

            public new (Int32 Version, DateTime TransactionTimestamp) GetLatestGridVersion(SqlConnection connection, StockPriceGridOuterKeyProperties outerKeyProperties)
            {
                return base.GetLatestGridVersion(connection, outerKeyProperties);
            }

            public new DateTime GetGridTransactionTimestamp(SqlConnection connection, StockPriceGridOuterKeyProperties outerKeyProperties, Int32 version)
            {
                return base.GetGridTransactionTimestamp(connection, outerKeyProperties, version);
            }

            public new IEnumerable<StockPriceGridItemPTO> GetGrid(SqlConnection connection, StockPriceGridOuterKeyProperties outerKeyProperties, DateTime transactionTimestamp)
            {
                return base.GetGrid(connection, outerKeyProperties, transactionTimestamp);
            }

            public new void InsertGridItem(SqlConnection connection, SqlTransaction transaction, StockPriceGridItem item, DateTime insertDateTime)
            {
                base.InsertGridItem(connection, transaction, item, insertDateTime);
            }

            public new void UpdateGridItem(SqlConnection connection, SqlTransaction transaction, StockPriceGridItemPTO supersededItem, StockPriceGridItem newItem, DateTime udpateDateTime)
            {
                base.UpdateGridItem(connection, transaction, supersededItem, newItem, udpateDateTime);
            }

            public new void DeleteGridItem(SqlConnection connection, SqlTransaction transaction, StockPriceGridItemPTO item, DateTime deleteDateTime)
            {
                base.DeleteGridItem(connection, transaction, item, deleteDateTime);
            }

            public new Int32 CreateGrid(SqlConnection readConnection, SqlConnection writeConnection, SqlTransaction transaction, StockPriceGridOuterKeyProperties outerKeyProperties, DateTime createDateTime)
            {
                return base.CreateGrid(readConnection, writeConnection, transaction, outerKeyProperties, createDateTime);
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
