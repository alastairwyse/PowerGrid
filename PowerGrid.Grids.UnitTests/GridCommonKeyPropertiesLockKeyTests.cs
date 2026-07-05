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
    /// Unit tests for the PowerGrid.Core.GridCommonKeyPropertiesLockKey class.
    /// </summary>
    [TestFixture]
    public class GridCommonKeyPropertiesLockKeyTests
    {
        private const String testTag = "Simulation";

        private GridCommonKeyPropertiesLockKey testGridCommonKeyPropertiesLockKey;

        [SetUp]
        protected void SetUp()
        {
            testGridCommonKeyPropertiesLockKey = new(new GridCommonKeyProperties(testTag));
        }

        [Test]
        public void KeyPropertyValues()
        {
            Object[] keyPropertyValues = testGridCommonKeyPropertiesLockKey.KeyPropertyValues;

            Assert.That(keyPropertyValues.Length == 2);
            Assert.That(keyPropertyValues[0].GetType() == typeof(Type));
            Assert.That((Type)keyPropertyValues[0] == typeof(GridCommonKeyProperties));
            Assert.That(keyPropertyValues[1].GetType() == typeof(String));
            Assert.That((String)keyPropertyValues[1] == testTag);
        }
    }
}
