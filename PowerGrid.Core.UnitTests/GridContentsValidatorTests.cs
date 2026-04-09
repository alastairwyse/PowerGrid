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
using System.Collections;
using System.Collections.Generic;
using PowerGrid.Core;
using PowerGrid.Grids;
using NUnit.Framework;

namespace PowerGrid.Core.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Core.GridContentsValidator class.
    /// </summary>
    [TestFixture]
    public class GridContentsValidatorTests
    {
        private const String bloombergDataSource = "Bloomberg";
        private const String canonCompany = "Canon";
        private const String hitachiCompany = "Hitachi";
        private const String sonyCompany = "Sony";
        private const String toyotaCompany = "Toyota";

        private TestUtilities utils;
        private Action<StockPrice> stockPriceValidationAction;
        private GridContentsValidator<StockPrice> testGridContentsValidator;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            stockPriceValidationAction = (StockPrice inputStockPrice) =>
            {
                if (inputStockPrice.Price < 0)
                    throw new Exception($"Stock price for datasource '{inputStockPrice.DataSource}', date '{inputStockPrice.Date.ToString("yyyy-MM-dd")}', and company '{inputStockPrice.Company}' has negative price {inputStockPrice.Price}.");
            };
            testGridContentsValidator = new GridContentsValidator<StockPrice>();
        }

        [Test]
        public void ValidateItems()
        {
            List<StockPrice> gridContents = new()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4733),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            List<StockPrice> result = new(testGridContentsValidator.ValidateItems(gridContents, stockPriceValidationAction));
        }

        [Test]
        public void ValidateItems_InvalidItemsExist()
        {
            List<StockPrice> gridContents = new()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, -1),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            var e = Assert.Throws<GridContentsValidationException<StockPrice>>(delegate
            {
                List<StockPrice> result = new(testGridContentsValidator.ValidateItems(gridContents, stockPriceValidationAction));
            });

            Assert.That(e.Message, Does.StartWith("Failed to validate item in grid."));
            Assert.That(e.InnerException.Message, Does.StartWith("Stock price for datasource 'Bloomberg', date '2026-03-23', and company 'Hitachi' has negative price -1."));
            Assert.That(e.GridItem == gridContents[1]);
        }
    }
}
