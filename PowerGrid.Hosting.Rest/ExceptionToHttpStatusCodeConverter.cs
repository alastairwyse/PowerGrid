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
using System.Net;

namespace PowerGrid.Hosting.Rest
{
    /// <summary>
    /// Converts an Exception to an equivalent HttpStatusCode.
    /// </summary>
    public class ExceptionToHttpStatusCodeConverter
    {
        /// <summary>Maps a type (assignable to Exception) to a HttpStatusCode.</summary>
        protected Dictionary<Type, HttpStatusCode> typeToHttpStatusCodeMap;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.ExceptionToHttpStatusCodeConverter class.
        /// </summary>
        public ExceptionToHttpStatusCodeConverter()
        {
            typeToHttpStatusCodeMap = new Dictionary<Type, HttpStatusCode>();
            InitialisetypeToStatusCodeMap(typeToHttpStatusCodeMap);
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.ExceptionToHttpStatusCodeConverter class.
        /// </summary>
        /// <param name="typesAndStatusCodes">A collection of tuples containing 2 values: A type (assignable to Exception), and an HTTP status code to map the exception type to.</param>
        public ExceptionToHttpStatusCodeConverter(IEnumerable<Tuple<Type, HttpStatusCode>> typesAndStatusCodes)
        {
            foreach (var currentMapping in typesAndStatusCodes)
            {
                AddMapping(currentMapping.Item1, currentMapping.Item2);
            }
        }

        /// <summary>
        /// Adds a mapping to the converter.
        /// </summary>
        /// <param name="exceptionType">The type (assignable to Exception) to map from.</param>
        /// <param name="httpStatusCode">The HTTP status code to map the exception type to.</param>
        public void AddMapping(Type exceptionType, HttpStatusCode httpStatusCode)
        {
            if (typeof(Exception).IsAssignableFrom(exceptionType) == false)
                throw new ArgumentException($"Type '{exceptionType.FullName}' specified in parameter '{nameof(exceptionType)}' is not assignable to '{typeof(Exception).FullName}'.", nameof(exceptionType));

            if (typeToHttpStatusCodeMap.ContainsKey(exceptionType) == true)
            {
                typeToHttpStatusCodeMap.Remove(exceptionType);
            }
            typeToHttpStatusCodeMap.Add(exceptionType, httpStatusCode);
        }

        /// <summary>
        /// Converts the specified exception to its equivalent HTTP status code.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <returns>The HTTP status code.</returns>
        public HttpStatusCode Convert(Exception exception)
        {
            Type exceptionType = exception.GetType();
            while (typeToHttpStatusCodeMap.ContainsKey(exceptionType) == false)
            {
                exceptionType = exceptionType.BaseType;
            }

            return typeToHttpStatusCodeMap[exceptionType];
        }

        /// <summary>
        /// Initialises the specified type to HTTP status code map with default mappings.
        /// </summary>
        /// <param name="typeToHttpStatusCodeMap">The type to HTTP status code map to initialise.</param>
        protected void InitialisetypeToStatusCodeMap(Dictionary<Type, HttpStatusCode> typeToHttpStatusCodeMap)
        {
            typeToHttpStatusCodeMap.Add(typeof(ArgumentException), HttpStatusCode.BadRequest);
            typeToHttpStatusCodeMap.Add(typeof(NotFoundException), HttpStatusCode.NotFound);
            typeToHttpStatusCodeMap.Add(typeof(Exception), HttpStatusCode.InternalServerError);
        }
    }
}
