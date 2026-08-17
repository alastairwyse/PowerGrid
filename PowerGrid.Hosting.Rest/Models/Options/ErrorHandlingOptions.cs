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
using System.ComponentModel.DataAnnotations;

namespace PowerGrid.Hosting.Rest.Models.Options
{
    /// <summary>
    /// Container class storing options for error handling, and following the <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0">ASP.NET Core Options pattern</see>.
    /// </summary>
    public class ErrorHandlingOptions
    {
        #pragma warning disable 0649

        public const String ErrorHandlingOptionsName = "ErrorHandling";

        protected const String ValidationErrorMessagePrefix = $"Error validating {ErrorHandlingOptionsName} options";

        [Required(ErrorMessage = $"{ValidationErrorMessagePrefix}.  Configuration for '{nameof(IncludeInnerExceptions)}' is required.")]
        public Boolean? IncludeInnerExceptions { get; set; }

        [Required(ErrorMessage = $"{ValidationErrorMessagePrefix}.  Configuration for '{nameof(OverrideInternalServerErrors)}' is required.")]
        public Boolean? OverrideInternalServerErrors { get; set; }

        [Required(ErrorMessage = $"{ValidationErrorMessagePrefix}.  Configuration for '{nameof(InternalServerErrorMessageOverride)}' is required.")]
        public String? InternalServerErrorMessageOverride { get; set; }

        public ErrorHandlingOptions()
        {
        }

        #pragma warning restore 0649
    }
}
