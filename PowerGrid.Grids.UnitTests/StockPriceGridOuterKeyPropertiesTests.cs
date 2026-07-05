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
using PowerGrid.Core.UnitTests;
using NUnit.Framework;

namespace PowerGrid.Grids.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Grids.StockPriceGridOuterKeyProperties class.
    /// </summary>
    public class StockPriceGridOuterKeyPropertiesTests
    {
        private TestUtilities utils;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
        }

        [Test]
        public void Constructor_TagParameterNull()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                StockPriceGridOuterKeyProperties testStockPriceGridOuterKeyProperties = new(null, "Bloomberg", DateOnly.FromDateTime(DateTime.UtcNow));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'tag' must contain a value."));
            Assert.That(e.ParamName == "tag");
        }

        [Test]
        public void Constructor_TagParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                StockPriceGridOuterKeyProperties testStockPriceGridOuterKeyProperties = new(" ", "Bloomberg", DateOnly.FromDateTime(DateTime.UtcNow));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'tag' must contain a value."));
            Assert.That(e.ParamName == "tag");
        }

        [Test]
        public void Constructor_DataSourceParameterNull()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                StockPriceGridOuterKeyProperties testStockPriceGridOuterKeyProperties = new("Market", null, DateOnly.FromDateTime(DateTime.UtcNow));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
            Assert.That(e.ParamName == "dataSource");
        }

        [Test]
        public void Constructor_DataSourceParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                StockPriceGridOuterKeyProperties testStockPriceGridOuterKeyProperties = new("Market", " ", DateOnly.FromDateTime(DateTime.UtcNow));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'dataSource' must contain a value."));
            Assert.That(e.ParamName == "dataSource");
        }

        [Test]
        public void PrintMembers()
        {
            const String testTag = "Market";
            const String testDataSource = "Bloomberg";
            DateOnly testDate = utils.CreateDateOnlyFromString("2026-05-30");
            StockPriceGridOuterKeyProperties testStockPriceGridOuterKeyProperties = new(testTag, testDataSource, testDate);

            String result = testStockPriceGridOuterKeyProperties.ToString();

            Assert.That(result == "StockPriceOuterKeyProperties { Tag = 'Market', DataSource = 'Bloomberg', Date = '2026-05-30' }");
        }
    }
}
