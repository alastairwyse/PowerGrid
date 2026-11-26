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
using System.Linq;
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence.Metrics;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer.Metrics;
using ApplicationLogging;
using ApplicationMetrics;
using NUnit.Framework;
using NSubstitute;

namespace PowerGrid.Persistence.SqlServer.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister class.
    /// </summary>
    public class StockPriceBulkPersisterTests : PersisterTestsBase
    {
        private StockPriceBulkPersisterWithProtectedMembers testStockPriceBulkPersister;

        [SetUp]
        protected override void SetUp()
        {
            base.SetUp();
            Int32 bufferSizeLimit = 3;
            testStockPriceBulkPersister = new StockPriceBulkPersisterWithProtectedMembers(testConnectionString, bufferSizeLimit, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
        }

        [Test]
        public void Constructor_BufferSizeLimitLessThan1()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                testStockPriceBulkPersister = new StockPriceBulkPersisterWithProtectedMembers(testConnectionString, 0, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'bufferSizeLimit' with value 0 must be greater than 0."));
            Assert.That(e.ParamName == "bufferSizeLimit");
            Assert.That((Int32)e.ActualValue == 0);
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
                testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, new List<StockPrice>());
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
                testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
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
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
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
            String expectedMaxVersionQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridBulkPersistTime>()).Returns(testBeginId);
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
                testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

            mockMetricLogger.Received(1).Begin(Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridBulkPersistTime>());
            Assert.That(e.Message, Does.StartWith($"Failed to persist grid to SQL Server."));
            Assert.That(e.InnerException.Message, Does.StartWith($"Failed to compare new stock price grid to existing grid in SQL Server for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-16' }}, and transaction time '2026-05-16T16:36:41.0000023'."));
            Assert.That(e.InnerException.InnerException is GridContentsValidationException<StockPrice>);
            GridContentsValidationException<StockPrice> innerInnerException = (GridContentsValidationException<StockPrice>)e.InnerException.InnerException;
            Assert.That(innerInnerException.Message, Does.StartWith($"Failed to validate item in grid."));
            Assert.That(innerInnerException.GridItem == testGridItems[0]);
            Assert.That(innerInnerException.InnerException.Message == $"StockPrice {{ Company = 'Canon', Price = -1 }} has negative Price -1.");
        }

        [Test]
        public void PersistGrid_DuplicateGridItems()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
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
            String expectedMaxVersionQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
            ";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridBulkPersistTime>()).Returns(testBeginId);
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
                testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

            mockMetricLogger.Received(1).Begin(Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridBulkPersistTime>());
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
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String hondaCompany = "Honda";
            const String mitsubishiCompany = "Mitsubishi";
            const String sonyCompany = "Sony";
            const String sumitomoCompany = "Sumitomo";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(hondaCompany, 1780),
                new StockPrice(mitsubishiCompany, 4732),
                new StockPrice(sonyCompany, 4137),
                new StockPrice(sumitomoCompany, 1691)
            };
            List<StockPriceGridItemPTO> existingGridItems = new()
            {
                new StockPriceGridItemPTO(1L, testTag, testDataSource, testDate, canonCompany, 4947, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(2L, testTag, testDataSource, testDate, hitachiCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(3L, testTag, testDataSource, testDate, hondaCompany, 1798, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(4L, testTag, testDataSource, testDate, mitsubishiCompany, 4715, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
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
                        );";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridBulkPersistTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockDateTimeProvider.UtcNow().Returns<DateTime>(transactionTimeStamp);
            mockSqlConnectionShim.BeginTransaction(Arg.Any<SqlConnection>()).Returns<SqlTransaction>((SqlTransaction)null);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                // Call to get existing grid contents
                true, true, true, true, false,
                // Call to get existing grid max id
                true, false
            );
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id, existingGridItems[1].Id);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company, existingGridItems[2].Company, existingGridItems[3].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price, existingGridItems[2].Price, existingGridItems[3].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);
            List<Int32> gridItemsStoredProcedureParameterRowCounts = new();
            List<Int32> gridItemIdsStoredProcedureParameterRowCounts = new();
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemIdsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);

            mockSqlConnectionShim.Received(2).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(4).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(2).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
            mockSqlCommandShim.Received(8).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(8).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(6).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlConnectionShim.Received(1).BeginTransaction(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(4).SetCommandType(Arg.Any<SqlCommand>(), CommandType.StoredProcedure);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Any<DataTable>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Any<DataTable>());
            mockSqlCommandShim.Received(4).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.DateTime2, transactionTimeStamp);
            mockSqlCommandShim.Received(5).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxVersionQueryText);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridInsertStatementText);
            mockSqlTransactionShim.Received(1).Commit(null);
            mockSqlConnectionShim.Received(2).Close(Arg.Any<SqlConnection>());
            mockMetricLogger.Received(1).Begin(Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<GridBulkPersisted>());
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsAdded>(), 2);
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsUpdated>(), 2);
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsDeleted>(), 2);
            Assert.That(resultVersion == 2);
            Assert.That(resultStatistics.ItemsAddedCount == 2);
            Assert.That(resultStatistics.ItemsUpdatedCount == 2);
            Assert.That(resultStatistics.ItemsDeletedCount == 2);
            Assert.That(gridItemsStoredProcedureParameterRowCounts.Count == 2);
            Assert.That(gridItemsStoredProcedureParameterRowCounts[0] == 3);
            Assert.That(gridItemsStoredProcedureParameterRowCounts[1] == 1);
            Assert.That(gridItemIdsStoredProcedureParameterRowCounts.Count == 2);
            Assert.That(gridItemIdsStoredProcedureParameterRowCounts[0] == 3);
            Assert.That(gridItemIdsStoredProcedureParameterRowCounts[1] == 1);
        }

        [Test]
        public void PersistGrid_ItemsRemainingInBufferAfterCompareCompletesArePersisted()
        {
            // Tests that if items remain in the buffers of the BulkAddEmitter and BulkDeleteEmitter classes after comparison between new and existing grid items is complete, those remaining items are also persisted

            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            const String hitachiCompany = "Hitachi";
            const String hondaCompany = "Honda";
            const String mitsubishiCompany = "Mitsubishi";
            const String sonyCompany = "Sony";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(hondaCompany, 1780),
                new StockPrice(mitsubishiCompany, 4732),
                new StockPrice(sonyCompany, 4137)
            };
            List<StockPriceGridItemPTO> existingGridItems = new()
            {
                new StockPriceGridItemPTO(2L, testTag, testDataSource, testDate, hitachiCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(3L, testTag, testDataSource, testDate, hondaCompany, 1798, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(4L, testTag, testDataSource, testDate, mitsubishiCompany, 4715, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
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
                        );";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridBulkPersistTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockDateTimeProvider.UtcNow().Returns<DateTime>(transactionTimeStamp);
            mockSqlConnectionShim.BeginTransaction(Arg.Any<SqlConnection>()).Returns<SqlTransaction>((SqlTransaction)null);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                // Call to get existing grid contents
                true, true, true, false,
                // Call to get existing grid max id
                true, false
            );
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id, existingGridItems[1].Id);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company, existingGridItems[2].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price, existingGridItems[2].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);
            List<Int32> gridItemsStoredProcedureParameterRowCounts = new();
            List<Int32> gridItemIdsStoredProcedureParameterRowCounts = new();
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemIdsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems, PersistComparisonStrategy.Syncronise);

            mockSqlConnectionShim.Received(2).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(4).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(2).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
            mockSqlCommandShim.Received(6).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(6).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(4).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlConnectionShim.Received(1).BeginTransaction(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandType(Arg.Any<SqlCommand>(), CommandType.StoredProcedure);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Any<DataTable>());
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Any<DataTable>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.DateTime2, transactionTimeStamp);
            mockSqlCommandShim.Received(3).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxVersionQueryText);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridInsertStatementText);
            mockSqlTransactionShim.Received(1).Commit(null);
            mockSqlConnectionShim.Received(2).Close(Arg.Any<SqlConnection>());
            mockMetricLogger.Received(1).Begin(Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<GridBulkPersisted>());
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsAdded>(), 1);
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsUpdated>(), 2);
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsDeleted>(), 1);
            Assert.That(resultVersion == 2);
            Assert.That(resultStatistics.ItemsAddedCount == 1);
            Assert.That(resultStatistics.ItemsUpdatedCount == 2);
            Assert.That(resultStatistics.ItemsDeletedCount == 1);
            Assert.That(gridItemsStoredProcedureParameterRowCounts.Count == 1);
            Assert.That(gridItemsStoredProcedureParameterRowCounts[0] == 3);
            Assert.That(gridItemIdsStoredProcedureParameterRowCounts.Count == 1);
            Assert.That(gridItemIdsStoredProcedureParameterRowCounts[0] == 3);
        }

        [Test]
        public void PersistGrid_AddAndUpdateOnlyPersistComparisonStrategy()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String hondaCompany = "Honda";
            const String mitsubishiCompany = "Mitsubishi";
            const String sonyCompany = "Sony";
            const String sumitomoCompany = "Sumitomo";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-05-16 16:36:41.0000023");
            List<StockPrice> testGridItems = new()
            {
                new StockPrice(hondaCompany, 1780),
                new StockPrice(mitsubishiCompany, 4732),
                new StockPrice(sonyCompany, 4137),
                new StockPrice(sumitomoCompany, 1691)
            };
            List<StockPriceGridItemPTO> existingGridItems = new()
            {
                new StockPriceGridItemPTO(1L, testTag, testDataSource, testDate, canonCompany, 4947, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(2L, testTag, testDataSource, testDate, hitachiCompany, 3209, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(3L, testTag, testDataSource, testDate, hondaCompany, 1798, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new StockPriceGridItemPTO(4L, testTag, testDataSource, testDate, mitsubishiCompany, 4715, utils.CreateDataTimeFromString("2026-03-02 09:06:09.0000026"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
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
                        );";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridBulkPersistTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockDateTimeProvider.UtcNow().Returns<DateTime>(transactionTimeStamp);
            mockSqlConnectionShim.BeginTransaction(Arg.Any<SqlConnection>()).Returns<SqlTransaction>((SqlTransaction)null);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                // Call to get existing grid contents
                true, true, true, true, false,
                // Call to get existing grid max id
                true, false
            );
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id, existingGridItems[1].Id);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>(testDataSource);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Company"].Returns<Object>(existingGridItems[0].Company, existingGridItems[1].Company, existingGridItems[2].Company, existingGridItems[3].Company);
            mockDataReader["Price"].Returns<Object>(existingGridItems[0].Price, existingGridItems[1].Price, existingGridItems[2].Price, existingGridItems[3].Price);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-05-15T09:05:40.0000012");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);
            List<Int32> gridItemsStoredProcedureParameterRowCounts = new();
            List<Int32> gridItemIdsStoredProcedureParameterRowCounts = new();
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemIdsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems, PersistComparisonStrategy.AddAndUpdateOnly);

            mockSqlConnectionShim.Received(2).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(4).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(2).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), "SET DEADLOCK_PRIORITY HIGH;");
            mockSqlCommandShim.Received(7).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(7).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(5).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlConnectionShim.Received(1).BeginTransaction(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(3).SetCommandType(Arg.Any<SqlCommand>(), CommandType.StoredProcedure);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Any<DataTable>());
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Any<DataTable>());
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.DateTime2, transactionTimeStamp);
            mockSqlCommandShim.Received(4).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxVersionQueryText);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridInsertStatementText);
            mockSqlTransactionShim.Received(1).Commit(null);
            mockSqlConnectionShim.Received(2).Close(Arg.Any<SqlConnection>());
            mockMetricLogger.Received(1).Begin(Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridBulkPersistTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<GridBulkPersisted>());
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsAdded>(), 2);
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsUpdated>(), 2);
            mockMetricLogger.Received(1).Add(Arg.Any<ComparedGridItemsDeleted>(), 0);
            Assert.That(resultVersion == 2);
            Assert.That(resultStatistics.ItemsAddedCount == 2);
            Assert.That(resultStatistics.ItemsUpdatedCount == 2);
            Assert.That(resultStatistics.ItemsDeletedCount == 0);
            Assert.That(gridItemsStoredProcedureParameterRowCounts.Count == 2);
            Assert.That(gridItemsStoredProcedureParameterRowCounts[0] == 3);
            Assert.That(gridItemsStoredProcedureParameterRowCounts[1] == 1);
            Assert.That(gridItemIdsStoredProcedureParameterRowCounts.Count == 1);
            Assert.That(gridItemIdsStoredProcedureParameterRowCounts[0] == 2);
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
                new List<StockPriceGridItemPTO>(testStockPriceBulkPersister.GetGrid(testOuterKeyProperties, 0));
            });

            Assert.That(mockMetricLogger.ReceivedCalls().Count() == 0);
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
                List<StockPriceGridItemPTO> results = new(testStockPriceBulkPersister.GetGrid(testOuterKeyProperties, 1));
            });

            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            Assert.That(mockMetricLogger.ReceivedCalls().Count() == 0);
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
                  AND   [Version] = @Version;";
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
                ORDER  BY Company;";
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

            List<StockPriceGridItemPTO> results = new(testStockPriceBulkPersister.GetGrid(testOuterKeyProperties, testVersion));

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
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-21");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        DataSource, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridDetailsByOuterKeyPropertiesRetrieveTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPriceBulkPersister.GetGridDetails(testOuterKeyProperties);
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
            mockMetricLogger.Received(1).Begin(Arg.Any<GridDetailsByOuterKeyPropertiesRetrieveTime>());
            mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridDetailsByOuterKeyPropertiesRetrieveTime>());
            Assert.That(mockMetricLogger.ReceivedCalls().Count() == 2);
            Assert.That(e.Message, Does.StartWith($"Failed to read grid details for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Reuters', Date = '2026-06-21' }} from SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGridDetailsStockPriceGridOuterKeyPropertiesOverload()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-21");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        DataSource, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridDetailsByOuterKeyPropertiesRetrieveTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                true, true, false
            );
            mockDataReader["Version"].Returns<Object>(1, 2);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-30T13:02:53.1837676", "2026-06-09T13:02:03.9134273");

            IList<GridVersionAndTransactionTimestamp> result = testStockPriceBulkPersister.GetGridDetails(testOuterKeyProperties);

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
            mockMetricLogger.Received(1).Begin(Arg.Any<GridDetailsByOuterKeyPropertiesRetrieveTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridDetailsByOuterKeyPropertiesRetrieveTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<GridDetailsByOuterKeyPropertiesRetrieved>());
            Assert.That(mockMetricLogger.ReceivedCalls().Count() == 3);
            Assert.That(result.Count == 2);
            Assert.That(result[0].Version == 1);
            Assert.That(result[0].TransactionTimestamp == utils.CreateDataTimeFromString("2026-05-30 13:02:53.1837676"));
            Assert.That(result[1].Version == 2);
            Assert.That(result[1].TransactionTimestamp == utils.CreateDataTimeFromString("2026-06-09 13:02:03.9134273"));
        }

        [Test]
        public void GetGridDetailsGridCommonKeyPropertiesOverload_ExceptionReading()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        DataSource, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridDetailsByCommonKeyPropertiesRetrieveTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPriceBulkPersister.GetGridDetails(testCommonKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockMetricLogger.Received(1).Begin(Arg.Any<GridDetailsByCommonKeyPropertiesRetrieveTime>());
            mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridDetailsByCommonKeyPropertiesRetrieveTime>());
            Assert.That(mockMetricLogger.ReceivedCalls().Count() == 2);
            Assert.That(e.Message, Does.StartWith($"Failed to read grid details for GridCommonKeyProperties {{ Tag = 'Market' }} from SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGridDetailsGridCommonKeyPropertiesOverload()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibrated";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        DataSource, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridDetailsByCommonKeyPropertiesRetrieveTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                true, true, true, false
            );
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["DataSource"].Returns<Object>("Bloomberg", "Bloomberg", "Reuters");
            mockDataReader["Date"].Returns<Object>("2026-05-30", "2026-05-30", "2026-05-31");
            mockDataReader["Version"].Returns<Object>(1, 2, 1);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-30T13:02:53.1837676", "2026-06-09T13:02:03.9134273", "2026-06-23T21:55:56.9750913");

            IList<Tuple<StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> result = testStockPriceBulkPersister.GetGridDetails(testCommonKeyProperties);

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
            mockMetricLogger.Received(1).Begin(Arg.Any<GridDetailsByCommonKeyPropertiesRetrieveTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridDetailsByCommonKeyPropertiesRetrieveTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<GridDetailsByCommonKeyPropertiesRetrieved>());
            Assert.That(mockMetricLogger.ReceivedCalls().Count() == 3);
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
                testStockPriceBulkPersister.SoftDeleteLatestGrid(testOuterKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            Assert.That(mockMetricLogger.ReceivedCalls().Count() == 0);
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
                testStockPriceBulkPersister.SoftDeleteLatestGrid(testOuterKeyProperties);
            });

            Assert.That(e.Message, Does.StartWith($"Stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-06-26' }} does not exist."));
        }

        [Test]
        public void SoftDeleteLatestGrid_ExceptionDeleting()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
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
                  AND   CONVERT(datetime2, @CurrentDateTime, 126) BETWEEN TransactionFrom AND TransactionTo;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<LatestGridSoftDeleteTime>()).Returns(testBeginId);
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
                testStockPriceBulkPersister.SoftDeleteLatestGrid(testOuterKeyProperties);
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
            mockMetricLogger.Received(1).Begin(Arg.Any<LatestGridSoftDeleteTime>());
            mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<LatestGridSoftDeleteTime>());
            Assert.That(e.Message, Does.StartWith($"Failed to delete latest grid items for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-06-26' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void SoftDeleteLatestGrid()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
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
                  AND   CONVERT(datetime2, @CurrentDateTime, 126) BETWEEN TransactionFrom AND TransactionTo;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<LatestGridSoftDeleteTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-26T21:56:42.0000031");
            mockDateTimeProvider.UtcNow().Returns<DateTime>(testDeleteTimestamp);

            testStockPriceBulkPersister.SoftDeleteLatestGrid(testOuterKeyProperties);

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
            mockMetricLogger.Received(1).Begin(Arg.Any<LatestGridSoftDeleteTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<LatestGridSoftDeleteTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<LatestGridSoftDeleted>());
        }

        [Test]
        public void HardDeleteGridsGridOuterKeyPropertiesOverload_ExceptionDeleting()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibration";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-27");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedStockPriceGridsDeleteCommandText = @$"
                DELETE 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridsByOuterKeyPropertiesHardDeleteTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPriceBulkPersister.HardDeleteGrids(testOuterKeyProperties);
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
            mockMetricLogger.Received(1).Begin(Arg.Any<GridsByOuterKeyPropertiesHardDeleteTime>());
            mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridsByOuterKeyPropertiesHardDeleteTime>());
            Assert.That(e.Message, Does.StartWith($"Failed to delete stock price grids for StockPriceGridOuterKeyProperties {{ Tag = 'Calibration', DataSource = 'Bloomberg', Date = '2026-06-27' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void HardDeleteGridsGridOuterKeyPropertiesOverload()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibration";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-27");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedStockPriceGridsDeleteCommandText = @$"
                DELETE 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
            String expectedStockPricesDeleteCommandText = @$"
                DELETE 
                FROM    StockPrices 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridsByOuterKeyPropertiesHardDeleteTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));

            testStockPriceBulkPersister.HardDeleteGrids(testOuterKeyProperties);

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
            mockMetricLogger.Received(1).Begin(Arg.Any<GridsByOuterKeyPropertiesHardDeleteTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridsByOuterKeyPropertiesHardDeleteTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<GridsByOuterKeyPropertiesHardDeleted>());
        }

        [Test]
        public void HardDeleteGridsCommonKeyPropertiesOverload_ExceptionDeleting()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibration";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedStockPriceGridsDeleteCommandText = @$"
                DELETE 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridsByCommonKeyPropertiesHardDeleteTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testStockPriceBulkPersister.HardDeleteGrids(testCommonKeyProperties);
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
            mockMetricLogger.Received(1).Begin(Arg.Any<GridsByCommonKeyPropertiesHardDeleteTime>());
            mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridsByCommonKeyPropertiesHardDeleteTime>());
            Assert.That(e.Message, Does.StartWith($"Failed to delete stock price grids for GridCommonKeyProperties {{ Tag = 'Calibration' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void HardDeleteGridsCommonKeyPropertiesOverload()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibration";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedStockPriceGridsDeleteCommandText = @$"
                DELETE 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag;";
            String expectedStockPricesDeleteCommandText = @$"
                DELETE 
                FROM    StockPrices 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockMetricLogger.Begin(Arg.Any<GridsByCommonKeyPropertiesHardDeleteTime>()).Returns(testBeginId);
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));

            testStockPriceBulkPersister.HardDeleteGrids(testCommonKeyProperties);

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
            mockMetricLogger.Received(1).Begin(Arg.Any<GridsByCommonKeyPropertiesHardDeleteTime>());
            mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridsByCommonKeyPropertiesHardDeleteTime>());
            mockMetricLogger.Received(1).Increment(Arg.Any<GridsByCommonKeyPropertiesHardDeleted>());
        }

        [Test]
        public void GetLatestGridVersion_ExceptionReading()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
                SELECT  [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23)
                        );";
            var mockException = new Exception("Mock exception");
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPriceBulkPersister.GetLatestGridVersion(connection, testOuterKeyProperties);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                Assert.That(e.Message, Does.StartWith($"Failed to read latest stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-16' }} from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetLatestGridVersion_NoVersionExists()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
                SELECT  [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23)
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testStockPriceBulkPersister.GetLatestGridVersion(connection, testOuterKeyProperties);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).Increment(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieved>());
                Assert.That(versionNumberResult == 0);
                Assert.That(transactionTimestampResult == DateTime.MinValue);
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetLatestGridVersion_MultipleRecordsReturned()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
                SELECT  [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23)
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, true);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-16T13:39:41.0000013");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPriceBulkPersister.GetLatestGridVersion(connection, testOuterKeyProperties);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                Assert.That(e.Message, Does.StartWith($"Failed to read latest stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Reuters', Date = '2026-05-16' }} from SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Read multiple results from SQL Server when attempting to retrieve latest stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Reuters', Date = '2026-05-16' }}."));
            }
        }

        [Test]
        public void GetLatestGridVersion()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Reuters";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedCommandText = @$"
                SELECT  [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23)
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-16T13:39:41.0000013");

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testStockPriceBulkPersister.GetLatestGridVersion(connection, testOuterKeyProperties);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).Increment(Arg.Any<GridMaximumVersionAndTransactionTimestampRetrieved>());
                Assert.That(versionNumberResult == 3);
                Assert.That(transactionTimestampResult == utils.CreateDataTimeFromString("2026-05-16 13:39:41.0000013"));
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_MultipleRecordsReturned()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibrated";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-01");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            Int32 testVersion = 9;
            String expectedCommandText = @$"
                SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Version] = @Version;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-01T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPriceBulkPersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                mockMetricLogger.Received(1).Begin(Arg.Any<GridTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridTransactionTimestampRetrieveTime>());
                Assert.That(e.Message, Does.StartWith($"Failed to read stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 9 from SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Read multiple results from SQL Server when attempting to retrieve stock price grid version for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 9."));
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_GridDoesntExist()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibrated";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-01");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            Int32 testVersion = 8;
            String expectedCommandText = @$"
                SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Version] = @Version;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-01T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<ArgumentException>(delegate
                {
                    testStockPriceBulkPersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                mockMetricLogger.Received(1).Begin(Arg.Any<GridTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridTransactionTimestampRetrieveTime>());
                Assert.That(e.Message, Does.StartWith($"Stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 8 did not exist."));
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_ExceptionReading()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Calibrated";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-06-01");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            Int32 testVersion = 7;
            String expectedCommandText = @$"
                SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Version] = @Version;";
            var mockException = new Exception("Mock exception");
            mockMetricLogger.Begin(Arg.Any<GridTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPriceBulkPersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                mockMetricLogger.Received(1).Begin(Arg.Any<GridTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridTransactionTimestampRetrieveTime>());
                Assert.That(e.Message, Does.StartWith($"Failed to read stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Calibrated', DataSource = 'Refinitiv', Date = '2026-06-01' }}, and version 7 from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetGridTransactionTimestamp()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
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
                  AND   [Version] = @Version;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridTransactionTimestampRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-06-01T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                DateTime result = testStockPriceBulkPersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                mockMetricLogger.Received(1).Begin(Arg.Any<GridTransactionTimestampRetrieveTime>());
                mockMetricLogger.Received(1).End(testBeginId, Arg.Any<GridTransactionTimestampRetrieveTime>());
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
                ORDER  BY Company;";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    List<StockPriceGridItemPTO> results = new(testStockPriceBulkPersister.GetGrid(connection, testOuterKeyProperties, testTransactionTimestamp));
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(mockMetricLogger.ReceivedCalls().Count() == 0);
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
                ORDER  BY Company;";
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
                List<StockPriceGridItemPTO> results = new(testStockPriceBulkPersister.GetGrid(connection, testOuterKeyProperties, testTransactionTimestamp));

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, testTransactionTimestamp.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                Assert.That(mockMetricLogger.ReceivedCalls().Count() == 0);
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
        public void CreateGrid_ExceptionRetrievingLatestGridVersion()
        {
            Guid testBeginId = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
            var mockException = new Exception("Mock exception");
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionRetrieveTime>()).Returns(testBeginId);
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testStockPriceBulkPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxVersionQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DataSource", SqlDbType.NVarChar, testDataSource);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionRetrieveTime>());
                mockMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<GridMaximumVersionRetrieveTime>());
                Assert.That(e.Message, Does.StartWith($"Failed to retrieve latest grid version number while inserting stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-05-16' }} into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_ExceptionInserting()
        {
            Guid testBeginId1 = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            Guid testBeginId2 = Guid.Parse("cd9d3c57-3b60-4abd-993b-f684caf529f1");
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
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
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionRetrieveTime>()).Returns(testBeginId1);
            mockMetricLogger.Begin(Arg.Any<GridCreateTime>()).Returns(testBeginId2);
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
                    testStockPriceBulkPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxVersionQueryText);
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
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionRetrieveTime>());
                mockMetricLogger.Received(1).End(testBeginId1, Arg.Any<GridMaximumVersionRetrieveTime>());
                mockMetricLogger.Received(1).Begin(Arg.Any<GridCreateTime>());
                mockMetricLogger.Received(1).CancelBegin(testBeginId2, Arg.Any<GridCreateTime>());
                Assert.That(e.Message, Does.StartWith($"Failed to insert stock price grid for StockPriceGridOuterKeyProperties {{ Tag = 'Market', DataSource = 'Refinitiv', Date = '2026-05-16' }} and version 2 into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_GridAlreadyExists()
        {
            Guid testBeginId1 = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            Guid testBeginId2 = Guid.Parse("cd9d3c57-3b60-4abd-993b-f684caf529f1");
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
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
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionRetrieveTime>()).Returns(testBeginId1);
            mockMetricLogger.Begin(Arg.Any<GridCreateTime>()).Returns(testBeginId2);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["MaxVersion"].Returns<Object>(1);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                Int32 result = testStockPriceBulkPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxVersionQueryText);
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
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionRetrieveTime>());
                mockMetricLogger.Received(1).End(testBeginId1, Arg.Any<GridMaximumVersionRetrieveTime>());
                mockMetricLogger.Received(1).Begin(Arg.Any<GridCreateTime>());
                mockMetricLogger.Received(1).End(testBeginId2, Arg.Any<GridCreateTime>());
                mockMetricLogger.Received(1).Increment(Arg.Any<GridCreated>());
                Assert.That(result == 2);
            }
        }

        [Test]
        public void CreateGrid_NoGridExists()
        {
            Guid testBeginId1 = Guid.Parse("39753d49-dfab-45cb-bd54-0efb1347660f");
            Guid testBeginId2 = Guid.Parse("cd9d3c57-3b60-4abd-993b-f684caf529f1");
            const String testTag = "Market";
            const String testDataSource = "Refinitiv";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-16");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-05-16 14:16:43.0000021");
            StockPriceGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDataSource, testDate);
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    StockPriceGrids 
                WHERE   Tag = @Tag 
                  AND   DataSource = @DataSource 
                  AND   [Date] = CONVERT(date, @Date, 23);";
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
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockMetricLogger.Begin(Arg.Any<GridMaximumVersionRetrieveTime>()).Returns(testBeginId1);
            mockMetricLogger.Begin(Arg.Any<GridCreateTime>()).Returns(testBeginId2);
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                Int32 result = testStockPriceBulkPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxVersionQueryText);
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
                mockMetricLogger.Received(1).Begin(Arg.Any<GridMaximumVersionRetrieveTime>());
                mockMetricLogger.Received(1).End(testBeginId1, Arg.Any<GridMaximumVersionRetrieveTime>());
                mockMetricLogger.Received(1).Begin(Arg.Any<GridCreateTime>());
                mockMetricLogger.Received(1).End(testBeginId2, Arg.Any<GridCreateTime>());
                mockMetricLogger.Received(1).Increment(Arg.Any<GridCreated>());
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
                testStockPriceBulkPersister.ExecuteNonQueryWithDeadlockRetry(connection, null, command);

                mockLogger.Received(1).Log(testStockPriceBulkPersister, LogLevel.Warning, "SQL Server error with number 1205 occurred when executing command.  Retrying in 0 seconds (retry 1 of 5).", Arg.Any<SqlException>());
                mockMetricLogger.Received(1).Increment(Arg.Any<SqlCommandExecutionRetried>());
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
                    testStockPriceBulkPersister.ExecuteNonQueryWithDeadlockRetry(connection, null, command);
                });

                mockLogger.Received(1).Log(testStockPriceBulkPersister, LogLevel.Warning, "SQL Server error with number 1205 occurred when executing command.  Retrying in 0 seconds (retry 1 of 5).", Arg.Any<SqlException>());
                mockLogger.Received(1).Log(testStockPriceBulkPersister, LogLevel.Warning, "SQL Server error with number 1205 occurred when executing command.  Retrying in 0 seconds (retry 2 of 5).", Arg.Any<SqlException>());
                mockLogger.Received(1).Log(testStockPriceBulkPersister, LogLevel.Warning, "SQL Server error with number 1205 occurred when executing command.  Retrying in 0 seconds (retry 3 of 5).", Arg.Any<SqlException>());
                mockLogger.Received(1).Log(testStockPriceBulkPersister, LogLevel.Warning, "SQL Server error with number 1205 occurred when executing command.  Retrying in 0 seconds (retry 4 of 5).", Arg.Any<SqlException>());
                mockLogger.Received(1).Log(testStockPriceBulkPersister, LogLevel.Warning, "SQL Server error with number 1205 occurred when executing command.  Retrying in 0 seconds (retry 5 of 5).", Arg.Any<SqlException>());
                mockMetricLogger.Received(5).Increment(Arg.Any<SqlCommandExecutionRetried>());
                mockSqlCommandShim.Received(6).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        #region Nested Classes

        #pragma warning disable 1591

        /// <summary>
        /// Version of the StockPriceBulkPersister class where private and protected methods are exposed as public so that they can be unit tested.
        /// </summary>
        protected class StockPriceBulkPersisterWithProtectedMembers : StockPriceBulkPersister
        {
            public StockPriceBulkPersisterWithProtectedMembers
            (
                String connectionString,
                Int32 bufferSizeLimit,
                Int32 retryCount,
                Int32 retryInterval,
                Int32 operationTimeout,
                IApplicationLogger logger,
                IMetricLogger metricLogger,
                IDateTimeProvider dateTimeProvider,
                ISqlConnectionShim sqlConnectionShim,
                ISqlTransactionShim sqlTransactionShim,
                ISqlCommandShim sqlCommandShim
            ) : base(connectionString, bufferSizeLimit, retryCount, retryInterval, operationTimeout, logger, metricLogger, dateTimeProvider, sqlConnectionShim, sqlTransactionShim, sqlCommandShim)
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
