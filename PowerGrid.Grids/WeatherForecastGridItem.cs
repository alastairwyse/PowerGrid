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
    public record WeatherForecastGridItem : WeatherForecast, IStockPriceGridOuterKeyProperties, IGridItem<WeatherForecastGridItem>
    {
        /// <summary>A tag used to classify the grid.</summary>
        public String Tag { get; init; }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Grids.WeatherForecastGridItem class.
        /// </summary>
        /// <param name="company">The country of the city the weather was forecast for.</param>
        /// <param name="city">The city the weather was forecast for.</param>
        /// <param name="time">The time of day of the weather forecast.</param>
        /// <param name="temperature">The forecast temperature.</param>
        public WeatherForecastGridItem(String tag, String country, String city, TimeOnly time, Int32 temperature)
            : base(country, city, time, temperature)
        {
            ThrowExceptionIfStringParameterNullOrWhitespace(nameof(tag), tag);

            Tag = tag;
        }

        /// <inheritdoc/>
        public Int32 KeyCompareTo(WeatherForecastGridItem other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Boolean ValuePropertiesEqual(WeatherForecastGridItem other)
        {
            throw new NotImplementedException();
        }
    }
}
