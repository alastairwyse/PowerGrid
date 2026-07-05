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
    /// <remarks>Tests are performed on derived class PowerGrid.Grids.StockPriceGridOuterKeyPropertiesLockKey since GridLockKeyBase is abstract.</remarks>
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
        private StockPriceGridOuterKeyPropertiesLockKey testStockPriceGridOuterKeyPropertiesLockKey;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            testStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey(new StockPriceGridOuterKeyProperties(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15")));
        }

        [Test]
        public void KeyPropertyValues()
        {
            Object[] result = testStockPriceGridOuterKeyPropertiesLockKey.KeyPropertyValues;

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
            StockPriceGridOuterKeyPropertiesLockKey otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"))
            );

            Boolean result = testStockPriceGridOuterKeyPropertiesLockKey.Equals(otherStockPriceGridOuterKeyPropertiesLockKey);

            Assert.That(result == true);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(calibratedTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"))
            );

            result = testStockPriceGridOuterKeyPropertiesLockKey.Equals(otherStockPriceGridOuterKeyPropertiesLockKey);

            Assert.That(result == false);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-04-15"))
            );

            result = testStockPriceGridOuterKeyPropertiesLockKey.Equals(otherStockPriceGridOuterKeyPropertiesLockKey);

            Assert.That(result == false);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-16"))
            );

            result = testStockPriceGridOuterKeyPropertiesLockKey.Equals(otherStockPriceGridOuterKeyPropertiesLockKey);

            Assert.That(result == false);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-16"))
            );

            result = testStockPriceGridOuterKeyPropertiesLockKey.Equals(otherStockPriceGridOuterKeyPropertiesLockKey);

            Assert.That(result == false);
        }

        [Test]
        public new void GetHashCode()
        {
            StockPriceGridOuterKeyPropertiesLockKey otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(calibratedTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"))
            );

            Int32 result1 = testStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();
            Int32 result2 = otherStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();

            Assert.That(result1 != result2);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-04-15"))
            );

            result1 = testStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();
            result2 = otherStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();

            Assert.That(result1 != result2);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-14"))
            );

            result1 = testStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();
            result2 = otherStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();

            Assert.That(result1 != result2);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"))
            );

            result1 = testStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();
            result2 = otherStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();

            Assert.That(result1 == result2);


            otherStockPriceGridOuterKeyPropertiesLockKey = new StockPriceGridOuterKeyPropertiesLockKey
            (
                new StockPriceGridOuterKeyProperties(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"))
            );

            result1 = testStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();
            result2 = otherStockPriceGridOuterKeyPropertiesLockKey.GetHashCode();

            Assert.That(result1 == result2);
        }
    }
}
