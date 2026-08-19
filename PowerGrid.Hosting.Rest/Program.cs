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
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using PowerGrid.Hosting.Rest.Models.Options;
using PowerGrid.Persistence;
using PowerGrid.Persistence.SqlServer;
using ApplicationLogging.Adapters.MicrosoftLoggingExtensions;

namespace PowerGrid.Hosting.Rest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddOptions<DatabaseConnectionOptions>()
                .Bind(builder.Configuration.GetSection(DatabaseConnectionOptions.DatabaseConnectionOptionsName))
                .ValidateDataAnnotations().ValidateOnStart();
            builder.Services.AddSingleton<PersistenceConcurrencyManager>(new PersistenceConcurrencyManager());
            DatabaseConnectionOptions databaseConnectionOptions = builder.Configuration.GetSection(DatabaseConnectionOptions.DatabaseConnectionOptionsName).Get<DatabaseConnectionOptions>();
            String connectionString = databaseConnectionOptions.ConnectionString;
            Int32 retryCount = databaseConnectionOptions.RetryCount;
            Int32 retryInterval = databaseConnectionOptions.RetryInterval;
            Int32 operationTimeout = databaseConnectionOptions.OperationTimeout;
            ILogger stockPricePersisterLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<StockPricePersister>>();
            builder.Services.AddSingleton<StockPricePersister>
            (
                new StockPricePersister(connectionString, retryCount, retryInterval, operationTimeout, new ApplicationLoggingMicrosoftLoggingExtensionsAdapter(stockPricePersisterLogger))
            );

            builder.Services.AddControllers();

            // Allow APIs to be versioned
            builder.Services.SetupApiVersioning();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen((SwaggerGenOptions swaggerGenOptions) =>
            {
                swaggerGenOptions.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "PowerGrid",
                    Description = "Persists grids of data to a database, with advanced storage features"
                });

                // Group endpoint routes together in the swagger page by their 'ApiExplorerSettings' > 'GroupName' attribute
                //   This allows endpoints defined across multiple files too all appear in the same group
                //   e.g. as occurs for endpoints in the 'UserEventProcessorControllerBase' and 'AddPrimaryUserEventProcessorControllerBase' controller classes
                swaggerGenOptions.TagActionsBy((ApiDescription apiDescription) =>
                {
                    if (apiDescription.GroupName != null)
                    {
                        return new List<String> { apiDescription.GroupName };
                    }
                    else
                    {
                        var controllerActionDescriptor = (ControllerActionDescriptor)apiDescription.ActionDescriptor;
                        if (controllerActionDescriptor != null)
                        {
                            throw new Exception($"'{nameof(apiDescription.GroupName)}' could not be found for controller '{controllerActionDescriptor.ControllerName}'.");
                        }
                        else
                        {
                            throw new Exception($"'{nameof(apiDescription.GroupName)}' could not be found for controller.");
                        }
                    }
                });
                // Omitting this causes only the first encountered 'ApiExplorerSettings' > 'GroupName' attribute to render in swagger
                //   Including it renders groups defined for all controllers
                swaggerGenOptions.DocInclusionPredicate((name, api) => { return true; });

                // Add XML comments to swagger
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                swaggerGenOptions.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });

            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            // Setup the Swagger UI
            app.SetupSwaggerUI(true);

            // Setup custom exception handler in the application's pipeline, so that any exceptions are caught and returned from the API as HttpErrorResponse objects
            var errorHandlingOptions = new ErrorHandlingOptions();
            app.Configuration.GetSection(ErrorHandlingOptions.ErrorHandlingOptionsName).Bind(errorHandlingOptions);
            var exceptionToHttpStatusCodeConverter = new ExceptionToHttpStatusCodeConverter();
            ExceptionToHttpErrorResponseConverter exceptionToHttpErrorResponseConverter = null;
            if (errorHandlingOptions.IncludeInnerExceptions.Value == true)
            {
                exceptionToHttpErrorResponseConverter = new ExceptionToHttpErrorResponseConverter();
            }
            else
            {
                exceptionToHttpErrorResponseConverter = new ExceptionToHttpErrorResponseConverter(0);
            }
            var middlewareUtilities = new MiddlewareUtilities();
            middlewareUtilities.SetupExceptionHandler(app, errorHandlingOptions, exceptionToHttpStatusCodeConverter, exceptionToHttpErrorResponseConverter);

            app.MapControllers();

            app.Run();
        }
    }
}
