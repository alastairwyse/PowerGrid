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
    /// Unit tests for the PowerGrid.Grids.StockPrice class.
    /// </summary>
    public class StockPriceTests
    {
        [Test]
        public void Constructor_CompanyParameterNull()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                StockPrice testStockPrice = new(null, 123.45m);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'company' must contain a value."));
            Assert.That(e.ParamName == "company");
        }

        [Test]
        public void Constructor_CompanyParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                StockPrice testStockPrice = new(" ", 123.45m);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'company' must contain a value."));
            Assert.That(e.ParamName == "company");
        }
    }
}
