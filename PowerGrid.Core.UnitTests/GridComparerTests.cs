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

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerGrid.Core.UnitTests
{
    /// <summary>
    /// Unit tests for the PowerGrid.Core.GridComparer class.
    /// </summary>
    public class GridComparerTests
    {
        private const String reutersDataSource = "Reuters";
        private const String bloombergDataSource = "Bloomberg";
        private const String refinitivDataSource = "Refinitiv";
        private const String sonyCompany = "Sony";
        private const String canonCompany = "Canon";
        private const String toyotaCompany = "Toyota";



        private List<StockPrice> existingGridContents;
        private List<StockPrice> addedItems;
        private List<StockPrice> updatedItems;
        private List<StockPrice> deletedItems;
        private GridComparer<StockPrice> testGridComparer;

        [SetUp]
        protected void SetUp()
        {
            existingGridContents = new List<StockPrice>()
            {
                new StockPrice(reutersDataSource, CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3209),
                new StockPrice(reutersDataSource, CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(reutersDataSource, CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 7203),
            };

            addedItems = new List<StockPrice>();
            updatedItems = new List<StockPrice>();
            deletedItems = new List<StockPrice>();
            testGridComparer = new GridComparer<StockPrice>(new ListEmitter<StockPrice>(addedItems), new ListEmitter<StockPrice>(updatedItems), new ListEmitter<StockPrice>(deletedItems));
        }

        [Test]
        public void Compare()
        {

        }

        #region Private/Protected Methods

        /// <summary>
        /// Creates a DateOnly from the specified yyyy-MM-dd format string.
        /// </summary>
        /// <param name="stringifiedDateOnly">The stringified date to convert.</param>
        /// <returns></returns>
        protected DateOnly CreateDateOnlyFromString(String stringifiedDateOnly)
        {
            return DateOnly.ParseExact(stringifiedDateOnly, "yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);
        }

        #endregion

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
                throw new NotImplementedException();
            }
        }

        #pragma warning restore 1591

        #endregion
    }
}
