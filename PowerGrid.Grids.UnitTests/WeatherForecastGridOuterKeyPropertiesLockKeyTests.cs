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
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using NUnit.Framework;

namespace PowerGrid.Grids.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Core.WeatherForecastGridOuterKeyPropertiesLockKey class.
    /// </summary>
    [TestFixture]
    public class WeatherForecastGridOuterKeyPropertiesLockKeyTests
    {
        private const String testTag = "Official";
        private DateOnly testDate;
        private TimeOnly testTime;

        private TestUtilities utils;
        private WeatherForecastGridOuterKeyPropertiesLockKey testWeatherForecastGridOuterKeyPropertiesLockKey;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            testDate = utils.CreateDateOnlyFromString("2026-07-12");
            testTime = utils.CreateTimeOnlyFromString("21:00:00");
            testWeatherForecastGridOuterKeyPropertiesLockKey = new(new WeatherForecastGridOuterKeyProperties(testTag, testDate, testTime));
        }

        [Test]
        public void KeyPropertyValues()
        {
            Object[] keyPropertyValues = testWeatherForecastGridOuterKeyPropertiesLockKey.KeyPropertyValues;

            Assert.That(keyPropertyValues.Length == 4);
            Assert.That(keyPropertyValues[0].GetType().IsAssignableTo(typeof(Type)));
            Assert.That((Type)keyPropertyValues[0] == typeof(WeatherForecastGridOuterKeyProperties));
            Assert.That(keyPropertyValues[1].GetType() == typeof(String));
            Assert.That((String)keyPropertyValues[1] == testTag);
            Assert.That(keyPropertyValues[2].GetType() == typeof(DateOnly));
            Assert.That((DateOnly)keyPropertyValues[2] == testDate);
            Assert.That(keyPropertyValues[3].GetType() == typeof(TimeOnly));
            Assert.That((TimeOnly)keyPropertyValues[3] == testTime);
        }
    }
}
