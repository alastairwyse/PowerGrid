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
using PowerGrid.Core;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using NUnit.Framework;

namespace PowerGrid.Grids.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Grids.WeatherForecastGridItem class.
    /// </summary>
    [TestFixture]
    public class WeatherForecastGridItemTests
    {
        private const String officialTag = "Official";
        private const String preliminaryTag = "Preliminary";
        private const String australiaCountry = "Australia";
        private const String japanCountry = "Japan";
        private const String tokyoCity = "Tokyo";
        private const String osakaCity = "Osaka";

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
                WeatherForecastGridItem testWeatherForecastGridItem = new(null, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'tag' must contain a value."));
            Assert.That(e.ParamName == "tag");
        }

        [Test]
        public void Constructor_TagParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentNullException>(delegate
            {
                WeatherForecastGridItem testWeatherForecastGridItem = new("  ", utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'tag' must contain a value."));
            Assert.That(e.ParamName == "tag");
        }

        [Test]
        public void KeyCompareTo()
        {
            WeatherForecastGridItem weatherForecast1 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            WeatherForecastGridItem weatherForecast2 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);

            Assert.That(weatherForecast1.KeyCompareTo(weatherForecast2) == 0);


            weatherForecast1 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            weatherForecast2 = new(preliminaryTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity,  26);

            Assert.That(weatherForecast1.KeyCompareTo(weatherForecast2) == -1);
            Assert.That(weatherForecast2.KeyCompareTo(weatherForecast1) == 1);


            weatherForecast1 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            weatherForecast2 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-12"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);

            Assert.That(weatherForecast1.KeyCompareTo(weatherForecast2) == -1);
            Assert.That(weatherForecast2.KeyCompareTo(weatherForecast1) == 1);


            weatherForecast1 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            weatherForecast2 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("22:00:00"), japanCountry, tokyoCity, 26);

            Assert.That(weatherForecast1.KeyCompareTo(weatherForecast2) == -1);
            Assert.That(weatherForecast2.KeyCompareTo(weatherForecast1) == 1);


            weatherForecast1 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), australiaCountry, tokyoCity, 26);
            weatherForecast2 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);

            Assert.That(weatherForecast1.KeyCompareTo(weatherForecast2) == -1);
            Assert.That(weatherForecast2.KeyCompareTo(weatherForecast1) == 1);


            weatherForecast1 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, osakaCity, 26);
            weatherForecast2 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);

            Assert.That(weatherForecast1.KeyCompareTo(weatherForecast2) == -1);
            Assert.That(weatherForecast2.KeyCompareTo(weatherForecast1) == 1);
        }

        [Test]
        public void ValuePropertiesEqual()
        {
            WeatherForecastGridItem weatherForecast1 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            WeatherForecastGridItem weatherForecast2 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);
            WeatherForecastGridItem weatherForecast3 = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 27);

            Assert.That(weatherForecast1.ValuePropertiesEqual(weatherForecast2) == true);
            Assert.That(weatherForecast1.ValuePropertiesEqual(weatherForecast3) == false);
        }

        [Test]
        public void PrintMembers()
        {
            WeatherForecastGridItem testWeatherForecastGridItem = new(officialTag, utils.CreateDateOnlyFromString("2026-07-11"), utils.CreateTimeOnlyFromString("21:00:00"), japanCountry, tokyoCity, 26);

            String result = testWeatherForecastGridItem.ToString();

            Assert.That(result == "WeatherForecastGridItem { Tag = 'Official', Date = '2026-07-11', Time = '21:00:00', Country = 'Japan', City = 'Tokyo', Temperature = 26 }");
        }
    }
}
