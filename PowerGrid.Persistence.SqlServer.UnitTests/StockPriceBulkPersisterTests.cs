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
using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using NSubstitute;
using NUnit.Framework;

namespace PowerGrid.Persistence.SqlServer.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Persistence.SqlServer.StockPriceBulkPersister class.
    /// </summary>
    public class StockPriceBulkPersisterTests : PersisterTestsBase
    {
        private StockPriceBulkPersister testStockPriceBulkPersister;

        [SetUp]
        protected override void SetUp()
        {
            base.SetUp();
            Int32 bufferSizeLimit = 3;
            testStockPriceBulkPersister = new StockPriceBulkPersister(testConnectionString, bufferSizeLimit, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
        }

        [Test]
        public void Constructor_BufferSizeLimitLessThan1()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                testStockPriceBulkPersister = new StockPriceBulkPersister(testConnectionString, 0, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
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
            ORDER  BY Company;";
            String expectedMaxVersionQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource 
              AND   [Date] = CONVERT(date, @Date, 23);
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
                testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

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
            ORDER  BY Company;";
            String expectedMaxVersionQueryText = @$"
            SELECT  MAX([Version]) AS MaxVersion 
            FROM    StockPriceGrids 
            WHERE   Tag = @Tag 
              AND   DataSource = @DataSource
              AND   [Date] = CONVERT(date, @Date, 23);
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
                testStockPriceBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
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
            ORDER  BY Company;";
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
            // calling sp -> could I check the values in the DataTable with an Action? -> or maybe at least count of rows
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Is<DataTable>(DataTableWithRowCount(3)));
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
            Assert.That(resultVersion == 2);
            Assert.That(resultStatistics.ItemsAddedCount == 2);
            Assert.That(resultStatistics.ItemsUpdatedCount == 2);
            Assert.That(resultStatistics.ItemsDeletedCount == 2);
        }

        #region Private/Protected Methods

        protected Expression<Predicate<DataTable>> DataTableWithRowCount(Int32 expectedRowCount)
        {
            // return (DataTable actual) => actual.Rows.Count == expectedRowCount;
            return (DataTable actual) => EnableDebugging(actual, expectedRowCount);
        }

        protected Boolean EnableDebugging(DataTable actual, Int32 expectedRowCount)
        {
            return actual.Rows.Count == expectedRowCount;
        }

        /*
        /// <summary>
        /// NSubstitute argument matcher for <see cref="DataTable"/> arguments, which checks that the argument has a specified row count.
        /// </summary>
        /// <param name="expectedRowCount">The number of rows the argument is expected to have.</param>
        protected class DataTableWithRowCount(Int32 expectedRowCount) : IArgumentMatcher<DataTable>, IDescribeNonMatches
        {
            /// <inheritdoc/>
            public String DescribeFor(Object argument)
            {
                return $"{nameof(DataTable)} parameter was expected to contain {expectedRowCount} rows, but contained {((DataTable)argument).Rows.Count}";
            }

            /// <inheritdoc/>
            public Boolean IsSatisfiedBy(DataTable? argument)
            {
                return (argument != null && argument.Rows.Count == expectedRowCount);
            }
        }
        */
        #endregion
    }
}
