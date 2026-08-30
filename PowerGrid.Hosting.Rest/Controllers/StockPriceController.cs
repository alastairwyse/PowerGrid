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
using PowerGrid.Persistence.Models;
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
    [Produces(MediaTypeNames.Application.Json)]
    public class StockPriceController : ControllerBase
    {
        /// <summary>The underlying persister host for stock prices.</summary>
        protected StockPricePersisterHost persisterHost;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.Controllers.StockPriceController class.
        /// </summary>
        public StockPriceController(IPersistenceConcurrencyManager concurrencyManager, StockPricePersister stockPricePersister)
        {
            persisterHost = new(concurrencyManager, stockPricePersister);
        }

        /// <summary>
        /// Writes the specified grid of stock prices to persistent storage.
        /// </summary>
        /// <param name="stockPriceGrid">The request parameters.</param>
        /// <returns>The response parameters.</returns>
        [HttpPost]
        [ApiExplorerSettings(GroupName = "StockPrices")]
        [Route("stockPrices")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public ActionResult<PersistGridResponse> PersistGrid([FromBody] StockPriceGrid stockPriceGrid)
        {
            Grids.StockPriceGridOuterKeyProperties stockPriceGridOuterKeyProperties = new
            (
                stockPriceGrid.StockPriceGridOuterKeyProperties.Tag,
                stockPriceGrid.StockPriceGridOuterKeyProperties.DataSource,
                stockPriceGrid.StockPriceGridOuterKeyProperties.Date
            );
            (Int32 version, Core.GridComparisonStatistics gridComparisonStatistics) result = persisterHost.PersistGrid(stockPriceGridOuterKeyProperties, stockPriceGrid.Items);

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
        /// Retrieve the grid of stock prices with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="dataSource">The source/entity which provided the price.</param>
        /// <param name="date">The date the price was quoted for.</param>
        /// <param name="gridVersion">The version number of the grid to retrieve.</param>
        /// <returns>The stock prices.</returns>
        [HttpGet]
        [ApiExplorerSettings(GroupName = "StockPrices")]
        [Route("stockPrices/tag/{tag}/dataSource/{dataSource}/date/{date}/gridVersion/{gridVersion}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<StockPriceGrid> GetGrid([FromRoute] String tag, [FromRoute] String dataSource, [FromRoute] String date, [FromRoute] String gridVersion)
        {
            (String decodedTag, String decodedDataSource, DateOnly parsedDate) = ValidateAndConvertStockPriceGridOuterKeyProperties(tag, dataSource, date);
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
            IEnumerable<StockPriceGridItemPTO> gridItems = persisterHost.GetGrid(stockPriceGridOuterKeyProperties, parsedVersion);
            StockPriceGrid returnGrid = new()
            {
                StockPriceGridOuterKeyProperties = new StockPriceGridOuterKeyProperties()
                {
                    Tag = decodedTag,
                    DataSource = decodedDataSource,
                    Date = parsedDate
                }, 
            };
            List<Grids.StockPrice> items = new();
            foreach (StockPriceGridItemPTO currentPTO in gridItems)
            {
                items.Add(new Grids.StockPrice(currentPTO.Company, currentPTO.Price));
            }
            returnGrid.Items = items;

            return returnGrid;
        }

        /// <summary>
        /// Gets details of all the grids with the specified key properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="dataSource">The source/entity which provided the price.</param>
        /// <param name="date">The date the price was quoted for.</param>
        /// <returns>A collection of versions and corresponding UTC transaction (creation) timestamps for the grids.</returns>
        [HttpGet]
        [ApiExplorerSettings(GroupName = "StockPriceGrids")]
        [Route("stockPriceGrids/tag/{tag}/dataSource/{dataSource}/date/{date}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IList<GridVersionAndTransactionTimestamp>> GetGridDetails([FromRoute] String tag, [FromRoute] String dataSource, [FromRoute] String date)
        {
            (String decodedTag, String decodedDataSource, DateOnly parsedDate) = ValidateAndConvertStockPriceGridOuterKeyProperties(tag, dataSource, date);
            Grids.StockPriceGridOuterKeyProperties stockPriceGridOuterKeyProperties = new
            (
                decodedTag,
                decodedDataSource,
                parsedDate
            );

            return new ActionResult<IList<GridVersionAndTransactionTimestamp>>(persisterHost.GetGridDetails(stockPriceGridOuterKeyProperties));
        }

        /// <summary>
        /// Gets details of all the grids with the specified common key properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <returns>The response parameters.</returns>
        [HttpGet]
        [ApiExplorerSettings(GroupName = "StockPriceGrids")]
        [Route("stockPriceGrids/tag/{tag}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IList<GetGridDetailsResponseItem> GetGridDetails([FromRoute] String tag)
        {
            String decodedTag = ValidateAndConvertStockPriceGridCommonKeyProperties(tag);
            Grids.GridCommonKeyProperties gridCommonKeyProperties = new(decodedTag);
            IList<Tuple<Grids.StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> result = persisterHost.GetGridDetails(gridCommonKeyProperties);
            List<GetGridDetailsResponseItem> response = new();
            foreach (Tuple<Grids.StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp> currentResult in result)
            {
                response.Add(new GetGridDetailsResponseItem()
                {
                    StockPriceGridOuterKeyProperties = new()
                    {
                        Tag = currentResult.Item1.Tag, 
                        DataSource = currentResult.Item1.DataSource, 
                        Date = currentResult.Item1.Date
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
        /// <param name="dataSource">The source/entity which provided the price.</param>
        /// <param name="date">The date the price was quoted for.</param>
        [HttpDelete]
        [ApiExplorerSettings(GroupName = "StockPriceGrids")]
        [Route("stockPriceGrids/tag/{tag}/dataSource/{dataSource}/date/{date}")]
        public void SoftDeleteLatestGrid([FromRoute] String tag, [FromRoute] String dataSource, [FromRoute] String date)
        {
            (String decodedTag, String decodedDataSource, DateOnly parsedDate) = ValidateAndConvertStockPriceGridOuterKeyProperties(tag, dataSource, date);
            Grids.StockPriceGridOuterKeyProperties stockPriceGridOuterKeyProperties = new
            (
                decodedTag,
                decodedDataSource,
                parsedDate
            );
            persisterHost.SoftDeleteLatestGrid(stockPriceGridOuterKeyProperties);
        }

        /// <summary>
        /// Hard deletes all grids with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        /// <param name="dataSource">The source/entity which provided the price.</param>
        /// <param name="date">The date the price was quoted for.</param>
        [HttpDelete]
        [ApiExplorerSettings(GroupName = "Administration")]
        [Route("stockPriceGrids/tag/{tag}/dataSource/{dataSource}/date/{date}:hardDelete")]
        public void HardDeleteGrids([FromRoute] String tag, [FromRoute] String dataSource, [FromRoute] String date)
        {
            (String decodedTag, String decodedDataSource, DateOnly parsedDate) = ValidateAndConvertStockPriceGridOuterKeyProperties(tag, dataSource, date);
            Grids.StockPriceGridOuterKeyProperties stockPriceGridOuterKeyProperties = new
            (
                decodedTag,
                decodedDataSource,
                parsedDate
            );
            persisterHost.HardDeleteGrids(stockPriceGridOuterKeyProperties);
        }

        /// <summary>
        /// Hard deletes all grids with the specified properties.
        /// </summary>
        /// <param name="tag">A tag used to classify the grid.</param>
        [HttpDelete]
        [ApiExplorerSettings(GroupName = "Administration")]
        [Route("stockPriceGrids/tag/{tag}:hardDelete")]
        public void HardDeleteGrids([FromRoute] String tag)
        {
            String decodedTag = ValidateAndConvertStockPriceGridCommonKeyProperties(tag);
            Grids.GridCommonKeyProperties gridCommonKeyProperties = new(decodedTag);
            persisterHost.HardDeleteGrids(gridCommonKeyProperties);
        }

        #region Private/Protected Methods

        protected String ValidateAndConvertStockPriceGridCommonKeyProperties(String tag)
        {
            return Uri.UnescapeDataString(tag);
        }

        protected (String Tag, String DataSource, DateOnly Date) ValidateAndConvertStockPriceGridOuterKeyProperties(String tag, String dataSource, String date)
        {
            String decodedTag = ValidateAndConvertStockPriceGridCommonKeyProperties(tag);
            String decodedDataSource = Uri.UnescapeDataString(dataSource);
            // TODO: Should probably be calling Uri.UnescapeDataString() on date and gridVersion aswell
            Boolean dateParseResult = DateOnly.TryParse(date, out DateOnly parsedDate);
            if (dateParseResult == false)
            {
                throw new ArgumentException($"Parameter '{nameof(date)}' with value {date} could not be converted to a {typeof(DateOnly).Name}.");
            }

            return (decodedTag, decodedDataSource, parsedDate);
        }

        #endregion
    }
}
