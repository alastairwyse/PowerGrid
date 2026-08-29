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
    /// Defines the <see cref="IGridOuterKeyProperties">outer key properties</see> for grids of weather forecasts.
    /// </summary>
    public record WeatherForecastGridOuterKeyProperties : ModelBase, IWeatherForecastGridOuterKeyProperties
    {
        /// <inheritdoc/>
        public String Tag { get; init; }

        /// <inheritdoc/>
        public DateOnly Date { get; init; }

        /// <inheritdoc/>
        public TimeOnly Time { get; init; }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Grids.WeatherForecastGridOuterKeyProperties class.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="date">The date the weather was forecast for.</param>
        /// <param name="time">The time of day of the weather was forecast for.</param>
        public WeatherForecastGridOuterKeyProperties(String tag, DateOnly date, TimeOnly time)
        {
            ThrowExceptionIfStringParameterNullOrWhitespace(nameof(tag), tag);

            Tag = tag;
            Date = date;
            Time = time;
        }

        #region Private/Protected Methods

        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append($"{nameof(Tag)} = '{Tag}', {nameof(Date)} = '{Date.ToString("yyyy-MM-dd")}', {nameof(Time)} = '{Time.ToString("HH:mm:ss")}'");

            return true;
        }

        #endregion
    }
}
