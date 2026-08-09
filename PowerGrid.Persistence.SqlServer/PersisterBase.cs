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

using ApplicationLogging;
using ApplicationMetrics;
using ApplicationMetrics.MetricLoggers;
using PowerGrid.Core;
using PowerGrid.Persistence;
using System;
using System.Collections.Generic;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Base for classes which write and read grids to and from Microsoft SQL Server databases.
    /// </summary>
    /// <typeparam name="TEntity">The type of data held in each item in the grid.</typeparam>
    /// <typeparam name="TOuterKeyProperties">The <see cref="IGridOuterKeyProperties">outer key properties</see> of the items in the grid.</typeparam>
    /// <typeparam name="TGridItem">The items in the grid (i.e. where each item includes the <see cref="IGridOuterKeyProperties">outer key properties</see>).</typeparam>
    /// <typeparam name="TGridItemPTO">The <see cref="IPersistenceTransferObject">persistence transfer object</see> equivalent of <see cref="TGridItem"/>.</typeparam>
    public abstract class PersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO> : Persistence.PersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TEntity : IGridItem<TEntity>
        where TCommonKeyProperties : Core.IGridCommonKeyProperties
        where TOuterKeyProperties : IGridOuterKeyProperties
        where TGridItem : TEntity, IGridOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridOuterKeyProperties, IGridItem<TGridItem>, IPersistenceTransferObject
    {
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 23 date and time style</see>.</summary>
        protected const String transactSql23DateStyle = "yyyy-MM-dd";
        /// <summary>DateTime format string which matches the <see href="https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver16#date-and-time-styles">Transact-SQL 126 date and time style</see>.</summary>
        protected const String transactSql126DateStyle = "yyyy-MM-ddTHH:mm:ss.fffffff";
        /// <summary>The type of collation to use when ordering results returned from SQL Server.</summary>
        protected const String transactSqlCollation = "Latin1_General_BIN2";
        /// <summary>The maximum possible <see cref="DateTime"/> value to use as the upper bound for validity period in the persisted temporal model.</summary>
        protected readonly DateTime temporalMaximumDateTime = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
        /// </summary>
        /// <param name="logger">The logger for general logging.</param>
        public PersisterBase(IApplicationLogger logger)
            : base(logger)    
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
        /// </summary>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public PersisterBase(IApplicationLogger logger, IMetricLogger metricLogger)
            : base(logger, metricLogger)
        {
        }

    }
}
