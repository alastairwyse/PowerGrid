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
using PowerGrid.Core;
using PowerGrid.Grids;
using NUnit.Framework;

namespace PowerGrid.Core.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Core.GridContentsDuplicateChecker class.
    /// </summary>
    [TestFixture]
    public class GridContentsDuplicateCheckerTests
    {
        private const String bloombergDataSource = "Bloomberg";
        private const String canonCompany = "Canon";
        private const String hitachiCompany = "Hitachi";
        private const String sonyCompany = "Sony";
        private const String toyotaCompany = "Toyota";

        private TestUtilities utils;
        private GridContentsDuplicateChecker<StockPrice> testGridContentsDuplicateChecker;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            testGridContentsDuplicateChecker = new GridContentsDuplicateChecker<StockPrice>();
        }

        [Test]
        public void CheckForDuplicates()
        {
            List<StockPrice> gridContents = new()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4441),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4733),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3210),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 3256)
            };

            List<StockPrice> result = new(testGridContentsDuplicateChecker.CheckForDuplicates(gridContents));
        }

        [Test]
        public void CheckForDuplicates_DuplicatesExist()
        {
            List<StockPrice> gridContents = new()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4441),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4733),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3210),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 3256)
            };

            var e = Assert.Throws<GridContentsDuplicateItemsException<StockPrice>>(delegate
            {
                List<StockPrice> result = new(testGridContentsDuplicateChecker.CheckForDuplicates(gridContents));
            });

            Assert.That(e.Message, Does.StartWith("Grid contains items with duplicate key values."));
            Assert.That(e.GridItem == gridContents[1]);


            gridContents = new()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4441),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4733),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 3210),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 3256)
            };

            e = Assert.Throws<GridContentsDuplicateItemsException<StockPrice>>(delegate
            {
                List<StockPrice> result = new(testGridContentsDuplicateChecker.CheckForDuplicates(gridContents));
            });

            Assert.That(e.Message, Does.StartWith("Grid contains items with duplicate key values."));
            Assert.That(e.GridItem == gridContents[2]);


            gridContents = new()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4441),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4733),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 3210),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 3256)
            };

            e = Assert.Throws<GridContentsDuplicateItemsException<StockPrice>>(delegate
            {
                List<StockPrice> result = new(testGridContentsDuplicateChecker.CheckForDuplicates(gridContents));
            });

            Assert.That(e.Message, Does.StartWith("Grid contains items with duplicate key values."));
            Assert.That(e.GridItem == gridContents[3]);
        }
    }
}
