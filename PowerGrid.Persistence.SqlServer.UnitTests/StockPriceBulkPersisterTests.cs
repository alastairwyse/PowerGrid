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
using Microsoft.Data.SqlClient;
using PowerGrid.Grids;
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
            throw new NotImplementedException();
        }
    }
}
