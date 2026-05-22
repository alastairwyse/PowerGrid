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
using PowerGrid.Core;
using PowerGrid.Grids;
using NUnit.Framework;

namespace PowerGrid.Core.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Core.GridLockKeyBase class.
    /// </summary>
    /// <remarks>Tests are performed on derived class PowerGrid.Grids.StockPriceGridItemGridLockKey since GridLockKeyBase is abstract.</remarks>
    [TestFixture]
    public class GridLockKeyBaseTests
    {
        private const String marketTag = "Market";
        private const String calibratedTag = "Calibrated";
        private const String bloombergDataSource = "Bloomberg";
        private const String refinitivDataSource = "Refinitiv";
        private const String canonCompany = "Canon";
        private const String sonyCompany = "Sony";

        private TestUtilities utils;
        StockPriceGridItem testStockPrice;
        private StockPriceGridItemGridLockKey testStockPriceGridItemGridLockKey;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            testStockPrice = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            testStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(testStockPrice);
        }

        [Test]
        public void KeyPropertyValues()
        {
            Object[] result = testStockPriceGridItemGridLockKey.KeyPropertyValues;

            Assert.That(result.Length == 4);
            Assert.That((Type)result[0] == typeof(StockPriceGridItem));
            Assert.That(result[1].GetType() == typeof(String));
            Assert.That((String)result[1] == marketTag);
            Assert.That(result[2].GetType() == typeof(String));
            Assert.That((String)result[2] == bloombergDataSource);
            Assert.That(result[3].GetType() == typeof(DateOnly));
            Assert.That(result[3].Equals(utils.CreateDateOnlyFromString("2026-04-15")));
        }

        [Test]
        public void Equals()
        {
            StockPriceGridItem otherStockPriceGridItem = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            StockPriceGridItemGridLockKey otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPriceGridItem);

            Boolean result = testStockPriceGridItemGridLockKey.Equals(otherStockPriceGridItemGridLockKey);

            Assert.That(result == true);


            otherStockPriceGridItem = new(calibratedTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPriceGridItem);

            result = testStockPriceGridItemGridLockKey.Equals(otherStockPriceGridItemGridLockKey);

            Assert.That(result == false);


            otherStockPriceGridItem = new(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPriceGridItem);

            result = testStockPriceGridItemGridLockKey.Equals(otherStockPriceGridItemGridLockKey);

            Assert.That(result == false);


            otherStockPriceGridItem = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-16"), canonCompany, 4431);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPriceGridItem);

            result = testStockPriceGridItemGridLockKey.Equals(otherStockPriceGridItemGridLockKey);

            Assert.That(result == false);


            otherStockPriceGridItem = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-16"), sonyCompany, 4431);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPriceGridItem);

            result = testStockPriceGridItemGridLockKey.Equals(otherStockPriceGridItemGridLockKey);

            Assert.That(result == false);
        }

        [Test]
        public new void GetHashCode()
        {
            StockPriceGridItem otherStockPrice = new(calibratedTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            StockPriceGridItemGridLockKey otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPrice);

            Int32 result1 = testStockPriceGridItemGridLockKey.GetHashCode();
            Int32 result2 = otherStockPriceGridItemGridLockKey.GetHashCode();

            Assert.That(result1 != result2);


            otherStockPrice = new(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPrice);

            result1 = testStockPriceGridItemGridLockKey.GetHashCode();
            result2 = otherStockPriceGridItemGridLockKey.GetHashCode();

            Assert.That(result1 != result2);


            otherStockPrice = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-14"), canonCompany, 4431);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPrice);

            result1 = testStockPriceGridItemGridLockKey.GetHashCode();
            result2 = otherStockPriceGridItemGridLockKey.GetHashCode();

            Assert.That(result1 != result2);


            otherStockPrice = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), sonyCompany, 4432);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPrice);

            result1 = testStockPriceGridItemGridLockKey.GetHashCode();
            result2 = otherStockPriceGridItemGridLockKey.GetHashCode();

            Assert.That(result1 == result2);


            otherStockPrice = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4432);
            otherStockPriceGridItemGridLockKey = new StockPriceGridItemGridLockKey(otherStockPrice);

            result1 = testStockPriceGridItemGridLockKey.GetHashCode();
            result2 = otherStockPriceGridItemGridLockKey.GetHashCode();

            Assert.That(result1 == result2);
        }
    }
}
