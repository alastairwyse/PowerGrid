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
    /// Unit tests for the PowerGrid.Grids.WeatherForecastGridOuterKeyProperties class.
    /// </summary>
    public class WeatherForecastGridOuterKeyPropertiesTests
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
                WeatherForecastGridOuterKeyProperties testWeatherForecastGridOuterKeyProperties = new(null, utils.CreateDateOnlyFromString("2026-07-12"));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'tag' must contain a value."));
            Assert.That(e.ParamName == "tag");
        }

        [Test]
        public void Constructor_TagParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                WeatherForecastGridOuterKeyProperties testWeatherForecastGridOuterKeyProperties = new(" ", utils.CreateDateOnlyFromString("2026-07-12"));
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'tag' must contain a value."));
            Assert.That(e.ParamName == "tag");
        }

        [Test]
        public void PrintMembers()
        {
            WeatherForecastGridOuterKeyProperties testWeatherForecastGridOuterKeyProperties = new("Official", utils.CreateDateOnlyFromString("2026-07-12"));

            String result = testWeatherForecastGridOuterKeyProperties.ToString();

            Assert.That(result == "WeatherForecastGridOuterKeyProperties { Tag = 'Official', Date = '2026-07-12' }");
        }
    }
}
