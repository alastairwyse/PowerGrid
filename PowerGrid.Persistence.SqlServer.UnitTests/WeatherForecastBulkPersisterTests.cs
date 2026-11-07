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
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using NUnit.Framework;
using NSubstitute;

namespace PowerGrid.Persistence.SqlServer.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Persistence.SqlServer.WeatherForecastBulkPersister class.
    /// </summary>
    public class WeatherForecastBulkPersisterTests : PersisterTestsBase
    {
        private WeatherForecastBulkPersister testWeatherForecastBulkPersister;

        [SetUp]
        protected override void SetUp()
        {
            base.SetUp();
            Int32 bufferSizeLimit = 3;
            testWeatherForecastBulkPersister = new WeatherForecastBulkPersister(testConnectionString, bufferSizeLimit, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
        }

        [Test]
        public void Constructor_BufferSizeLimitLessThan1()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                testWeatherForecastBulkPersister = new WeatherForecastBulkPersister(testConnectionString, 0, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'bufferSizeLimit' with value 0 must be greater than 0."));
            Assert.That(e.ParamName == "bufferSizeLimit");
            Assert.That((Int32)e.ActualValue == 0);
        }


        [Test]
        public void PersistGrid_GridItemsParameterEmpty()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-22");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);

            var e = Assert.Throws<ArgumentException>(delegate
            {
                testWeatherForecastBulkPersister.PersistGrid(testOuterKeyProperties, new List<WeatherForecast>());
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'items' contained no items."));
            Assert.That(e.ParamName == "items");
        }

        [Test]
        public void PersistGrid_ExceptionConnectingToSqlServer()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-22");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            List<WeatherForecast> testGridItems = new()
            {
                new WeatherForecast("Japan", "Tokyo", 14)
            };
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlConnectionShim.When((shim) => shim.Open(Arg.Any<SqlConnection>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            Assert.That(e.Message, Does.StartWith($"Failed to connect to SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void PersistGrid_NewGridItemTemperatureLessThanAbsoluteZero()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-22");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-09-22 01:23:45.0000040");
            List<WeatherForecast> testGridItems = new()
            {
                new WeatherForecast("Japan", "Tokyo", -275),
                new WeatherForecast("Japan", "Kobe", 24)
            };
            List<WeatherForecastGridItemPTO> existingGridItems = new()
            {
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Japan", "Kobe", 22, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Japan", "Tokyo", 23, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
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
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Time"].Returns<Object>(testTime.ToString(transactSql24TimeStyle));
            mockDataReader["Country"].Returns<Object>(existingGridItems[0].Country);
            mockDataReader["City"].Returns<Object>(existingGridItems[0].City);
            mockDataReader["Temperature"].Returns<Object>(existingGridItems[0].Temperature);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-09-22T01:00:02.0000041");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

            Assert.That(e.Message, Does.StartWith($"Failed to persist grid to SQL Server."));
            Assert.That(e.InnerException.Message, Does.StartWith($"Failed to compare new weather forecast grid to existing grid in SQL Server for WeatherForecastGridOuterKeyProperties {{ Tag = 'Apple', Date = '2026-09-22', Time = '22:00:00' }}, and transaction time '2026-09-22T01:23:45.0000040'"));
            Assert.That(e.InnerException.InnerException is GridContentsValidationException<WeatherForecast>);
            GridContentsValidationException<WeatherForecast> innerInnerException = (GridContentsValidationException<WeatherForecast>)e.InnerException.InnerException;
            Assert.That(innerInnerException.Message, Does.StartWith($"Failed to validate item in grid."));
            Assert.That(innerInnerException.GridItem == testGridItems[0]);
            Assert.That(innerInnerException.InnerException.Message == $"WeatherForecast {{ Country = 'Japan', City = 'Tokyo', Temperature = -275 }} Temperature -275 cannot be less than -274.");
        }

        [Test]
        public void PersistGrid_DuplicateGridItems()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-22");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-09-22 01:23:45.0000040");
            List<WeatherForecast> testGridItems = new()
            {
                new WeatherForecast("Japan", "Tokyo", 23),
                new WeatherForecast("Japan", "Tokyo", 24)
            };
            List<WeatherForecastGridItemPTO> existingGridItems = new()
            {
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Japan", "Kobe", 22, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Japan", "Tokyo", 23, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
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
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Time"].Returns<Object>(testTime.ToString(transactSql24TimeStyle));
            mockDataReader["Country"].Returns<Object>(existingGridItems[0].Country, existingGridItems[1].Country);
            mockDataReader["City"].Returns<Object>(existingGridItems[0].City, existingGridItems[1].City);
            mockDataReader["Temperature"].Returns<Object>(existingGridItems[0].Temperature, existingGridItems[1].Temperature);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-09-22T01:00:02.0000041");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);
            });

            Assert.That(e.Message, Does.StartWith($"Failed to persist grid to SQL Server."));
            Assert.That(e.InnerException.Message, Does.StartWith($"Failed to compare new weather forecast grid to existing grid in SQL Server for WeatherForecastGridOuterKeyProperties {{ Tag = 'Apple', Date = '2026-09-22', Time = '22:00:00' }}, and transaction time '2026-09-22T01:23:45.0000040'"));
            Assert.That(e.InnerException.InnerException is GridContentsDuplicateItemsException<WeatherForecast>);
            GridContentsDuplicateItemsException<WeatherForecast> innerInnerException = (GridContentsDuplicateItemsException<WeatherForecast>)e.InnerException.InnerException;
            Assert.That(innerInnerException.Message, Does.StartWith($"Grid contains items with duplicate key values."));
            Assert.That(innerInnerException.GridItem == testGridItems[1]);
        }

        [Test]
        public void PersistGrid()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-22");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-09-22 01:23:45.0000040");
            List<WeatherForecast> testGridItems = new()
            {
                new WeatherForecast("Australia", "Melbourne", 13),
                new WeatherForecast("Australia", "Sydney", 25),
                new WeatherForecast("Japan", "Kobe", 17),
                new WeatherForecast("Japan", "Tokyo", 14)
            };
            List<WeatherForecastGridItemPTO> existingGridItems = new()
            {
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Australia", "Brisbane", 26, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Australia", "Cairns", 30, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Australia", "Melbourne", 25, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Australia", "Sydney", 29, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            String expectedGridInsertStatementText = @$"
                INSERT 
                INTO    WeatherForecastGrids 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            [Version], 
                            TransactionTimestamp
                        )
                VALUES  (
                            @Tag, 
                            CONVERT(date, @Date, 23), 
                            CONVERT(time, @Time, 24), 
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
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Time"].Returns<Object>(testTime.ToString(transactSql24TimeStyle));
            mockDataReader["Country"].Returns<Object>(existingGridItems[0].Country, existingGridItems[1].Country, existingGridItems[2].Country, existingGridItems[3].Country);
            mockDataReader["City"].Returns<Object>(existingGridItems[0].City, existingGridItems[1].City, existingGridItems[2].City, existingGridItems[3].City);
            mockDataReader["Temperature"].Returns<Object>(existingGridItems[0].Temperature, existingGridItems[1].Temperature, existingGridItems[2].Temperature, existingGridItems[3].Temperature);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-09-21T01:34:56.0000040");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);
            List<Int32> gridItemsStoredProcedureParameterRowCounts = new();
            List<Int32> gridItemIdsStoredProcedureParameterRowCounts = new();
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemIdsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testWeatherForecastBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems);

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
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
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

            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-22");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-09-22 01:23:45.0000040");
            List<WeatherForecast> testGridItems = new()
            {
                new WeatherForecast("Australia", "Melbourne", 13),
                new WeatherForecast("Australia", "Sydney", 25),
                new WeatherForecast("Japan", "Kobe", 17)
            };
            List<WeatherForecastGridItemPTO> existingGridItems = new()
            {
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Australia", "Brisbane", 26, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Australia", "Melbourne", 25, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Australia", "Sydney", 29, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            String expectedGridInsertStatementText = @$"
                INSERT 
                INTO    WeatherForecastGrids 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            [Version], 
                            TransactionTimestamp
                        )
                VALUES  (
                            @Tag, 
                            CONVERT(date, @Date, 23), 
                            CONVERT(time, @Time, 24), 
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
                true, true, true, false,
                // Call to get existing grid max id
                true, false
            );
            mockDataReader["Id"].Returns<Object>(existingGridItems[0].Id, existingGridItems[1].Id);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Time"].Returns<Object>(testTime.ToString(transactSql24TimeStyle));
            mockDataReader["Country"].Returns<Object>(existingGridItems[0].Country, existingGridItems[1].Country, existingGridItems[2].Country);
            mockDataReader["City"].Returns<Object>(existingGridItems[0].City, existingGridItems[1].City, existingGridItems[2].City);
            mockDataReader["Temperature"].Returns<Object>(existingGridItems[0].Temperature, existingGridItems[1].Temperature, existingGridItems[2].Temperature);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-09-21T01:34:56.0000040");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);
            List<Int32> gridItemsStoredProcedureParameterRowCounts = new();
            List<Int32> gridItemIdsStoredProcedureParameterRowCounts = new();
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemIdsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testWeatherForecastBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems, PersistComparisonStrategy.Syncronise);

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
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridInsertStatementText);
            mockSqlTransactionShim.Received(1).Commit(null);
            mockSqlConnectionShim.Received(2).Close(Arg.Any<SqlConnection>());
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
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-22");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime transactionTimeStamp = utils.CreateDataTimeFromString("2026-09-22 01:23:45.0000040");
            List<WeatherForecast> testGridItems = new()
            {
                new WeatherForecast("Australia", "Melbourne", 13),
                new WeatherForecast("Australia", "Sydney", 25),
                new WeatherForecast("Japan", "Kobe", 17),
                new WeatherForecast("Japan", "Tokyo", 14)
            };
            List<WeatherForecastGridItemPTO> existingGridItems = new()
            {
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Australia", "Brisbane", 26, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Australia", "Cairns", 30, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Australia", "Melbourne", 25, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Australia", "Sydney", 29, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedMaxVersionQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            String expectedGridInsertStatementText = @$"
                INSERT 
                INTO    WeatherForecastGrids 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            [Version], 
                            TransactionTimestamp
                        )
                VALUES  (
                            @Tag, 
                            CONVERT(date, @Date, 23), 
                            CONVERT(time, @Time, 24), 
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
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Time"].Returns<Object>(testTime.ToString(transactSql24TimeStyle));
            mockDataReader["Country"].Returns<Object>(existingGridItems[0].Country, existingGridItems[1].Country, existingGridItems[2].Country, existingGridItems[3].Country);
            mockDataReader["City"].Returns<Object>(existingGridItems[0].City, existingGridItems[1].City, existingGridItems[2].City, existingGridItems[3].City);
            mockDataReader["Temperature"].Returns<Object>(existingGridItems[0].Temperature, existingGridItems[1].Temperature, existingGridItems[2].Temperature, existingGridItems[3].Temperature);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-09-21T01:34:56.0000040");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");
            mockDataReader["MaxVersion"].Returns<Object>(1);
            List<Int32> gridItemsStoredProcedureParameterRowCounts = new();
            List<Int32> gridItemIdsStoredProcedureParameterRowCounts = new();
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItems", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));
            mockSqlCommandShim.AddParameter(Arg.Any<SqlCommand>(), "@GridItemIds", SqlDbType.Structured, Arg.Do<DataTable>(dataTable => gridItemIdsStoredProcedureParameterRowCounts.Add(dataTable.Rows.Count)));

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testWeatherForecastBulkPersister.PersistGrid(testOuterKeyProperties, testGridItems, PersistComparisonStrategy.AddAndUpdateOnly);

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
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(3).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, transactionTimeStamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridInsertStatementText);
            mockSqlTransactionShim.Received(1).Commit(null);
            mockSqlConnectionShim.Received(2).Close(Arg.Any<SqlConnection>());
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
    }
}
