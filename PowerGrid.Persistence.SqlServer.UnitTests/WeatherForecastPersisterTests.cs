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
using NSubstitute;
using NUnit.Framework;

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
            throw new NotImplementedException();
        }

        [Test]
        public void CreateGrid_NoGridExists()
        {
            throw new NotImplementedException();
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
                //return base.GetLatestGridVersion(connection, outerKeyProperties);
                throw new NotImplementedException();
            }

            public new DateTime GetGridTransactionTimestamp(SqlConnection connection, WeatherForecastGridOuterKeyProperties outerKeyProperties, Int32 version)
            {
                //return base.GetGridTransactionTimestamp(connection, outerKeyProperties, version);
                throw new NotImplementedException();
            }

            public new IEnumerable<StockPriceGridItemPTO> GetGrid(SqlConnection connection, WeatherForecastGridOuterKeyProperties outerKeyProperties, DateTime transactionTimestamp)
            {
                //return base.GetGrid(connection, outerKeyProperties, transactionTimestamp);
                throw new NotImplementedException();
            }

            public new void InsertGridItem(SqlConnection connection, SqlTransaction transaction, WeatherForecastGridItem item, DateTime insertDateTime)
            {
                //base.InsertGridItem(connection, transaction, item, insertDateTime);
            }

            public new void UpdateGridItem(SqlConnection connection, SqlTransaction transaction, WeatherForecastGridItemPTO supersededItem, WeatherForecastGridItem newItem, DateTime udpateDateTime)
            {
                //base.UpdateGridItem(connection, transaction, supersededItem, newItem, udpateDateTime);
            }

            public new void DeleteGridItem(SqlConnection connection, SqlTransaction transaction, WeatherForecastGridItemPTO item, DateTime deleteDateTime)
            {
                //base.DeleteGridItem(connection, transaction, item, deleteDateTime);
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
