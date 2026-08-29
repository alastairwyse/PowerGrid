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
using PowerGrid.Grids;

namespace PowerGrid.Persistence.Models.PersistenceTransferObjects
{
    /// <summary>
    /// Model/container class holding a <see cref="WeatherForecastGridItem"/> augmented with properties allowing it to be transferred to and from persistent storage.
    /// </summary>
    /// <param name="Id">A unique id for the object within persistent storage.</param>
    /// <param name="Tag">A tag used to classify the grid.</param>
    /// <param name="Date">The date the weather was forecast for.</param>
    /// <param name="Time">The time of day of the weather was forecast for.</param>
    /// <param name="Country">The country of the city the weather was forecast for.</param>
    /// <param name="City">The city the weather was forecast for.</param>
    /// <param name="Temperature">The forecast temperature.</param>
    /// <param name="TransactionFrom">The date and time that the object became active.</param>
    /// <param name="TransactionTo">The date and time that the object was superseded or deleted.</param>
    public record WeatherForecastGridItemPTO
    (
        Int64 Id,
        String Tag, 
        DateOnly Date,
        TimeOnly Time,
        String Country, 
        String City,  
        Int32 Temperature,
        DateTime TransactionFrom,
        DateTime TransactionTo
    )
        : WeatherForecastGridItem(Tag, Date, Time, Country, City, Temperature), IWeatherForecastGridOuterKeyProperties, IGridItem<WeatherForecastGridItem>, IPersistenceTransferObject
    {
    }
}
