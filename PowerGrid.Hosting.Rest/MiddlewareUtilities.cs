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
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Builder;
using PowerGrid.Hosting.Rest.Models.Options;

namespace PowerGrid.Hosting.Rest
{
    public class MiddlewareUtilities
    {
        /// <summary>
        /// Sets up a custom exception handler in the specified application builder, which catches any thrown exceptions and converts them to and returns serialized <see cref="HttpErrorResponse"/> objects.
        /// </summary>
        /// <param name="appBuilder">A class which allows configuration of the application's request pipeline.</param>
        /// <param name="errorHandlingOptions">A set of application error handling options.</param>
        /// <param name="exceptionToHttpStatusCodeConverter">Used to convert types of exceptions to HTTP status codes.</param>
        /// <param name="exceptionToHttpErrorResponseConverter">Used to convert types of exceptions to <see cref="HttpErrorResponse"/> instances.</param>
        public void SetupExceptionHandler
        (
            IApplicationBuilder appBuilder,
            ErrorHandlingOptions errorHandlingOptions,
            ExceptionToHttpStatusCodeConverter exceptionToHttpStatusCodeConverter,
            ExceptionToHttpErrorResponseConverter exceptionToHttpErrorResponseConverter
        )
        {
            // As per https://docs.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-5.0#exception-handler-lambda
            appBuilder.UseExceptionHandler((IApplicationBuilder appBuilder) =>
            {
                appBuilder.Run(async (HttpContext context) =>
                {
                    // Get the exception
                    var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                    Exception exception = exceptionHandlerPathFeature.Error;

                    if (exception != null)
                    {
                        context.Response.ContentType = MediaTypeNames.Application.Json;
                        context.Response.StatusCode = (Int32)exceptionToHttpStatusCodeConverter.Convert(exception);
                        HttpErrorResponse httpErrorResponse = null;
                        if (context.Response.StatusCode == StatusCodes.Status500InternalServerError && errorHandlingOptions.OverrideInternalServerErrors.Value == true)
                        {
                            httpErrorResponse = new HttpErrorResponse("InternalServerError", errorHandlingOptions.InternalServerErrorMessageOverride);
                        }
                        else
                        {
                            httpErrorResponse = exceptionToHttpErrorResponseConverter.Convert(exception);
                        }
                        var serializer = new HttpErrorResponseJsonSerializer();
                        await context.Response.WriteAsync(serializer.Serialize(httpErrorResponse).ToString());
                    }
                    else
                    {
                        // TODO: Not sure if this situation can arise, but will leave this handler in while testing
                        throw new Exception("'exceptionHandlerPathFeature.Error' was null whilst handling exception.");
                    }
                });
            });
        }
    }
}
