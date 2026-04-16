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
    /// <remarks>Tests are performed on derived class PowerGrid.Grids.StockPriceGridLockKey since GridLockKeyBase is abstract.</remarks>
    [TestFixture]
    public class GridLockKeyBaseTests
    {
        private const String bloombergDataSource = "Bloomberg";
        private const String refinitivDataSource = "Refinitiv";
        private const String canonCompany = "Canon";
        private const String sonyCompany = "Sony";

        private TestUtilities utils;
        StockPrice testStockPrice;
        private StockPriceGridLockKey testStockPriceGridLockKey;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            testStockPrice = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            testStockPriceGridLockKey = new StockPriceGridLockKey(testStockPrice);
        }

        [Test]
        public void KeyPropertyValues()
        {
            Object[] result = testStockPriceGridLockKey.KeyPropertyValues;

            Assert.That(result.Length == 3);
            Assert.That((Type)result[0] == typeof(StockPrice));
            Assert.That(result[1].GetType() == typeof(String));
            Assert.That((String)result[1] == bloombergDataSource);
            Assert.That(result[2].GetType() == typeof(DateOnly));
            Assert.That(result[2].Equals(utils.CreateDateOnlyFromString("2026-04-15")));
        }

        [Test]
        public void Equals()
        {
            StockPrice otherStockPrice = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            StockPriceGridLockKey otherStockPriceGridLockKey = new StockPriceGridLockKey(otherStockPrice);

            Boolean result = testStockPriceGridLockKey.Equals(otherStockPriceGridLockKey);

            Assert.That(result == true);


            otherStockPrice = new(refinitivDataSource, utils.CreateDateOnlyFromString("2026-04-15"), canonCompany, 4431);
            otherStockPriceGridLockKey = new StockPriceGridLockKey(otherStockPrice);

            result = testStockPriceGridLockKey.Equals(otherStockPriceGridLockKey);

            Assert.That(result == false);


            otherStockPrice = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-16"), sonyCompany, 4431);
            otherStockPriceGridLockKey = new StockPriceGridLockKey(otherStockPrice);

            result = testStockPriceGridLockKey.Equals(otherStockPriceGridLockKey);

            Assert.That(result == false);
        }

        [Test]
        public void GetHashCode()
        {
            StockPrice otherStockPrice = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-14"), canonCompany, 4431);
            StockPriceGridLockKey otherStockPriceGridLockKey = new StockPriceGridLockKey(otherStockPrice);

            Int32 result1 = testStockPriceGridLockKey.GetHashCode();
            Int32 result2 = otherStockPriceGridLockKey.GetHashCode();

            Assert.That(result1 != result2);


            otherStockPrice = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-15"), sonyCompany, 4432);
            otherStockPriceGridLockKey = new StockPriceGridLockKey(otherStockPrice);

            result1 = testStockPriceGridLockKey.GetHashCode();
            result2 = otherStockPriceGridLockKey.GetHashCode();

            Assert.That(result1 == result2);
        }
    }
}
