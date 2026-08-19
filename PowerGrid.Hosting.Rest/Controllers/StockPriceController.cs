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
using PowerGrid.Persistence;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer;

namespace PowerGrid.Hosting.Rest.Controllers
{
    /// <summary>
    /// Controller which exposes persistence methods for stock prices.
    /// </summary>
    [ApiController]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}")]
    [ApiExplorerSettings(GroupName = "StockPrices")]
    [Produces(MediaTypeNames.Application.Json)]
    public class StockPriceController : ControllerBase
    {
        /// <summary>The underlying persister host for stock prices.</summary>
        protected StockPricePersisterHost persisterHost;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.Controllers.StockPriceController class.
        /// </summary>
        public StockPriceController(PersistenceConcurrencyManager concurrencyManager, StockPricePersister stockPricePersister)
        {
            persisterHost = new(concurrencyManager, stockPricePersister);
        }

        /// <summary>
        /// Writes the specified grid of stock prices to persistent storage.
        /// </summary>
        /// <param name="requestParameters">The request parameters.</param>
        /// <returns>The response parameters.</returns>
        [HttpPost]
        [Route("stockPrices")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<PersistGridResponseParameters> PersistGrid([FromBody] PersistGridRequestParameters requestParameters)
        {
            Grids.StockPriceGridOuterKeyProperties stockPriceGridOuterKeyProperties = new
            (
                requestParameters.StockPriceGridOuterKeyProperties.Tag,
                requestParameters.StockPriceGridOuterKeyProperties.DataSource,
                requestParameters.StockPriceGridOuterKeyProperties.Date
            );
            (Int32 version, Core.GridComparisonStatistics gridComparisonStatistics) response = persisterHost.PersistGrid(stockPriceGridOuterKeyProperties, requestParameters.Items);

            return new ActionResult<PersistGridResponseParameters>
            (
                new PersistGridResponseParameters()
                {
                    Version = response.version, 
                    GridComparisonStatistics = new Core.GridComparisonStatistics
                    (
                        response.gridComparisonStatistics.ItemsAddedCount,
                        response.gridComparisonStatistics.ItemsUpdatedCount,
                        response.gridComparisonStatistics.ItemsDeletedCount
                    )
                }
            );
        }

        /// <summary>
        /// Retrieve the grid of stock prices with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="dataSource">The source/entity which provided the price.</param>
        /// <param name="date">The date the price was quoted for.</param>
        /// <param name="gridVersion">The version number of the grid to retrieve.</param>
        /// <returns>The stock prices.</returns>
        [HttpGet]
        [Route("stockPrices/tag/{tag}/dataSource/{dataSource}/date/{date}/gridVersion/{gridVersion}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IEnumerable<StockPriceGridItemPTO> GetGrid([FromRoute] String tag, [FromRoute] String dataSource, [FromRoute] String date, [FromRoute] String gridVersion)
        {
            String decodedTag = Uri.UnescapeDataString(tag);
            String decodedDataSource = Uri.UnescapeDataString(dataSource);
            // TODO: Should probably be calling Uri.UnescapeDataString() on date and gridVersion aswell
            Boolean dateParseResult = DateOnly.TryParse(date, out DateOnly parsedDate);
            if (dateParseResult == false)
            {
                throw new ArgumentException($"Parameter '{nameof(date)}' with value {date} could not be converted to a {typeof(DateOnly).Name}.");
            }
            Boolean versionParseResult = Int32.TryParse(gridVersion, out Int32 parsedVersion);
            if (versionParseResult == false)
            {
                throw new ArgumentException($"Parameter '{nameof(gridVersion)}' with value {gridVersion} could not be converted to a {typeof(Int32).Name}.");
            }
            Grids.StockPriceGridOuterKeyProperties stockPriceGridOuterKeyProperties = new
            (
                decodedTag,
                decodedDataSource,
                parsedDate
            );

            return persisterHost.GetGrid(stockPriceGridOuterKeyProperties, parsedVersion);
        }
    }
}
