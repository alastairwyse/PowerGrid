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
using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PowerGrid.Hosting.Rest.Models.DataTransferObjects;
using PowerGrid.Hosting.Rest.Models.DataTransferObjects.WeatherForecast;
using PowerGrid.Persistence;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer;

namespace PowerGrid.Hosting.Rest.Controllers
{
    /// <summary>
    /// Controller which exposes persistence methods for weather forecasts.
    /// </summary>
    [ApiController]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}")]
    [Produces(MediaTypeNames.Application.Json)]
    public class WeatherForecastController
    {
        /// <summary>The underlying persister host for weather forecasts.</summary>
        protected WeatherForecastPersisterHost persisterHost;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.Controllers.WeatherForecastController class.
        /// </summary>
        public WeatherForecastController(IPersistenceConcurrencyManager concurrencyManager, WeatherForecastPersister weatherForecastPersister)
        {
            persisterHost = new(concurrencyManager, weatherForecastPersister);
        }

        #region Private/Protected Methods

        protected String ValidateAndConvertWeatherForecastGridCommonKeyProperties(String tag)
        {
            return Uri.UnescapeDataString(tag);
        }

        protected (String Tag, DateOnly Date, TimeOnly Time) ValidateAndConvertWeatherForecastGridOuterKeyProperties(String tag, String date, String time)
        {
            String decodedTag = ValidateAndConvertWeatherForecastGridCommonKeyProperties(tag);
            // TODO: Should probably be calling Uri.UnescapeDataString() on date and gridVersion aswell
            Boolean dateParseResult = DateOnly.TryParse(date, out DateOnly parsedDate);
            if (dateParseResult == false)
            {
                throw new ArgumentException($"Parameter '{nameof(date)}' with value {date} could not be converted to a {typeof(DateOnly).Name}.");
            }
            Boolean timeParseResult = TimeOnly.TryParse(time, out TimeOnly parsedTime);
            if (timeParseResult == false)
            {
                throw new ArgumentException($"Parameter '{nameof(time)}' with value {time} could not be converted to a {typeof(TimeOnly).Name}.");
            }

            return (decodedTag, parsedDate, parsedTime);
        }

        #endregion
    }
}
