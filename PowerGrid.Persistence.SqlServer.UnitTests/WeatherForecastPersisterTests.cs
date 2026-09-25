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
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using ApplicationLogging;
using ApplicationMetrics;
using NUnit.Framework;
using NUnit.Framework.Internal;
using NSubstitute;

namespace PowerGrid.Persistence.SqlServer.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Persistence.SqlServer.WeatherForecastPersister class.
    /// </summary>
    public class WeatherForecastPersisterTests : PersisterTestsBase
    {
        private WeatherForecastPersisterWithProtectedMembers testWeatherForecastPersister;

        [SetUp]
        protected override void SetUp()
        {
            base.SetUp();
            testWeatherForecastPersister = new WeatherForecastPersisterWithProtectedMembers(testConnectionString, 5, 10, 0, mockLogger, mockMetricLogger, mockDateTimeProvider, mockSqlConnectionShim, mockSqlTransactionShim, mockSqlCommandShim);
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
            String expectedReadExistingGridCommandText = @$"
                SELECT  Id, 
                        Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        Country, 
                        City, 
                        Temperature, 
                        CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                        CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER   BY Country, 
                           City;";
            String expectedMaxIdQueryText = @$"
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
                testWeatherForecastPersister.PersistGrid(testOuterKeyProperties, testGridItems);
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
            String expectedReadExistingGridCommandText = @$"
                SELECT  Id, 
                        Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        Country, 
                        City, 
                        Temperature, 
                        CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                        CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER   BY Country, 
                           City;";
            String expectedMaxIdQueryText = @$"
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
                testWeatherForecastPersister.PersistGrid(testOuterKeyProperties, testGridItems);
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
                new WeatherForecast("Japan", "Kobe", 24), 
                new WeatherForecast("Japan", "Tokyo", 23)
            };
            List<WeatherForecastGridItemPTO> existingGridItems = new()
            {
                new WeatherForecastGridItemPTO(1L, testTag, testDate, testTime, "Japan", "Himeji", 20, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999")),
                new WeatherForecastGridItemPTO(2L, testTag, testDate, testTime, "Japan", "Tokyo", 21, utils.CreateDataTimeFromString("2026-09-22 01:00:02.0000041"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"))
            };
            String expectedReadExistingGridCommandText = @$"
                SELECT  Id, 
                        Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        Country, 
                        City, 
                        Temperature, 
                        CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                        CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER   BY Country, 
                           City;";
            String expectedMaxIdQueryText = @$"
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

            (Int32 resultVersion, GridComparisonStatistics resultStatistics) = testWeatherForecastPersister.PersistGrid(testOuterKeyProperties, testGridItems);

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
            mockSqlCommandShim.Received(5).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(5).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
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
            const String testTag = "www.weatheronline.co.uk";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-17");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("11:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);

            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                new List<WeatherForecastGridItemPTO>(testWeatherForecastPersister.GetGrid(testOuterKeyProperties, 0));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'version' with value 0 must be greater than 0."));
            Assert.That(e.ParamName == "version");
        }

        [Test]
        public void GetGrid_ExceptionConnectingToSqlServer()
        {
            const String testTag = "www.weatheronline.co.uk";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-17");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("11:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            var mockException = new Exception("Mock exception");
            mockSqlConnectionShim.When((shim) => shim.Open(Arg.Any<SqlConnection>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                List<WeatherForecastGridItemPTO> results = new(testWeatherForecastPersister.GetGrid(testOuterKeyProperties, 1));
            });

            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            Assert.That(e.Message, Does.StartWith($"Failed to connect to SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGrid()
        {
            const String testTag = "www.weatheronline.co.uk";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-17");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("11:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            Int32 testVersion = 15;
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-09-17 23:54:31.0000205");
            String expectedVersionQueryCommandText = @$"
                SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = @Version;";
            String expectedGridQueryCommandText = @$"
                SELECT  Id, 
                        Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        Country, 
                        City, 
                        Temperature, 
                        CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                        CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER   BY Country, 
                           City;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false, true, false);
            // Mock returns for grid version query
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-09-17T23:54:31.0000205");
            // Mock returns for grid contents query
            mockDataReader["Id"].Returns<Object>(1L);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Time"].Returns<Object>(testTime.ToString(transactSql24TimeStyle));
            mockDataReader["Country"].Returns<Object>("United Kingdom");
            mockDataReader["City"].Returns<Object>("London");
            mockDataReader["Temperature"].Returns<Object>(11);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-09-17T23:54:31.0000205");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");

            List<WeatherForecastGridItemPTO> results = new(testWeatherForecastPersister.GetGrid(testOuterKeyProperties, testVersion));

            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedVersionQueryCommandText);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedGridQueryCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, testTransactionTimestamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(2).ExecuteReader(Arg.Any<SqlCommand>());
            Assert.That(results.Count == 1);
            Assert.That(results[0].Id == 1);
            Assert.That(results[0].Tag == testTag);
            Assert.That(results[0].Date == testDate);
            Assert.That(results[0].Time == testTime);
            Assert.That(results[0].Country == "United Kingdom");
            Assert.That(results[0].City == "London");
            Assert.That(results[0].Temperature == 11);
            Assert.That(results[0].TransactionFrom == testTransactionTimestamp);
            Assert.That(results[0].TransactionFrom.Kind == DateTimeKind.Utc);
            Assert.That(results[0].TransactionTo == utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            Assert.That(results[0].TransactionTo.Kind == DateTimeKind.Utc);
        }

        [Test]
        public void GetGridDetailsGridOuterKeyPropertiesOverload_ExceptionReading()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-16");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("10:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastPersister.GetGridDetails(testOuterKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            Assert.That(e.Message, Does.StartWith($"Failed to read grid details for WeatherForecastGridOuterKeyProperties {{ Tag = 'Apple', Date = '2026-09-16', Time = '10:00:00' }} from SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGridDetailsGridOuterKeyPropertiesOverload()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-16");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("11:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
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
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-09-16T13:02:53.1837676", "2026-09-16T13:02:03.9134273");

            IList<GridVersionAndTransactionTimestamp> result = testWeatherForecastPersister.GetGridDetails(testOuterKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
            mockDataReader.Received(3).Read();
            mockSqlConnectionShim.Received(1).Close(Arg.Any<SqlConnection>());
            Assert.That(result.Count == 2);
            Assert.That(result[0].Version == 1);
            Assert.That(result[0].TransactionTimestamp == utils.CreateDataTimeFromString("2026-09-16 13:02:53.1837676"));
            Assert.That(result[1].Version == 2);
            Assert.That(result[1].TransactionTimestamp == utils.CreateDataTimeFromString("2026-09-16 13:02:03.9134273"));
        }

        [Test]
        public void GetGridDetailsGridCommonKeyPropertiesOverload_ExceptionReading()
        {
            const String testTag = "Apple";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastPersister.GetGridDetails(testCommonKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Received(1).Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            Assert.That(e.Message, Does.StartWith($"Failed to read grid details for GridCommonKeyProperties {{ Tag = 'Apple' }} from SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void GetGridDetailsGridCommonKeyPropertiesOverload()
        {
            const String testTag = "www.bom.gov.au";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedCommandText = @$"
                SELECT  Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp, 126) AS TransactionTimestamp 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns
            (
                true, true, true, false
            );
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["Date"].Returns<Object>("2026-05-30", "2026-05-30", "2026-05-31");
            mockDataReader["Time"].Returns<Object>("09:00:00", "09:00:00", "10:00:00");
            mockDataReader["Version"].Returns<Object>(1, 2, 1);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-30T13:02:53.1837676", "2026-06-09T13:02:03.9134273", "2026-06-23T21:55:56.9750913");

            IList<Tuple<WeatherForecastGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> result = testWeatherForecastPersister.GetGridDetails(testCommonKeyProperties);

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
            Assert.That(result[0].Item1.Date == utils.CreateDateOnlyFromString("2026-05-30"));
            Assert.That(result[0].Item1.Time == utils.CreateTimeOnlyFromString("09:00:00"));
            Assert.That(result[0].Item2.Version == 1);
            Assert.That(result[0].Item2.TransactionTimestamp == utils.CreateDataTimeFromString("2026-05-30 13:02:53.1837676"));
            Assert.That(result[1].Item1.Tag == testTag);
            Assert.That(result[1].Item1.Date == utils.CreateDateOnlyFromString("2026-05-30"));
            Assert.That(result[1].Item1.Time == utils.CreateTimeOnlyFromString("09:00:00"));
            Assert.That(result[1].Item2.Version == 2);
            Assert.That(result[1].Item2.TransactionTimestamp == utils.CreateDataTimeFromString("2026-06-09 13:02:03.9134273"));
            Assert.That(result[2].Item1.Tag == testTag);
            Assert.That(result[2].Item1.Date == utils.CreateDateOnlyFromString("2026-05-31"));
            Assert.That(result[2].Item1.Time == utils.CreateTimeOnlyFromString("10:00:00"));
            Assert.That(result[2].Item2.Version == 1);
            Assert.That(result[2].Item2.TransactionTimestamp == utils.CreateDataTimeFromString("2026-06-23 21:55:56.9750913"));
        }

        [Test]
        public void SoftDeleteLatestGrid_ExceptionConnectingToSqlServer()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-15");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("21:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlConnectionShim.When((shim) => shim.Open(Arg.Any<SqlConnection>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastPersister.SoftDeleteLatestGrid(testOuterKeyProperties);
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
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-15");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("21:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastPersister.SoftDeleteLatestGrid(testOuterKeyProperties);
            });

            Assert.That(e.Message, Does.StartWith($"Weather forecast grid for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-15', Time = '21:00:00' }} does not exist."));
        }

        [Test]
        public void SoftDeleteLatestGrid_ExceptionDeleting()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-15");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("21:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime testDeleteTimestamp = utils.CreateDataTimeFromString("2026-09-15 20:49:13.0000033");
            String expectedDeleteCommandText = @$"
                UPDATE  WeatherForecasts 
                SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126) 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @CurrentDateTime, 126) BETWEEN TransactionFrom AND TransactionTo;";
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
                testWeatherForecastPersister.SoftDeleteLatestGrid(testOuterKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CurrentDateTime", SqlDbType.NVarChar, testDeleteTimestamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, testDeleteTimestamp.AddTicks(-1).ToString(transactSql126DateStyle));
            Assert.That(e.Message, Does.StartWith($"Failed to delete latest grid items for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-15', Time = '21:00:00' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void SoftDeleteLatestGrid()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-15");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("21:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime testDeleteTimestamp = utils.CreateDataTimeFromString("2026-09-15 20:49:13.0000035");
            String expectedDeleteCommandText = @$"
                UPDATE  WeatherForecasts 
                SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126) 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @CurrentDateTime, 126) BETWEEN TransactionFrom AND TransactionTo;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-09-15T21:52:07.0000036");
            mockDateTimeProvider.UtcNow().Returns<DateTime>(testDeleteTimestamp);

            testWeatherForecastPersister.SoftDeleteLatestGrid(testOuterKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CurrentDateTime", SqlDbType.NVarChar, testDeleteTimestamp.ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, testDeleteTimestamp.AddTicks(-1).ToString(transactSql126DateStyle));
            mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlTransactionShim.Received(1).Commit(Arg.Any<SqlTransaction>());
            mockSqlConnectionShim.Close(Arg.Any<SqlConnection>());
        }

        [Test]
        public void HardDeleteGridsOuterKeyPropertiesOverload_ExceptionDeleting()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-13");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("20:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedWeatherForecastGridsDeleteCommandText = @$"
                DELETE 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastPersister.HardDeleteGrids(testOuterKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedWeatherForecastGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            Assert.That(e.Message, Does.StartWith($"Failed to delete weather forecast grids for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-13', Time = '20:00:00' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void HardDeleteGridsGridOuterKeyPropertiesOverload()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-13");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("20:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedWeatherForecastGridsDeleteCommandText = @$"
                DELETE 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            String expectedWeatherForecastsDeleteCommandText = @$"
                DELETE 
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));

            testWeatherForecastPersister.HardDeleteGrids(testOuterKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedWeatherForecastGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedWeatherForecastsDeleteCommandText);
            mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
            mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
            mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
            mockSqlTransactionShim.Received(1).Commit(Arg.Any<SqlTransaction>());
            mockSqlConnectionShim.Close(Arg.Any<SqlConnection>());
        }

        [Test]
        public void HardDeleteGridsCommonKeyPropertiesOverload_ExceptionDeleting()
        {
            const String testTag = "Apple";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedWeatherForecastGridsDeleteCommandText = @$"
                DELETE 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteNonQuery(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            var e = Assert.Throws<Exception>(delegate
            {
                testWeatherForecastPersister.HardDeleteGrids(testCommonKeyProperties);
            });

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedWeatherForecastGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
            mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), Arg.Any<SqlTransaction>());
            mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
            mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            Assert.That(e.Message, Does.StartWith($"Failed to delete weather forecast grids for GridCommonKeyProperties {{ Tag = 'Apple' }} in SQL Server."));
            Assert.That(e.InnerException == mockException);
        }

        [Test]
        public void HardDeleteGridsCommonKeyPropertiesOverload()
        {
            const String testTag = "Calibration";
            GridCommonKeyProperties testCommonKeyProperties = new(testTag);
            String expectedWeatherForecastGridsDeleteCommandText = @$"
                DELETE 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag;";
            String expectedWeatherForecastsDeleteCommandText = @$"
                DELETE 
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag;";
            SqlRetryLogicOption sqlRetryLogicOption = new();
            sqlRetryLogicOption.NumberOfTries = 1;
            mockSqlConnectionShim.GetRetryLogicProvider(Arg.Any<SqlConnection>()).Returns<SqlRetryLogicBaseProvider>(SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption));

            testWeatherForecastPersister.HardDeleteGrids(testCommonKeyProperties);

            mockSqlConnectionShim.Received(1).SetRetryLogicProvider(Arg.Any<SqlConnection>(), Arg.Any<SqlRetryLogicBaseProvider>());
            mockSqlConnectionShim.Received(1).GetRetryLogicProvider(Arg.Any<SqlConnection>());
            mockSqlConnectionShim.Open(Arg.Any<SqlConnection>());
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedWeatherForecastGridsDeleteCommandText);
            mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedWeatherForecastsDeleteCommandText);
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
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-12");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("09:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedCommandText = @$"
                SELECT  [Version] AS [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24)
                        );";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.GetLatestGridVersion(connection, testOuterKeyProperties);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to read latest weather forecast grid version for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-12', Time = '09:00:00' }} from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetLatestGridVersion_NoVersionExists()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-12");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("09:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedCommandText = @$"
                SELECT  [Version] AS [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24)
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testWeatherForecastPersister.GetLatestGridVersion(connection, testOuterKeyProperties);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                Assert.That(versionNumberResult == 0);
                Assert.That(transactionTimestampResult == DateTime.MinValue);
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetLatestGridVersion_MultipleRecordsReturned()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-12");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("09:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedCommandText = @$"
                SELECT  [Version] AS [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24)
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, true);
            mockDataReader["Version"].Returns<Object>(3);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-05-16T13:39:41.0000013");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.GetLatestGridVersion(connection, testOuterKeyProperties);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                Assert.That(e.Message, Does.StartWith($"Failed to read latest weather forecast grid version for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-12', Time = '09:00:00' }} from SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Read multiple results from SQL Server when attempting to retrieve latest weather forecast grid version for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-12', Time = '09:00:00' }}."));
            }
        }

        [Test]
        public void GetLatestGridVersion()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-12");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("09:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedCommandText = @$"
                SELECT  [Version] AS [Version], 
                        CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = 
                        (
                          
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24)
                        );";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Version"].Returns<Object>(4);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-09-12T09:49:52.0000060");

            using (var connection = new SqlConnection(testConnectionString))
            {
                (Int32 versionNumberResult, DateTime transactionTimestampResult) = testWeatherForecastPersister.GetLatestGridVersion(connection, testOuterKeyProperties);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                Assert.That(versionNumberResult == 4);
                Assert.That(transactionTimestampResult == utils.CreateDataTimeFromString("2026-09-12 09:49:52.0000060"));
                Assert.That(transactionTimestampResult.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_MultipleRecordsReturned()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-10");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            Int32 testVersion = 9;
            String expectedCommandText = @$"
                SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = @Version;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-09-09T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                Assert.That(e.Message, Does.StartWith($"Failed to read weather forecast grid for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-10', Time = '23:00:00' }}, and version 9 from SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Read multiple results from SQL Server when attempting to retrieve weather forecast grid version for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-10', Time = '23:00:00' }}, and version 9."));
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_GridDoesntExist()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-10");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            Int32 testVersion = 10;
            String expectedCommandText = @$"
                SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = @Version;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);
            mockDataReader["TransactionTimestamp"].Returns<Object>("2026-09-09T23:02:45.0000101");

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                Assert.That(e.Message, Does.StartWith($"Weather forecast grid for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-10', Time = '23:00:00' }}, and version 10 did not exist."));
            }
        }

        [Test]
        public void GetGridTransactionTimestamp_ExceptionReading()
        {
            const String testTag = "www.bom.gov.au";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-10");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            Int32 testVersion = 11;
            String expectedCommandText = @$"
                SELECT  CONVERT(nvarchar(30), TransactionTimestamp , 126) AS TransactionTimestamp
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   [Version] = @Version;";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.GetGridTransactionTimestamp(connection, testOuterKeyProperties, testVersion);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, testVersion);
                Assert.That(e.Message, Does.StartWith($"Failed to read weather forecast grid for WeatherForecastGridOuterKeyProperties {{ Tag = 'www.bom.gov.au', Date = '2026-09-10', Time = '23:00:00' }}, and version 11 from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetExistingGrid_ExceptionReading()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-10");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-09-10 22:04:51.0000031");
            String expectedCommandText = @$"
                SELECT  Id, 
                        Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        Country, 
                        City, 
                        Temperature, 
                        CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                        CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER   BY Country, 
                           City;";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    List<WeatherForecastGridItemPTO> results = new(testWeatherForecastPersister.GetGrid(connection, testOuterKeyProperties, testTransactionTimestamp));
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to read weather forecast grid for WeatherForecastGridOuterKeyProperties {{ Tag = 'Apple', Date = '2026-09-10', Time = '23:00:00' }}, and transaction timestamp '2026-09-10 22:04:51.0000031' from SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void GetGridTransactionTimestampOverload()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-08");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            DateTime testTransactionTimestamp = utils.CreateDataTimeFromString("2026-09-08 22:04:51.0000030");
            String expectedCommandText = @$"
                SELECT  Id, 
                        Tag, 
                        CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                        CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                        Country, 
                        City, 
                        Temperature, 
                        CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                        CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM    WeatherForecasts 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24) 
                  AND   CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER   BY Country, 
                           City;";
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["Id"].Returns<Object>(1L);
            mockDataReader["Tag"].Returns<Object>(testTag);
            mockDataReader["Date"].Returns<Object>(testDate.ToString(transactSql23DateStyle));
            mockDataReader["Time"].Returns<Object>(testTime.ToString(transactSql24TimeStyle));
            mockDataReader["Country"].Returns<Object>("Japan");
            mockDataReader["City"].Returns<Object>("Tokyo");
            mockDataReader["Temperature"].Returns<Object>(27);
            mockDataReader["TransactionFrom"].Returns<Object>("2026-09-08T22:04:51.0000030");
            mockDataReader["TransactionTo"].Returns<Object>("9999-12-31T23:59:59.9999999");

            using (var connection = new SqlConnection(testConnectionString))
            {
                List<WeatherForecastGridItemPTO> results = new(testWeatherForecastPersister.GetGrid(connection, testOuterKeyProperties, testTransactionTimestamp));

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TransactionTimestamp", SqlDbType.NVarChar, testTransactionTimestamp.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                Assert.That(results.Count == 1);
                Assert.That(results[0].Id == 1);
                Assert.That(results[0].Tag == testTag);
                Assert.That(results[0].Date == testDate);
                Assert.That(results[0].Time == testTime);
                Assert.That(results[0].Country == "Japan");
                Assert.That(results[0].City == "Tokyo");
                Assert.That(results[0].Temperature == 27);
                Assert.That(results[0].TransactionFrom == testTransactionTimestamp);
                Assert.That(results[0].TransactionFrom.Kind == DateTimeKind.Utc);
                Assert.That(results[0].TransactionTo == utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
                Assert.That(results[0].TransactionTo.Kind == DateTimeKind.Utc);
            }
        }

        [Test]
        public void InsertGridItem_ExceptionInserting()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-07");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            const String testCountry = "Japan";
            const String testCity = "Osaka";
            WeatherForecastGridItem testItem = new(testTag, testDate, testTime, testCountry, testCity, 27);
            DateTime testInsertDateTime = utils.CreateDataTimeFromString("2026-09-07 22:45:08.0000021");
            String expectedCommandText = @$"
                INSERT 
                INTO    WeatherForecasts 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            Country, 
                            City, 
                            Temperature, 
                            TransactionFrom, 
                            TransactionTo 
                        )
                VALUES  (
                            @Tag, 
                            CONVERT(date, @Date, 23), 
                            CONVERT(time, @Time, 24), 
                            @Country, 
                            @City, 
                            @Temperature, 
                            CONVERT(datetime2, @InsertDateTime, 126), 
                            CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                        );";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.InsertGridItem(connection, null, testItem, testInsertDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to insert WeatherForecastGridItem {{ Tag = 'Apple', Date = '2026-09-07', Time = '23:00:00', Country = 'Japan', City = 'Osaka', Temperature = 27 }} into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void InsertGridItem()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-07");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            const String testCountry = "Japan";
            const String testCity = "Osaka";
            WeatherForecastGridItem testItem = new(testTag, testDate, testTime, testCountry, testCity, 27);
            DateTime testInsertDateTime = utils.CreateDataTimeFromString("2026-09-07 22:30:12.0000020");
            String expectedCommandText = @$"
                INSERT 
                INTO    WeatherForecasts 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            Country, 
                            City, 
                            Temperature, 
                            TransactionFrom, 
                            TransactionTo 
                        )
                VALUES  (
                            @Tag, 
                            CONVERT(date, @Date, 23), 
                            CONVERT(time, @Time, 24), 
                            @Country, 
                            @City, 
                            @Temperature, 
                            CONVERT(datetime2, @InsertDateTime, 126), 
                            CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                        );";

            using (var connection = new SqlConnection(testConnectionString))
            {
                testWeatherForecastPersister.InsertGridItem(connection, null, testItem, testInsertDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Country", SqlDbType.NVarChar, testCountry);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@City", SqlDbType.NVarChar, testCity);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Temperature", SqlDbType.Int, testItem.Temperature);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@InsertDateTime", SqlDbType.NVarChar, testInsertDateTime.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TemporalMaximumDateTime", SqlDbType.NVarChar, DateTime.MaxValue.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        [Test]
        public void UpdateGridItem_ExceptionUpdating()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-06");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("10:00:00");
            const String testCountry = "Australia";
            const String testCity = "Sydney";
            WeatherForecastGridItem testNewItem = new(testTag, testDate, testTime, testCountry, testCity, 22);
            WeatherForecastGridItemPTO testSupersededItem = new(123, testTag, testDate, testTime, testCountry, testCity, 24, utils.CreateDataTimeFromString("2026-09-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testUpdateDateTime = utils.CreateDataTimeFromString("2026-09-06 09:09:42.0000012");
            String expectedInsertCommandText = @$"
                INSERT 
                INTO    WeatherForecasts 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            Country, 
                            City, 
                            Temperature, 
                            TransactionFrom, 
                            TransactionTo 
                        )
                VALUES  (
                            @Tag, 
                            CONVERT(date, @Date, 23), 
                            CONVERT(time, @Time, 24), 
                            @Country, 
                            @City, 
                            @Temperature, 
                            CONVERT(datetime2, @InsertDateTime, 126), 
                            CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                        );";
            String expectedDeleteCommandText = @$"
            UPDATE  WeatherForecasts 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.UpdateGridItem(connection, null, testSupersededItem, testNewItem, testUpdateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to update weather forecast with id '{testSupersededItem.Id}' in SQL Server."));
                Assert.That(e.InnerException.Message, Does.StartWith($"Failed to delete weather forecast with id '{testSupersededItem.Id}' in SQL Server."));
                Assert.That(e.InnerException.InnerException == mockException);
            }
        }

        [Test]
        public void UpdateGridItem()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-06");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("10:00:00");
            const String testCountry = "Australia";
            const String testCity = "Sydney";
            WeatherForecastGridItem testNewItem = new(testTag, testDate, testTime, testCountry, testCity, 22);
            WeatherForecastGridItemPTO testSupersededItem = new(123, testTag, testDate, testTime, testCountry, testCity, 24, utils.CreateDataTimeFromString("2026-09-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testUpdateDateTime = utils.CreateDataTimeFromString("2026-09-06 09:44:19.0000013");
            String expectedInsertCommandText = @$"
                INSERT 
                INTO    WeatherForecasts 
                        (
                            Tag, 
                            [Date], 
                            [Time], 
                            Country, 
                            City, 
                            Temperature, 
                            TransactionFrom, 
                            TransactionTo 
                        )
                VALUES  (
                            @Tag, 
                            CONVERT(date, @Date, 23), 
                            CONVERT(time, @Time, 24), 
                            @Country, 
                            @City, 
                            @Temperature, 
                            CONVERT(datetime2, @InsertDateTime, 126), 
                            CONVERT(datetime2, @TemporalMaximumDateTime, 126)
                        );";
            String expectedDeleteCommandText = @$"
            UPDATE  WeatherForecasts 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;";

            using (var connection = new SqlConnection(testConnectionString))
            {
                testWeatherForecastPersister.UpdateGridItem(connection, null, testSupersededItem, testNewItem, testUpdateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedDeleteCommandText);
                mockSqlCommandShim.Received(2).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Id", SqlDbType.BigInt, testSupersededItem.Id);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-09-06 09:44:19.0000012").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(2).ExecuteNonQuery(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertCommandText);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Country", SqlDbType.NVarChar, testCountry);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@City", SqlDbType.NVarChar, testCity);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Temperature", SqlDbType.Int, testNewItem.Temperature);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@InsertDateTime", SqlDbType.NVarChar, testUpdateDateTime.ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@TemporalMaximumDateTime", SqlDbType.NVarChar, DateTime.MaxValue.ToString(transactSql126DateStyle));
            }
        }

        [Test]
        public void DeleteGridItem_ExceptionDeleting()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-03");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            const String testCountry = "Japan";
            const String testCity = "Tokyo";
            WeatherForecastGridItemPTO testItem = new(123, testTag, testDate, testTime, testCountry, testCity, 24, utils.CreateDataTimeFromString("2026-09-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testDeleteDateTime = utils.CreateDataTimeFromString("2026-09-03 22:56:57.0000008");
            String expectedCommandText = @$"
            UPDATE  WeatherForecasts 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText)).Do((callInfo) => throw mockException);

            using (var connection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.DeleteGridItem(connection, null, testItem, testDeleteDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                Assert.That(e.Message, Does.StartWith($"Failed to delete weather forecast with id '{testItem.Id}' in SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void DeleteGridItem()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-03");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("23:00:00");
            const String testCountry = "Japan";
            const String testCity = "Tokyo";
            WeatherForecastGridItemPTO testItem = new(123, testTag, testDate, testTime, testCountry, testCity, 24, utils.CreateDataTimeFromString("2026-09-01 09:05:08.0000007"), utils.CreateDataTimeFromString("9999-12-31 23:59:59.9999999"));
            DateTime testDeleteDateTime = utils.CreateDataTimeFromString("2026-09-03 22:56:57.0000009");
            String expectedCommandText = @$"
            UPDATE  WeatherForecasts 
            SET     TransactionTo = CONVERT(datetime2, @DeleteDateTime, 126)
            WHERE   Id = @Id;";

            using (var connection = new SqlConnection(testConnectionString))
            {
                testWeatherForecastPersister.DeleteGridItem(connection, null, testItem, testDeleteDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedCommandText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), connection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Id", SqlDbType.BigInt, testItem.Id);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@DeleteDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-09-03 22:56:57.0000008").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
            }
        }

        [Test]
        public void CreateGrid_ExceptionRetrievingLatestGridVersion()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-02");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-09-02 22:27:28.0000021");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedMaxIdQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            var mockException = new Exception("Mock exception");
            mockSqlCommandShim.When((shim) => shim.ExecuteReader(Arg.Any<SqlCommand>())).Do((callInfo) => throw mockException);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                var e = Assert.Throws<Exception>(delegate
                {
                    testWeatherForecastPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                Assert.That(e.Message, Does.StartWith($"Failed to retrieve latest grid version number while inserting weather forecast grid for WeatherForecastGridOuterKeyProperties {{ Tag = 'Apple', Date = '2026-09-02', Time = '22:00:00' }} into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_ExceptionInserting()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-02");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-09-02 22:27:28.0000021");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedMaxIdQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            String expectedInsertStatementText = @$"
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
                    testWeatherForecastPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);
                });

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-09-02 22:27:28.0000021").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(e.Message, Does.StartWith($"Failed to insert weather forecast grid for WeatherForecastGridOuterKeyProperties {{ Tag = 'Apple', Date = '2026-09-02', Time = '22:00:00' }} and version 2 into SQL Server."));
                Assert.That(e.InnerException == mockException);
            }
        }

        [Test]
        public void CreateGrid_GridAlreadyExists()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-02");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-09-02 22:27:28.0000021");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedMaxIdQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            String expectedInsertStatementText = @$"
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
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(true, false);
            mockDataReader["MaxVersion"].Returns<Object>(1);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                Int32 result = testWeatherForecastPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).ExecuteReader(Arg.Any<SqlCommand>());
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 2);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-09-02 22:27:28.0000021").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(result == 2);
            }
        }

        [Test]
        public void CreateGrid_NoGridExists()
        {
            const String testTag = "Apple";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-09-02");
            TimeOnly testTime = utils.CreateTimeOnlyFromString("22:00:00");
            DateTime testCreateDateTime = utils.CreateDataTimeFromString("2026-09-02 22:27:28.0000021");
            WeatherForecastGridOuterKeyProperties testOuterKeyProperties = new(testTag, testDate, testTime);
            String expectedMaxIdQueryText = @$"
                SELECT  MAX([Version]) AS MaxVersion 
                FROM    WeatherForecastGrids 
                WHERE   Tag = @Tag 
                  AND   [Date] = CONVERT(date, @Date, 23) 
                  AND   [Time] = CONVERT(time, @Time, 24);";
            String expectedInsertStatementText = @$"
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
            IDataReader mockDataReader = Substitute.For<IDataReader>();
            mockSqlCommandShim.ExecuteReader(Arg.Any<SqlCommand>()).Returns(mockDataReader);
            mockDataReader.Read().Returns(false);

            using (var readConnection = new SqlConnection(testConnectionString))
            using (var writeConnection = new SqlConnection(testConnectionString))
            {
                Int32 result = testWeatherForecastPersister.CreateGrid(readConnection, writeConnection, null, testOuterKeyProperties, testCreateDateTime);

                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedMaxIdQueryText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), readConnection);
                mockSqlCommandShim.Received(1).SetTransaction(Arg.Any<SqlCommand>(), null);
                mockSqlCommandShim.Received(2).SetCommandTimeout(Arg.Any<SqlCommand>(), 0);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Tag", SqlDbType.NVarChar, testTag);
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Date", SqlDbType.NVarChar, testDate.ToString(transactSql23DateStyle));
                mockSqlCommandShim.Received(2).AddParameter(Arg.Any<SqlCommand>(), "@Time", SqlDbType.NVarChar, testTime.ToString(transactSql24TimeStyle));
                mockSqlCommandShim.Received(1).SetCommandText(Arg.Any<SqlCommand>(), expectedInsertStatementText);
                mockSqlCommandShim.Received(1).SetConnection(Arg.Any<SqlCommand>(), writeConnection);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@Version", SqlDbType.Int, 1);
                mockSqlCommandShim.Received(1).AddParameter(Arg.Any<SqlCommand>(), "@CreateDateTime", SqlDbType.NVarChar, utils.CreateDataTimeFromString("2026-09-02 22:27:28.0000021").ToString(transactSql126DateStyle));
                mockSqlCommandShim.Received(1).ExecuteNonQuery(Arg.Any<SqlCommand>());
                Assert.That(result == 1);
            }
        }

        #region Nested Classes

        #pragma warning disable 1591

        /// <summary>
        /// Version of the WeatherForecastPersister class where private and protected methods are exposed as public so that they can be unit tested.
        /// </summary>
        protected class WeatherForecastPersisterWithProtectedMembers : WeatherForecastPersister
        {
            public WeatherForecastPersisterWithProtectedMembers
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

            public new (Int32 Version, DateTime TransactionTimestamp) GetLatestGridVersion(SqlConnection connection, WeatherForecastGridOuterKeyProperties outerKeyProperties)
            {
                return base.GetLatestGridVersion(connection, outerKeyProperties);
            }

            public new DateTime GetGridTransactionTimestamp(SqlConnection connection, WeatherForecastGridOuterKeyProperties outerKeyProperties, Int32 version)
            {
                return base.GetGridTransactionTimestamp(connection, outerKeyProperties, version);
            }

            public new IEnumerable<WeatherForecastGridItemPTO> GetGrid(SqlConnection connection, WeatherForecastGridOuterKeyProperties outerKeyProperties, DateTime transactionTimestamp)
            {
                return base.GetGrid(connection, outerKeyProperties, transactionTimestamp);
            }

            public new void InsertGridItem(SqlConnection connection, SqlTransaction transaction, WeatherForecastGridItem item, DateTime insertDateTime)
            {
                base.InsertGridItem(connection, transaction, item, insertDateTime);
            }

            public new void UpdateGridItem(SqlConnection connection, SqlTransaction transaction, WeatherForecastGridItemPTO supersededItem, WeatherForecastGridItem newItem, DateTime udpateDateTime)
            {
                base.UpdateGridItem(connection, transaction, supersededItem, newItem, udpateDateTime);
            }

            public new void DeleteGridItem(SqlConnection connection, SqlTransaction transaction, WeatherForecastGridItemPTO item, DateTime deleteDateTime)
            {
                base.DeleteGridItem(connection, transaction, item, deleteDateTime);
            }

            public new Int32 CreateGrid(SqlConnection readConnection, SqlConnection writeConnection, SqlTransaction transaction, WeatherForecastGridOuterKeyProperties outerKeyProperties, DateTime createDateTime)
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
