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
    /// Container class storing generic options for connecting to SQL Server databases, and following the <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0">ASP.NET Core Options pattern</see>.
    /// </summary>
    public class DatabaseConnectionOptions
    {
        #pragma warning disable 0649
        #pragma warning disable 8618

        public const String DatabaseConnectionOptionsName = "DatabaseConnection";

        protected const String ValidationErrorMessagePrefix = $"Error validating {DatabaseConnectionOptionsName} options";

        [Required(ErrorMessage = $"{ValidationErrorMessagePrefix}.  Configuration for '{nameof(ConnectionString)}' is required.")]
        public String ConnectionString { get; set; }

        [Required(ErrorMessage = $"{ValidationErrorMessagePrefix}.  Configuration for '{nameof(RetryCount)}' is required.")]
        [Range(0, 59, ErrorMessage = $"{ValidationErrorMessagePrefix}.  Value for '{nameof(RetryCount)}' must be between {{1}} and {{2}}.")]
        public Int32 RetryCount { get; set; }

        [Required(ErrorMessage = $"{ValidationErrorMessagePrefix}.  Configuration for '{nameof(RetryInterval)}' is required.")]
        [Range(0, 120, ErrorMessage = $"{ValidationErrorMessagePrefix}.  Value for '{nameof(RetryInterval)}' must be between {{1}} and {{2}}.")]
        public Int32 RetryInterval { get; set; }

        [Required(ErrorMessage = $"{ValidationErrorMessagePrefix}.  Configuration for '{nameof(OperationTimeout)}' is required.")]
        [Range(0, 2147483647, ErrorMessage = $"{ValidationErrorMessagePrefix}.  Value for '{nameof(OperationTimeout)}' must be between {{1}} and {{2}}.")]
        public Int32 OperationTimeout { get; set; }

        public DatabaseConnectionOptions()
        {
        }

        #pragma warning restore 8618
        #pragma warning restore 0649
    }
}
