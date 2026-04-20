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

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Defines values for the SQL Server session <see href="https://learn.microsoft.com/en-us/sql/t-sql/statements/set-deadlock-priority-transact-sql?view=sql-server-ver16">deadlock priority</see> setting.
    /// </summary>
    public enum SessionDeadlockPriority
    {
        /// <summary>Specifies that the current session will be more likely to be the deadlock victim (will be the victim in deadlocks where the other session has 'normal' or 'high' priority).</summary>
        Low,
        /// <summary>Specifies that the current session will be equally likely to be the deadlock victim (will be the victim in deadlocks where the other session has 'high' priority).</summary>
        Normal,
        /// <summary>Specifies that the current session will be less likely to be the deadlock victim (will only potentially be the victim in deadlocks where the other session also has 'high' priority).</summary>
        High
    }
}