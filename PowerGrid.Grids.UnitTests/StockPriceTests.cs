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
using System.Globalization;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using NUnit.Framework;

namespace PowerGrid.Core.UnitTests
{
    /// <summary>
    /// Tests implementations of the <see cref="IKeyPropertyComparable{T}"/> interface via the <see cref="StockPrice"/> class.
    /// </summary>
    [TestFixture]
    public class StockPriceTests
    {
        private const String bloombergDataSource = "Bloomberg";
        private const String refinitivDataSource = "Refinitiv";
        private const String canonCompany = "Canon";
        private const String sonyCompany = "Sony";
        private TestUtilities utils;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
        }

        [Test]
        public void KeyCompareTo()
        {
            StockPrice stockPrice1 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-03"), canonCompany, 4440);
            StockPrice stockPrice2 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-03"), canonCompany, 4441);

            Assert.That(stockPrice1.KeyCompareTo(stockPrice2) == 0);


            stockPrice1 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-03"), canonCompany, 4440);
            stockPrice2 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-03"), sonyCompany, 4441);

            Assert.That(stockPrice1.KeyCompareTo(stockPrice2) == -1);
            Assert.That(stockPrice2.KeyCompareTo(stockPrice1) == 1);


            stockPrice1 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-03"), canonCompany, 4440);
            stockPrice2 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-04"), canonCompany, 4441);

            Assert.That(stockPrice1.KeyCompareTo(stockPrice2) == -1);
            Assert.That(stockPrice2.KeyCompareTo(stockPrice1) == 1);


            stockPrice1 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-04-03"), canonCompany, 4440);
            stockPrice2 = new(refinitivDataSource, utils.CreateDateOnlyFromString("2026-04-03"), canonCompany, 4441);

            Assert.That(stockPrice1.KeyCompareTo(stockPrice2) == -1);
            Assert.That(stockPrice2.KeyCompareTo(stockPrice1) == 1);
        }
    }
}
