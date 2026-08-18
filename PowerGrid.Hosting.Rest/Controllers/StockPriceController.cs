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
using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PowerGrid.Hosting.Rest.Models.DataTransferObjects;

namespace PowerGrid.Hosting.Rest.Controllers
{
    /// <summary>
    /// Controller which exposes persistence methods for stock prices.
    /// </summary>
    [ApiController]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}")]
    //[ApiExplorerSettings(GroupName = "StockPrices")]
    [Produces(MediaTypeNames.Application.Json)]
    public class StockPriceController : ControllerBase
    {
        /// <summary>The underlying persister host for stock prices.</summary>
        protected StockPricePersisterHost persisterHost;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.Controllers.StockPriceController class.
        /// </summary>
        public StockPriceController(StockPricePersisterHost persisterHost)
        {
            this.persisterHost = persisterHost;
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
            throw new NotImplementedException();
        }
    }
}
