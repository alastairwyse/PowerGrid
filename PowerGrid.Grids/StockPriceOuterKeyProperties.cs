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
using System.Text;
using PowerGrid.Core;

namespace PowerGrid.Grids
{
    /// <summary>
    /// Defines the <see cref="IGridItemOuterKeyProperties">outer key properties</see> for stock prices.
    /// </summary>
    public record StockPriceOuterKeyProperties : KeyPropertiesBase, IStockPriceOuterKeyProperties
    {
        /// <summary>A tag used to classify the grid.</summary>
        public String Tag { get; init; }

        /// <summary>The source/entity which provided the price.</summary>
        public String DataSource { get; init; }

        /// <summary>The date the price was quoted for.</summary>
        public DateOnly Date { get; init; }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Grids.StockPriceOuterKeyProperties class.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="dataSource">The source/entity which provided the price.</param>
        /// <param name="date">The date the price was quoted for.</param>
        public StockPriceOuterKeyProperties(String tag, String dataSource, DateOnly date)
        {
            ThrowExceptionIfStringParameterNullOrWhitespace(nameof(tag), tag);
            ThrowExceptionIfStringParameterNullOrWhitespace(nameof(dataSource), dataSource);

            Tag = tag;
            DataSource = dataSource;
            Date = date;
        }

        #region Private/Protected Methods

        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append($"{nameof(Tag)} = '{Tag}', {nameof(DataSource)} = '{DataSource}', {nameof(Date)} = '{Date.ToString("yyyy-MM-dd")}'");

            return true;
        }

        #endregion
    }
}
