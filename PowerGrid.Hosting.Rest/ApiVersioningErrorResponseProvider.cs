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

using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Newtonsoft.Json.Linq;

namespace PowerGrid.Hosting.Rest
{
    /// <summary>
    /// Implementation of <see cref="IErrorResponseProvider"/> which returns a <see cref="HttpErrorResponse"/> indicating that an API version is not supported.
    /// </summary>
    public class ApiVersioningErrorResponseProvider : IErrorResponseProvider
    {
        /// <inheritdoc/>
        public IActionResult CreateResponse(ErrorResponseContext context)
        {
            var response = new HttpErrorResponse(context.ErrorCode, context.Message);
            var serializer = new HttpErrorResponseJsonSerializer();
            JObject serializedErrorResponse = serializer.Serialize(response);

            var contentResult = new ContentResult();
            contentResult.Content = serializedErrorResponse.ToString();
            contentResult.ContentType = MediaTypeNames.Application.Json;
            contentResult.StatusCode = StatusCodes.Status400BadRequest;

            return contentResult;
        }
    }
}
