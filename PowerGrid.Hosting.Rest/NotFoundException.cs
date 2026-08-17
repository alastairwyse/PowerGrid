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

namespace PowerGrid.Hosting.Rest
{
    /// <summary>
    /// The exception that is thrown when a resource doesn't exist or could not be found.
    /// </summary>
    /// <remarks>Equivalent to an <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404">HTTP 404 status error</see>.</remarks>
    public class NotFoundException : Exception
    {
        /// <summary>A unique identifier for the resource.</summary>
        protected String resourceId;

        /// <summary>
        /// A unique identifier for the resource.
        /// </summary>
        public String ResourceId
        {
            get
            {
                return resourceId;
            }
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.NotFoundException class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="resourceId">A unique identifier for the resource.</param>
        public NotFoundException(String message, String resourceId)
            : base(message)
        {
            this.resourceId = resourceId;
        }
    }
}
