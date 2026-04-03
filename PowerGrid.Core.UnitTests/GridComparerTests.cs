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
using System.Globalization;
using NUnit.Framework;

namespace PowerGrid.Core.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Core.GridComparer class.
    /// </summary>
    [TestFixture]
    public class GridComparerTests
    {
        private const String bloombergDataSource = "Bloomberg";
        private const String canonCompany = "Canon";
        private const String hitachiCompany = "Hitachi";
        private const String sonyCompany = "Sony";
        private const String toyotaCompany = "Toyota";

        private TestUtilities utils;
        private List<StockPrice> existingGridContents;
        private List<StockPrice> newGridContents;
        private List<StockPrice> addedItems;
        private List<StockPrice> updatedItems;
        private List<StockPrice> deletedItems;
        private GridComparer<StockPrice> testGridComparer;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            addedItems = new List<StockPrice>();
            updatedItems = new List<StockPrice>();
            deletedItems = new List<StockPrice>();
            testGridComparer = new GridComparer<StockPrice>(new ListEmitter<StockPrice>(addedItems), new ListEmitter<StockPrice>(updatedItems), new ListEmitter<StockPrice>(deletedItems));
        }

        [Test]
        public void Compare_ItemUpdatedAtStartOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4441),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 0);
            Assert.That(updatedItems.Count == 1);
            Assert.That(deletedItems.Count == 0);
            Assert.That(updatedItems[0] == newGridContents[0]);
        }

        [Test]
        public void Compare_ItemUpdatedInMiddleOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4733),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 0);
            Assert.That(updatedItems.Count == 1);
            Assert.That(deletedItems.Count == 0);
            Assert.That(updatedItems[0] == newGridContents[1]);
        }

        [Test]
        public void Compare_ItemUpdatedAtEndOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3208)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 0);
            Assert.That(updatedItems.Count == 1);
            Assert.That(deletedItems.Count == 0);
            Assert.That(updatedItems[0] == newGridContents[2]);
        }

        [Test]
        public void Compare_ItemAddedAtStartOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 1);
            Assert.That(updatedItems.Count == 0);
            Assert.That(deletedItems.Count == 0);
            Assert.That(addedItems[0] == newGridContents[0]);
        }

        [Test]
        public void Compare_ItemAddedInMiddleOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 1);
            Assert.That(updatedItems.Count == 0);
            Assert.That(deletedItems.Count == 0);
            Assert.That(addedItems[0] == newGridContents[1]);
        }

        [Test]
        public void Compare_ItemAddedAtEndOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 1);
            Assert.That(updatedItems.Count == 0);
            Assert.That(deletedItems.Count == 0);
            Assert.That(addedItems[0] == newGridContents[2]);
        }

        [Test]
        public void Compare_ItemDeletedAtStartOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 0);
            Assert.That(updatedItems.Count == 0);
            Assert.That(deletedItems.Count == 1);
            Assert.That(deletedItems[0] == existingGridContents[0]);
        }

        [Test]
        public void Compare_ItemDeletedInMiddleOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 0);
            Assert.That(updatedItems.Count == 0);
            Assert.That(deletedItems.Count == 1);
            Assert.That(deletedItems[0] == existingGridContents[1]);
        }

        [Test]
        public void Compare_ItemDeletedAtEndOfExistingGrid()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209)
            };
            newGridContents = new List<StockPrice>()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732)
            };

            testGridComparer.Compare(existingGridContents, newGridContents);

            Assert.That(addedItems.Count == 0);
            Assert.That(updatedItems.Count == 0);
            Assert.That(deletedItems.Count == 1);
            Assert.That(deletedItems[0] == existingGridContents[2]);
        }

        // TODO: Realistic test with large grid

        #region Nested Classes

        #pragma warning disable 1591

        /// <summary>
        /// Implementation of <see cref="IEmitter{T}"/> which emits/outputs objects to a <see cref="List{T}"/>
        /// </summary>
        /// <typeparam name="T">The type of items held in the list.</typeparam>
        private class ListEmitter<T> : IEmitter<T>
        {
            protected List<T> list;

            public ListEmitter(List<T> list)
            {
                this.list = list;
            }

            /// <inheritdoc/>
            public void Emit(T instance)
            {
                list.Add(instance);
            }
        }

        #pragma warning restore 1591

        #endregion
    }
}
