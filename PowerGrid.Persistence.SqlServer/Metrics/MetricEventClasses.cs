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
using ApplicationMetrics;

namespace PowerGrid.Persistence.SqlServer.Metrics
{
    #pragma warning disable 1591

    /// <summary>
    /// Count metric which records execution of a SQL command resulting in a transient error and being retried.
    /// </summary>
    public class SqlCommandExecutionRetried : CountMetric
    {
        protected static String staticName = "SqlCommandExecutionRetried";
        protected static String staticDescription = "Execution of a SQL command resulting in a transient error and being retried.";

        public SqlCommandExecutionRetried()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    #pragma warning restore 1591
}
