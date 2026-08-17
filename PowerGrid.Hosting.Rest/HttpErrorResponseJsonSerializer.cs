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
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

namespace PowerGrid.Hosting.Rest
{
    /// <summary>
    /// Serializes and deserializes <see cref="HttpErrorResponse"/> instances to and from JSON documents.
    /// </summary>
    public class HttpErrorResponseJsonSerializer
    {
        protected const String errorPropertyName = "error";
        protected const String codePropertyName = "code";
        protected const String messagePropertyName = "message";
        protected const String targetPropertyName = "target";
        protected const String attributesPropertyName = "attributes";
        protected const String namePropertyName = "name";
        protected const String valuePropertyName = "value";
        protected const String innerErrorPropertyName = "innererror";

        /// <summary>
        /// Serializes the specified <see cref="HttpErrorResponse"/> to a JSON document.
        /// </summary>
        /// <param name="httpErrorResponse">The HttpErrorResponse object to serialize.</param>
        /// <returns>A JSON document representing the HttpErrorResponse.</returns>
        public JObject Serialize(HttpErrorResponse httpErrorResponse)
        {
            var returnDocument = new JObject();

            returnDocument.Add(errorPropertyName, SerializeError(httpErrorResponse));

            return returnDocument;
        }

        /// <summary>
        /// Deserializes the specified JSON object to a <see cref="HttpErrorResponse"/> object.
        /// </summary>
        /// <param name="jsonObject">The JSON object to deserialize.</param>
        /// <returns>The deserialized HttpErrorResponse.</returns>
        /// <exception cref="DeserializationException">Failed to deserialize the HttpErrorResponse.</exception>
        public HttpErrorResponse Deserialize(JObject jsonObject)
        {
            if (jsonObject[errorPropertyName] != null && jsonObject[errorPropertyName] is JObject)
            {
                return DeserializeError((JObject)jsonObject[errorPropertyName]);
            }
            else
            {
                throw new DeserializationException($"Failed to deserialize {nameof(HttpErrorResponse)}.  The specified {nameof(JObject)} did not contain an '{errorPropertyName}' property.");
            }
        }

        /// <summary>
        /// Serializes the 'error' and 'innererror' properties of the JSON document returned by the Serialize() method.
        /// </summary>
        /// <param name="httpErrorResponse">The HttpErrorResponse object to serialize.</param>
        /// <returns>The 'error' or 'innererror' property of the JSON document.</returns>
        protected JObject SerializeError(HttpErrorResponse httpErrorResponse)
        {
            var returnDocument = new JObject();

            returnDocument.Add(codePropertyName, httpErrorResponse.Code);
            returnDocument.Add(messagePropertyName, httpErrorResponse.Message);
            if (httpErrorResponse.Target != null)
            {
                returnDocument.Add(targetPropertyName, httpErrorResponse.Target);
            }
            var attributesJson = new JArray();
            foreach (Tuple<String, String> currentAttribute in httpErrorResponse.Attributes)
            {
                var currentAttributeJson = new JObject();
                currentAttributeJson.Add(namePropertyName, currentAttribute.Item1);
                currentAttributeJson.Add(valuePropertyName, currentAttribute.Item2);
                attributesJson.Add(currentAttributeJson);
            }
            if (attributesJson.Count > 0)
            {
                returnDocument.Add(attributesPropertyName, attributesJson);
            }
            if (httpErrorResponse.InnerError != null)
            {
                returnDocument.Add(innerErrorPropertyName, SerializeError(httpErrorResponse.InnerError));
            }

            return returnDocument;
        }

        /// <summary>
        /// Deserializes the 'error' and 'innererror' properties of a JSON document containing a serialized <see cref="HttpErrorResponse"/>.
        /// </summary>
        /// <param name="jsonObject">The 'error' or 'innererror' property of the JSON object to deserialize.</param>
        /// <returns>The deserialized 'error' or 'innererror' property, or null if the property could not be deserialized.</returns>
        /// <exception cref="DeserializationException">Failed to deserialize the property.</exception>
        protected HttpErrorResponse DeserializeError(JObject jsonObject)
        {
            // Deserialize non-optional properties
            if (jsonObject[codePropertyName] == null)
                throw new DeserializationException($"Failed to deserialize {nameof(HttpErrorResponse)} 'error' or 'innererror' property.  The specified {nameof(JObject)} did not contain a '{codePropertyName}' property.");
            if (jsonObject[messagePropertyName] == null)
                throw new DeserializationException($"Failed to deserialize {nameof(HttpErrorResponse)} 'error' or 'innererror' property.  The specified {nameof(JObject)} did not contain a '{messagePropertyName}' property.");

            String code = jsonObject[codePropertyName].ToString();
            String message = jsonObject[messagePropertyName].ToString();
            String target = null;
            var attributes = new List<Tuple<String, String>>();
            HttpErrorResponse innerError = null;

            // Deserialize optional properties
            if (jsonObject[targetPropertyName] != null)
            {
                target = jsonObject[targetPropertyName].ToString();
            }
            if (jsonObject[attributesPropertyName] != null && jsonObject[attributesPropertyName] is JArray)
            {
                var attributesJson = (JArray)jsonObject[attributesPropertyName];
                foreach (JObject currentAttributeJson in attributesJson)
                {
                    if (currentAttributeJson[namePropertyName] != null && currentAttributeJson[valuePropertyName] != null)
                    {
                        attributes.Add(new Tuple<String, String>(currentAttributeJson[namePropertyName].ToString(), currentAttributeJson[valuePropertyName].ToString()));
                    }
                }
            }
            if (jsonObject[innerErrorPropertyName] != null && jsonObject[innerErrorPropertyName] is JObject)
            {
                innerError = DeserializeError((JObject)jsonObject[innerErrorPropertyName]);
            }

            // Create the return HttpErrorResponse
            if (target != null && attributes.Count > 0 && innerError != null)
            {
                return new HttpErrorResponse(code, message, target, attributes, innerError);
            }
            else if (target != null && attributes.Count > 0)
            {
                return new HttpErrorResponse(code, message, target, attributes);
            }
            else if (target != null && innerError != null)
            {
                return new HttpErrorResponse(code, message, target, innerError);
            }
            else if (attributes.Count > 0 && innerError != null)
            {
                return new HttpErrorResponse(code, message, attributes, innerError);
            }
            else if (target != null)
            {
                return new HttpErrorResponse(code, message, target);
            }
            else if (attributes.Count > 0)
            {
                return new HttpErrorResponse(code, message, attributes);
            }
            else if (innerError != null)
            {
                return new HttpErrorResponse(code, message, innerError);
            }
            else
            {
                return new HttpErrorResponse(code, message);
            }
        }
    }
}
