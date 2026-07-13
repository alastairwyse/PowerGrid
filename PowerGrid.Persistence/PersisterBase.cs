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
using PowerGrid.Core;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using ApplicationLogging;
using ApplicationMetrics;
using ApplicationMetrics.MetricLoggers;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Base for classes which write and read grids to and from persistent storage.
    /// </summary>
    /// <typeparam name="TEntity">The type of data held in each item in the grid.</typeparam>
    /// <typeparam name="TOuterKeyProperties">The <see cref="IGridOuterKeyProperties">outer key properties</see> of the items in the grid.</typeparam>
    /// <typeparam name="TGridItem">The items in the grid (i.e. where each item includes the <see cref="IGridOuterKeyProperties">outer key properties</see>).</typeparam>
    /// <typeparam name="TGridItemPTO">The <see cref="IPersistenceTransferObject">persistence transfer object</see> equivalent of <see cref="TGridItem"/>.</typeparam>
    public abstract class PersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO> : IGridPersister<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TEntity : IGridItem<TEntity>
        where TCommonKeyProperties : Core.IGridCommonKeyProperties
        where TOuterKeyProperties : IGridOuterKeyProperties
        where TGridItem : TEntity, IGridOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridOuterKeyProperties, IGridItem<TGridItem>, IPersistenceTransferObject
    {
        /// <summary>The logger for general logging.</summary>
        protected IApplicationLogger logger;
        /// <summary>The logger for metrics.</summary>
        protected IMetricLogger metricLogger;

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.PersisterBase class.
        /// </summary>
        /// <param name="logger">The logger for general logging.</param>
        public PersisterBase(IApplicationLogger logger)
        {
            this.logger = logger;
            metricLogger = new NullMetricLogger();
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.PersisterBase class.
        /// </summary>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public PersisterBase(IApplicationLogger logger, IMetricLogger metricLogger)
            : this(logger)
        {
            this.metricLogger = metricLogger;
        }

        /// <inheritdoc/>
        public abstract (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(TOuterKeyProperties gridOuterKeyProperties, IList<TEntity> items);

        /// <inheritdoc/>
        public abstract IEnumerable<TGridItemPTO> GetGrid(TOuterKeyProperties gridOuterKeyProperties, Int32 version);

        /// <inheritdoc/>
        public abstract IList<GridVersionAndTransactionTimestamp> GetGridDetails(TOuterKeyProperties gridOuterKeyProperties);

        /// <inheritdoc/>
        public abstract IList<Tuple<TOuterKeyProperties, GridVersionAndTransactionTimestamp>> GetGridDetails(TCommonKeyProperties gridCommonKeyProperties);

        /// <inheritdoc/>
        public abstract void SoftDeleteLatestGrid(TOuterKeyProperties gridOuterKeyProperties);

        /// <inheritdoc/>
        public abstract void HardDeleteGrids(TOuterKeyProperties gridOuterKeyProperties);

        /// <inheritdoc/>
        public abstract void HardDeleteGrids(TCommonKeyProperties gridCommonKeyProperties);
    }
}
