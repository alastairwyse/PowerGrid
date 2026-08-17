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
    /// The exception that is thrown when deserialization fails.
    /// </summary>
    public class DeserializationException : Exception
    {
        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.DeserializationException class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public DeserializationException(String message)
            : base(message)
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.DeserializationException class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DeserializationException(String message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
