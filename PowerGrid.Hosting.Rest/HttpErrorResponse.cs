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

namespace PowerGrid.Hosting.Rest
{
    /// <summary>
    /// Container class holding the data returned from a REST API when an error occurs.
    /// </summary>
    /// <remarks>Based on the Microsoft REST API Guidelines... https://github.com/Microsoft/api-guidelines/blob/master/Guidelines.md#7102-error-condition-responses.</remarks>
    public class HttpErrorResponse
    {
        /// <summary>An internal code representing the error.</summary>
        /// <remarks>Typically this should contain the class name of the exception which caused the server error.</remarks>
        protected String code;
        /// <summary>A description of the error.</summary>
        protected String message;
        /// <summary>The target of the error.</summary>
        protected String target;
        /// <summary>A collection of key/value pairs which give additional details of the error.</summary>
        protected List<Tuple<String, String>> attributes;
        /// <summary>The error which caused this error.</summary>
        protected HttpErrorResponse innerError;

        /// <summary>
        /// An internal code representing the error.
        /// </summary>
        /// <remarks>Typically this should contain the class name of the exception which caused the server error.</remarks>
        public String Code
        {
            get { return code; }
        }

        /// <summary>
        /// A description of the error.
        /// </summary>
        public String Message
        {
            get { return message; }
        }

        /// <summary>
        /// The target of the error.
        /// </summary>
        public String Target
        {
            get { return target; }
        }

        /// <summary>
        /// A collection of key/value pairs which give additional details of the error.
        /// </summary>
        public IEnumerable<Tuple<String, String>> Attributes
        {
            get { return attributes; }
        }

        /// <summary>
        /// The error which caused this error.
        /// </summary>
        public HttpErrorResponse InnerError
        {
            get { return innerError; }
            set { innerError = value; }
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        public HttpErrorResponse(String code, String message)
        {
            // Ordinarily would have exception handlers here for null or whitespace 'code' and 'message' parameters...
            //   However since this instances of this class will likely be created as part of exception handling code, we don't want to throw further exceptions and risk hiding/losing the original exception details.

            this.code = code;
            this.message = message;
            target = null;
            attributes = new List<Tuple<String, String>>();
            innerError = null;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        /// <param name="target">The target of the error.</param>
        public HttpErrorResponse(String code, String message, String target)
            : this(code, message)
        {
            this.target = target;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        /// <param name="attributes">A collection of key/value pairs which give additional details of the error.</param>
        public HttpErrorResponse(String code, String message, List<Tuple<String, String>> attributes)
            : this(code, message)
        {
            this.attributes = attributes;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        /// <param name="innerError">The error which caused this error.</param>
        public HttpErrorResponse(String code, String message, HttpErrorResponse innerError)
            : this(code, message)
        {
            this.innerError = innerError;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        /// <param name="target">The target of the error.</param>
        /// <param name="attributes">A collection of key/value pairs which give additional details of the error.</param>
        public HttpErrorResponse(String code, String message, String target, List<Tuple<String, String>> attributes)
            : this(code, message)
        {
            this.target = target;
            this.attributes = attributes;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        /// <param name="attributes">A collection of key/value pairs which give additional details of the error.</param>
        /// <param name="innerError">The error which caused this error.</param>
        public HttpErrorResponse(String code, String message, List<Tuple<String, String>> attributes, HttpErrorResponse innerError)
            : this(code, message)
        {
            this.attributes = attributes;
            this.innerError = innerError;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        /// <param name="target">The target of the error.</param>
        /// <param name="innerError">The error which caused this error.</param>
        public HttpErrorResponse(String code, String message, String target, HttpErrorResponse innerError)
            : this(code, message)
        {
            this.target = target;
            this.innerError = innerError;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.HttpErrorResponse class.
        /// </summary>
        /// <param name="code">An internal code representing the error.</param>
        /// <param name="message">A description of the error.</param>
        /// <param name="target">The target of the error.</param>
        /// <param name="attributes">A collection of key/value pairs which give additional details of the error.</param>
        /// <param name="innerError">The error which caused this error.</param>
        public HttpErrorResponse(String code, String message, String target, List<Tuple<String, String>> attributes, HttpErrorResponse innerError)
            : this(code, message)
        {
            this.target = target;
            this.attributes = attributes;
            this.innerError = innerError;
        }
    }
}
