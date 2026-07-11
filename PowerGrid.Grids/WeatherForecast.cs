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

namespace PowerGrid.Grids
{
    /// <summary>
    /// Model/container class holding a weather forecast.
    /// </summary>
    public record WeatherForecast : ModelBase, IGridItem<WeatherForecast>
    {
        /// <summary>The country of the city the weather was forecast for.</summary>
        public String Country { get; init; }

        /// <summary>The city the weather was forecast for.</summary>
        public String City { get; init; }

        /// <summary>The time of day of the weather forecast.</summary>
        public TimeOnly Time { get; init; }

        /// <summary>The forecast temperature.</summary>
        public Int32 Temperature { get; init; }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Grids.WeatherForecast class.
        /// </summary>
        /// <param name="country">The country of the city the weather was forecast for.</param>
        /// <param name="city">The city the weather was forecast for.</param>
        /// <param name="time">The time of day of the weather forecast.</param>
        /// <param name="temperature">The forecast temperature.</param>
        public WeatherForecast(String country, String city, TimeOnly time, Int32 temperature)
        {
            ThrowExceptionIfStringParameterNullOrWhitespace(nameof(country), country);
            ThrowExceptionIfStringParameterNullOrWhitespace(nameof(city), city);

            Country = country;
            City = city;
            Time = time;
            Temperature = temperature;
        }

        /// <inheritdoc/>
        public Int32 KeyCompareTo(WeatherForecast other)
        {
            if (this.Country.CompareTo(other.Country) == 0)
            {
                if (this.City.CompareTo(other.City) == 0)
                {
                    return this.Time.CompareTo(other.Time);
                }
                else
                {
                    return this.City.CompareTo(other.City);
                }
            }
            else
            {
                return this.Country.CompareTo(other.Country);
            }
        }

        /// <inheritdoc/>
        public Boolean ValuePropertiesEqual(WeatherForecast other)
        {
            return this.Temperature.Equals(other.Temperature);
        }
    }
}
