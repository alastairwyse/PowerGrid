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
using PowerGrid.Core;
using ApplicationLogging;
using ApplicationMetrics;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Base for classes which write in bulk and read grids to and from Microsoft SQL Server databases.
    /// </summary>
    /// <typeparam name="TEntity">The type of data held in each item in the grid.</typeparam>
    /// <typeparam name="TOuterKeyProperties">The <see cref="IGridOuterKeyProperties">outer key properties</see> of the items in the grid.</typeparam>
    /// <typeparam name="TGridItem">The items in the grid (i.e. where each item includes the <see cref="IGridOuterKeyProperties">outer key properties</see>).</typeparam>
    /// <typeparam name="TGridItemPTO">The <see cref="IPersistenceTransferObject">persistence transfer object</see> equivalent of <see cref="TGridItem"/>.</typeparam>
    public abstract class BulkPersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO> : PersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TEntity : IGridItem<TEntity>
        where TCommonKeyProperties : Core.IGridCommonKeyProperties
        where TOuterKeyProperties : IGridOuterKeyProperties
        where TGridItem : TEntity, IGridOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridOuterKeyProperties, TGridItem, IGridItem<TGridItem>, IPersistenceTransferObject
    {

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.BulkPersisterBase class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        public BulkPersisterBase
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger
        )
            : base(connectionString, retryCount, retryInterval, operationTimeout, logger)
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.BulkPersisterBase class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public BulkPersisterBase
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger, 
            IMetricLogger metricLogger
        )
            : base(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger)
        {
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.BulkPersisterBase class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="dateTimeProvider">A mock <see cref="IDateTimeProvider"/></param>
        /// <param name="sqlConnectionShim">A mock <see cref="ISqlConnectionShim"/>.</param>
        /// <param name="sqlTransactionShim">A mock <see cref="ISqlTransactionShim"/>.</param>
        /// <param name="sqlCommandShim">A mock <see cref="ISqlCommandShim"/>.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public BulkPersisterBase
        (
            String connectionString,
            Int32 retryCount,
            Int32 retryInterval,
            Int32 operationTimeout,
            IApplicationLogger logger,
            IMetricLogger metricLogger,
            IDateTimeProvider dateTimeProvider,
            ISqlConnectionShim sqlConnectionShim,
            ISqlTransactionShim sqlTransactionShim,
            ISqlCommandShim sqlCommandShim
        ) : base(connectionString, retryCount, retryInterval, operationTimeout, logger, metricLogger, dateTimeProvider, sqlConnectionShim, sqlTransactionShim, sqlCommandShim)
        {
        }
    }
}
