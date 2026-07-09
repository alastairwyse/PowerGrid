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
    /// Unit tests for the PowerGrid.Grids.WeatherForecast class.
    /// </summary>
    public class WeatherForecastTests
    {
        private TestUtilities utils;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
        }

        [Test]
        public void Constructor_CountryParameterNull()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                WeatherForecast testWeatherForecast = new(null, "Tokyo", TimeOnly.FromDateTime(DateTime.UtcNow), 25);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'country' must contain a value."));
            Assert.That(e.ParamName == "country");
        }

        [Test]
        public void Constructor_CountryParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                WeatherForecast testWeatherForecast = new(" ", "Tokyo", TimeOnly.FromDateTime(DateTime.UtcNow), 25);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'country' must contain a value."));
            Assert.That(e.ParamName == "country");
        }

        [Test]
        public void Constructor_CityParameterNull()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                WeatherForecast testWeatherForecast = new("Japan", null, TimeOnly.FromDateTime(DateTime.UtcNow), 25);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'city' must contain a value."));
            Assert.That(e.ParamName == "city");
        }

        [Test]
        public void Constructor_CityParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                WeatherForecast testWeatherForecast = new("Japan", "", TimeOnly.FromDateTime(DateTime.UtcNow), 25);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'city' must contain a value."));
            Assert.That(e.ParamName == "city");
        }

        [Test]
        public void KeyCompareTo()
        {
            WeatherForecast testWeatherForecast1 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);
            WeatherForecast testWeatherForecast2 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);

            Assert.That(testWeatherForecast1.KeyCompareTo(testWeatherForecast2) == 0);


            testWeatherForecast1 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:39.0000000")), 25);
            testWeatherForecast2 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);

            Assert.That(testWeatherForecast1.KeyCompareTo(testWeatherForecast2) == -1);
            Assert.That(testWeatherForecast2.KeyCompareTo(testWeatherForecast1) == 1);


            testWeatherForecast1 = new("Japan", "Osaka", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);
            testWeatherForecast2 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);

            Assert.That(testWeatherForecast1.KeyCompareTo(testWeatherForecast2) == -1);
            Assert.That(testWeatherForecast2.KeyCompareTo(testWeatherForecast1) == 1);


            testWeatherForecast1 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);
            testWeatherForecast2 = new("Nihon", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);

            Assert.That(testWeatherForecast1.KeyCompareTo(testWeatherForecast2) == -1);
            Assert.That(testWeatherForecast2.KeyCompareTo(testWeatherForecast1) == 1);
        }

        [Test]
        public void ValuePropertiesEqual()
        {
            WeatherForecast testWeatherForecast1 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);
            WeatherForecast testWeatherForecast2 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 25);
            WeatherForecast testWeatherForecast3 = new("Japan", "Tokyo", TimeOnly.FromDateTime(utils.CreateDataTimeFromString("2026-07-09 22:39:40.0000000")), 26);

            Assert.That(testWeatherForecast1.ValuePropertiesEqual(testWeatherForecast2) == true);
            Assert.That(testWeatherForecast1.ValuePropertiesEqual(testWeatherForecast3) == false);
        }
    }
}
