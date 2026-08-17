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
    /// Converts an Exception (or object derived from Exception) to an <see cref="HttpErrorResponse"/> object.
    /// </summary>
    public class ExceptionToHttpErrorResponseConverter
    {
        /// <summary>Maps a type (assignable to Exception) to a conversion function which converts that type to an HttpErrorResponse.</summary>
        protected Dictionary<Type, Func<Exception, HttpErrorResponse>> typeToConversionFunctionMap;
        /// <summary>The limit of the depth of inner exceptions to convert.</summary>
        protected Int32 innerExceptionDepthLimit;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.ExceptionToHttpErrorResponseConverter class.
        /// </summary>
        public ExceptionToHttpErrorResponseConverter()
        {
            typeToConversionFunctionMap = new Dictionary<Type, Func<Exception, HttpErrorResponse>>();
            InitialiseTypeToConversionFunctionMap(typeToConversionFunctionMap);
            innerExceptionDepthLimit = Int32.MaxValue;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.ExceptionToHttpErrorResponseConverter class.
        /// </summary>
        /// <param name="innerExceptionDepthLimit">The limit of the depth of inner exceptions to convert.  A value of 0 will convert only the top level exception and no inner exceptions.</param>
        public ExceptionToHttpErrorResponseConverter(Int32 innerExceptionDepthLimit)
            : this()
        {
            ThrowExceptionIfInnerExceptionDepthLimitParameterOutOfRange(nameof(innerExceptionDepthLimit), innerExceptionDepthLimit);
            this.innerExceptionDepthLimit = innerExceptionDepthLimit;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.ExceptionToHttpErrorResponseConverter class.
        /// </summary>
        /// <param name="typesAndConversionFunctions">A collection of tuples containing 2 values: A type (assignable to Exception) that the conversion function in the second component converts, and the conversion function which accepts an Exception object and returns a HttpErrorResponse.</param>
        /// <remarks>Note that the conversion functions should not handle the exception's 'InnerException' property, nor assign to the HttpErrorResponse's 'InnerError' property.</remarks>
        public ExceptionToHttpErrorResponseConverter(IEnumerable<Tuple<Type, Func<Exception, HttpErrorResponse>>> typesAndConversionFunctions)
            : this()
        {
            foreach (Tuple<Type, Func<Exception, HttpErrorResponse>> currentTypeAndConversionFunction in typesAndConversionFunctions)
            {
                AddConversionFunction(currentTypeAndConversionFunction.Item1, currentTypeAndConversionFunction.Item2);
            }
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Hosting.Rest.ExceptionToHttpErrorResponseConverter class.
        /// </summary>
        /// <param name="typesAndConversionFunctions">A collection of tuples containing 2 values: A type (assignable to Exception) that the conversion function in the second component converts, and the conversion function which accepts an Exception object and returns a HttpErrorResponse.</param>
        /// <param name="innerExceptionDepthLimit">The limit of the depth of inner exceptions to convert.  A value of 0 will convert only the top level exception and no inner exceptions.</param>
        /// <remarks>Note that the conversion functions should not handle the exception's 'InnerException' property, nor assign to the HttpErrorResponse's 'InnerError' property.</remarks>
        public ExceptionToHttpErrorResponseConverter(IEnumerable<Tuple<Type, Func<Exception, HttpErrorResponse>>> typesAndConversionFunctions, Int32 innerExceptionDepthLimit)
            : this(typesAndConversionFunctions)
        {
            {
                ThrowExceptionIfInnerExceptionDepthLimitParameterOutOfRange(nameof(innerExceptionDepthLimit), innerExceptionDepthLimit);
                this.innerExceptionDepthLimit = innerExceptionDepthLimit;
            }
        }

        /// <summary>
        /// Adds a conversion function to the converter.
        /// </summary>
        /// <param name="exceptionType">The type (assignable to <see cref="Exception"/>) that the conversion function converts.</param>
        /// <param name="conversionFunction">The conversion function.  Accepts an Exception object and returns a <see cref="HttpErrorResponse"/>.</param>
        /// <remarks>Note that the conversion function should not handle the exception's 'InnerException' property, nor assign to the HttpErrorResponse's 'InnerError' property.</remarks>
        public void AddConversionFunction(Type exceptionType, Func<Exception, HttpErrorResponse> conversionFunction)
        {
            if (typeof(Exception).IsAssignableFrom(exceptionType) == false)
                throw new ArgumentException($"Type '{exceptionType.FullName}' specified in parameter '{nameof(exceptionType)}' is not assignable to '{typeof(Exception).FullName}'.", nameof(exceptionType));

            if (typeToConversionFunctionMap.ContainsKey(exceptionType) == true)
            {
                typeToConversionFunctionMap.Remove(exceptionType);
            }
            typeToConversionFunctionMap.Add(exceptionType, conversionFunction);
        }

        /// <summary>
        /// Adds handling for an exception to the converter, using a default conversion function (one which populates just the <see cref="HttpErrorResponse.Code"/>, <see cref="HttpErrorResponse.Message"/> and optionally <see cref="HttpErrorResponse.Target"/> properties).
        /// </summary>
        /// <param name="exceptionType">The type (assignable to <see cref="Exception"/>) to convert.</param>
        public void AddConversionFunction(Type exceptionType)
        {
            Func<Exception, HttpErrorResponse> conversionFunction = (Exception exception) =>
            {
                if (exception.TargetSite == null)
                {
                    return new HttpErrorResponse(exception.GetType().Name, exception.Message);
                }
                else
                {
                    return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name);
                }
            };
            AddConversionFunction(exceptionType, conversionFunction);
        }

        /// <summary>
        /// Converts the specified exception to an HttpErrorResponse.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <returns>The exception converted to an HttpErrorResponse.</returns>
        public HttpErrorResponse Convert(Exception exception)
        {
            return ConvertExceptionRecurse(exception, 0);
        }

        /// <summary>
        /// Initialises the specified type to conversion function map with conversion functions for many of the common .NET exceptions.
        /// </summary>
        /// <param name="typeToConversionFunctionMap">The type to conversion map to initialise.</param>
        protected void InitialiseTypeToConversionFunctionMap(Dictionary<Type, Func<Exception, HttpErrorResponse>> typeToConversionFunctionMap)
        {
            typeToConversionFunctionMap.Add
            (
                typeof(Exception),
                (Exception exception) =>
                {
                    if (exception.TargetSite == null)
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message);
                    }
                    else
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name);
                    }
                }
            );
            typeToConversionFunctionMap.Add
            (
                typeof(ArgumentException),
                (Exception exception) =>
                {
                    var argumentException = (ArgumentException)exception;
                    var attributes = new List<Tuple<String, String>>()
                    {
                        new Tuple<String, String>("ParameterName", $"{argumentException.ParamName}")
                    };
                    if (exception.TargetSite == null)
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, attributes);
                    }
                    else
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name, attributes);
                    }
                }
            );
            typeToConversionFunctionMap.Add
            (
                typeof(ArgumentOutOfRangeException),
                (Exception exception) =>
                {
                    var argumentOutOfRangeException = (ArgumentOutOfRangeException)exception;
                    var attributes = new List<Tuple<String, String>>()
                    {
                        new Tuple<String, String>("ParameterName", $"{argumentOutOfRangeException.ParamName}")
                    };
                    if (exception.TargetSite == null)
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, attributes);
                    }
                    else
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name, attributes);
                    }
                }
            );
            typeToConversionFunctionMap.Add
            (
                typeof(ArgumentNullException),
                (Exception exception) =>
                {
                    var argumentNullException = (ArgumentNullException)exception;
                    var attributes = new List<Tuple<String, String>>()
                    {
                        new Tuple<String, String>("ParameterName", $"{argumentNullException.ParamName}")
                    };
                    if (exception.TargetSite == null)
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, attributes);
                    }
                    else
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name, attributes);
                    }
                }
            );
            typeToConversionFunctionMap.Add
            (
                typeof(IndexOutOfRangeException),
                (Exception exception) =>
                {
                    if (exception.TargetSite == null)
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message);
                    }
                    else
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name);
                    }
                }
            );
            typeToConversionFunctionMap.Add
            (
                typeof(AggregateException),
                (Exception exception) =>
                {
                    var aggregateException = (AggregateException)exception;
                    // Convert the inner exceptions
                    Int32 innerExceptionNumber = 1;
                    var innerExceptionDetails = new List<Tuple<String, String>>();
                    foreach (Exception currentInnerException in aggregateException.InnerExceptions)
                    {
                        innerExceptionDetails.Add(new Tuple<String, String>($"InnerException{innerExceptionNumber}Code", currentInnerException.GetType().Name));
                        innerExceptionDetails.Add(new Tuple<String, String>($"InnerException{innerExceptionNumber}Message", currentInnerException.Message));
                        innerExceptionNumber++;
                    }
                    if (exception.TargetSite == null)
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, innerExceptionDetails);
                    }
                    else
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name, innerExceptionDetails);
                    }
                }
            );
            typeToConversionFunctionMap.Add
            (
                typeof(NotFoundException),
                (Exception exception) =>
                {
                    var notFoundException = (NotFoundException)exception;
                    var attributes = new List<Tuple<String, String>>()
                    {
                        new Tuple<String, String>(nameof(notFoundException.ResourceId), $"{notFoundException.ResourceId}")
                    };
                    if (exception.TargetSite == null)
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, attributes);
                    }
                    else
                    {
                        return new HttpErrorResponse(exception.GetType().Name, exception.Message, exception.TargetSite.Name, attributes);
                    }
                }
            );
        }

        /// <summary>
        /// Converts the specified exception to an HttpErrorResponse, recursively converting any inner exceptions.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <param name="currentInnerExceptionDepth">The current depth of inner exceptions.</param>
        /// <returns>The converted exception.</returns>
        protected HttpErrorResponse ConvertExceptionRecurse(Exception exception, Int32 currentInnerExceptionDepth)
        {
            // Recursively call for any inner exceptions
            HttpErrorResponse innerError = null;
            if (currentInnerExceptionDepth < innerExceptionDepthLimit)
            {
                if (exception.InnerException != null)
                {
                    innerError = ConvertExceptionRecurse(exception.InnerException, currentInnerExceptionDepth + 1);
                }
            }

            // Convert the exception using a matching function from the type to conversion function map
            HttpErrorResponse returnError = ConvertException(exception);
            if (innerError != null)
            {
                returnError.InnerError = innerError;
            }

            return returnError;
        }

        /// <summary>
        /// Converts an individual exception (i.e. not its inner exception hierarchy) to an HttpErrorResponse using a function from the type to conversion function map.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <returns>The converted exception.</returns>
        protected HttpErrorResponse ConvertException(Exception exception)
        {
            var currentType = exception.GetType();
            while (currentType != null)
            {
                if (typeToConversionFunctionMap.ContainsKey(currentType) == true)
                {
                    return typeToConversionFunctionMap[currentType].Invoke(exception);
                }
                else
                {
                    currentType = currentType.BaseType;
                }
            }
            throw new Exception($"No valid conversion function defined for exception type '{exception.GetType().FullName}'.");
        }

        protected void ThrowExceptionIfInnerExceptionDepthLimitParameterOutOfRange(String parameterName, Int32 parameterValue)
        {
            if (parameterValue < 0)
                throw new ArgumentOutOfRangeException(parameterName, $"Parameter '{parameterName}' with value {parameterValue} cannot be less than 0.");
        }
    }
}
