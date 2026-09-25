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

        /// <summary>
        /// Writes the specified grid of weather forecasts to persistent storage.
        /// </summary>
        /// <param name="weatherForecastGrid">The request parameters.</param>
        /// <returns>The response parameters.</returns>
        [HttpPost]
        [ApiExplorerSettings(GroupName = "WeatherForecasts")]
        [Route("weatherForecasts")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public ActionResult<PersistGridResponse> PersistGrid([FromBody] InputWeatherForecastGrid weatherForecastGrid)
        {
            Grids.WeatherForecastGridOuterKeyProperties weatherForecastGridOuterKeyProperties = new
            (
                weatherForecastGrid.WeatherForecastGridOuterKeyProperties.Tag,
                weatherForecastGrid.WeatherForecastGridOuterKeyProperties.Date,
                weatherForecastGrid.WeatherForecastGridOuterKeyProperties.Time
            );
            (Int32 version, Core.GridComparisonStatistics gridComparisonStatistics) result = persisterHost.PersistGrid(weatherForecastGridOuterKeyProperties, weatherForecastGrid.Items);

            return new ActionResult<PersistGridResponse>
            (
                new PersistGridResponse()
                {
                    Version = result.version,
                    GridComparisonStatistics = new Core.GridComparisonStatistics
                    (
                        result.gridComparisonStatistics.ItemsAddedCount,
                        result.gridComparisonStatistics.ItemsUpdatedCount,
                        result.gridComparisonStatistics.ItemsDeletedCount
                    )
                }
            );
        }

        /// <summary>
        /// Retrieve the grid of weather forecasts with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="date">The date the weather was forecast for.</param>
        /// <param name="time">The time of day of the weather was forecast for.</param>
        /// <param name="gridVersion">The version number of the grid to retrieve.</param>
        /// <returns>The weather forecasts.</returns>
        [HttpGet]
        [ApiExplorerSettings(GroupName = "WeatherForecasts")]
        [Route("weatherForecasts/tag/{tag}/date/{date}/time/{time}/gridVersion/{gridVersion}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<OutputWeatherForecastGrid> GetGrid([FromRoute] String tag, [FromRoute] String date, [FromRoute] String time, [FromRoute] String gridVersion)
        {
            (String decodedTag, DateOnly parsedDate, TimeOnly parsedTime) = ValidateAndConvertWeatherForecastGridOuterKeyProperties(tag, date, time);
            Boolean versionParseResult = Int32.TryParse(gridVersion, out Int32 parsedVersion);
            if (versionParseResult == false)
            {
                throw new ArgumentException($"Parameter '{nameof(gridVersion)}' with value {gridVersion} could not be converted to a {typeof(Int32).Name}.");
            }
            Grids.WeatherForecastGridOuterKeyProperties weatherForecastGridOuterKeyProperties = new
            (
                decodedTag,
                parsedDate, 
                parsedTime
            );
            IEnumerable<WeatherForecastGridItemPTO> gridItems = persisterHost.GetGrid(weatherForecastGridOuterKeyProperties, parsedVersion);
            OutputWeatherForecastGrid returnGrid = new()
            {
                WeatherForecastGridOuterKeyProperties = new WeatherForecastGridOuterKeyProperties()
                {
                    Tag = decodedTag,
                    Date = parsedDate,
                    Time = parsedTime,
                },
                Items = new List<WeatherForecastGridItemPTO>(gridItems)
            };

            return returnGrid;
        }

        /// <summary>
        /// Gets details of all the grids with the specified key properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="date">The date the weather was forecast for.</param>
        /// <param name="time">The time of day of the weather was forecast for.</param>
        /// <returns>A collection of versions and corresponding UTC transaction (creation) timestamps for the grids.</returns>
        [HttpGet]
        [ApiExplorerSettings(GroupName = "WeatherForecastGrids")]
        [Route("weatherForecastGrids/tag/{tag}/date/{date}/time/{time}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IList<GridVersionAndTransactionTimestamp>> GetGridDetails([FromRoute] String tag, [FromRoute] String date, [FromRoute] String time)
        {
            (String decodedTag, DateOnly parsedDate, TimeOnly parsedTime) = ValidateAndConvertWeatherForecastGridOuterKeyProperties(tag, date, time);
            Grids.WeatherForecastGridOuterKeyProperties weatherForecastGridOuterKeyProperties = new
            (
                decodedTag,
                parsedDate, 
                parsedTime
            );

            return new ActionResult<IList<GridVersionAndTransactionTimestamp>>(persisterHost.GetGridDetails(weatherForecastGridOuterKeyProperties));
        }

        /// <summary>
        /// Gets details of all the grids with the specified common key properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <returns>The response parameters.</returns>
        [HttpGet]
        [ApiExplorerSettings(GroupName = "WeatherForecastGrids")]
        [Route("weatherForecastGrids/tag/{tag}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IList<GetGridDetailsResponseItem> GetGridDetails([FromRoute] String tag)
        {
            String decodedTag = ValidateAndConvertWeatherForecastGridCommonKeyProperties(tag);
            Grids.GridCommonKeyProperties gridCommonKeyProperties = new(decodedTag);
            IList<Tuple<Grids.WeatherForecastGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> result = persisterHost.GetGridDetails(gridCommonKeyProperties);
            List<GetGridDetailsResponseItem> response = new();
            foreach (Tuple<Grids.WeatherForecastGridOuterKeyProperties, GridVersionAndTransactionTimestamp> currentResult in result)
            {
                response.Add(new GetGridDetailsResponseItem()
                {
                    WeatherForecastGridOuterKeyProperties = new()
                    {
                        Tag = currentResult.Item1.Tag,
                        Date = currentResult.Item1.Date, 
                        Time = currentResult.Item1.Time
                    },
                    Version = currentResult.Item2.Version,
                    TransactionTimestamp = currentResult.Item2.TransactionTimestamp
                });
            }

            return response;
        }

        /// <summary>
        /// Soft deletes all items in the latest grid with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="date">The date the weather was forecast for.</param>
        /// <param name="time">The time of day of the weather was forecast for.</param>
        [HttpDelete]
        [ApiExplorerSettings(GroupName = "WeatherForecastGrids")]
        [Route("weatherForecastGrids/tag/{tag}/date/{date}/time/{time}")]
        public void SoftDeleteLatestGrid([FromRoute] String tag, [FromRoute] String date, [FromRoute] String time)
        {
            (String decodedTag, DateOnly parsedDate, TimeOnly parsedTime) = ValidateAndConvertWeatherForecastGridOuterKeyProperties(tag, date, time);
            Grids.WeatherForecastGridOuterKeyProperties weatherForecastGridOuterKeyProperties = new
            (
                decodedTag,
                parsedDate, 
                parsedTime
            );
            persisterHost.SoftDeleteLatestGrid(weatherForecastGridOuterKeyProperties);
        }

        /// <summary>
        /// Hard deletes all grids with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="date">The date the weather was forecast for.</param>
        /// <param name="time">The time of day of the weather was forecast for.</param>
        [HttpDelete]
        [ApiExplorerSettings(GroupName = "Administration")]
        [Route("weatherForecastGrids/tag/{tag}/date/{date}/time/{time}:hardDelete")]
        public void HardDeleteGrids([FromRoute] String tag, [FromRoute] String date, [FromRoute] String time)
        {
            (String decodedTag, DateOnly parsedDate, TimeOnly parsedTime) = ValidateAndConvertWeatherForecastGridOuterKeyProperties(tag, date, time);
            Grids.WeatherForecastGridOuterKeyProperties weatherForecastGridOuterKeyProperties = new
            (
                decodedTag,
                parsedDate, 
                parsedTime
            );
            persisterHost.HardDeleteGrids(weatherForecastGridOuterKeyProperties);
        }

        /// <summary>
        /// Hard deletes all grids with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        [HttpDelete]
        [ApiExplorerSettings(GroupName = "Administration")]
        [Route("weatherForecastGrids/tag/{tag}:hardDelete")]
        public void HardDeleteGrids([FromRoute] String tag)
        {
            String decodedTag = ValidateAndConvertWeatherForecastGridCommonKeyProperties(tag);
            Grids.GridCommonKeyProperties gridCommonKeyProperties = new(decodedTag);
            persisterHost.HardDeleteGrids(gridCommonKeyProperties);
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
