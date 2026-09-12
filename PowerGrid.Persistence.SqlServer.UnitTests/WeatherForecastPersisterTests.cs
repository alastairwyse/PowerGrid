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
using PowerGrid.Grids;
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
                SELECT Id, 
                       Tag, 
                       CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                       CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                       Country, 
                       City, 
                       Temperature, 
                       CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                       CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM   WeatherForecasts 
                WHERE  Tag = @Tag 
                  AND  [Date] = CONVERT(date, @Date, 23) 
                  AND  [Time] = CONVERT(time, @Time, 24) 
                  AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER  BY Country, 
                          City 
                COLLATE Latin1_General_BIN2;";
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
                SELECT Id, 
                       Tag, 
                       CONVERT(nvarchar(30), [Date], 23) AS [Date], 
                       CONVERT(nvarchar(30), [Time], 24) AS [Time], 
                       Country, 
                       City, 
                       Temperature, 
                       CONVERT(nvarchar(30), TransactionFrom, 126) AS TransactionFrom, 
                       CONVERT(nvarchar(30), TransactionTo, 126) AS TransactionTo
                FROM   WeatherForecasts 
                WHERE  Tag = @Tag 
                  AND  [Date] = CONVERT(date, @Date, 23) 
                  AND  [Time] = CONVERT(time, @Time, 24) 
                  AND  CONVERT(datetime2, @TransactionTimestamp, 126) BETWEEN TransactionFrom AND TransactionTo 
                ORDER  BY Country, 
                          City 
                COLLATE Latin1_General_BIN2;";
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
