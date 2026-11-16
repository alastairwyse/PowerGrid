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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using PowerGrid.Core;
using PowerGrid.Persistence.Metrics;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.SqlServer.Metrics;
using ApplicationLogging;
using ApplicationMetrics;

namespace PowerGrid.Persistence.SqlServer
{
    /// <summary>
    /// Base for classes which write and read grids to and from Microsoft SQL Server databases item by item (i.e. not in bulk).
    /// </summary>
    /// <typeparam name="TEntity">The type of data held in each item in the grid.</typeparam>
    /// <typeparam name="TOuterKeyProperties">The <see cref="IGridOuterKeyProperties">outer key properties</see> of the items in the grid.</typeparam>
    /// <typeparam name="TGridItem">The items in the grid (i.e. where each item includes the <see cref="IGridOuterKeyProperties">outer key properties</see>).</typeparam>
    /// <typeparam name="TGridItemPTO">The <see cref="IPersistenceTransferObject">persistence transfer object</see> equivalent of <see cref="TGridItem"/>.</typeparam>
    public abstract class PersisterBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO> : PersisterCommonBase<TEntity, TCommonKeyProperties, TOuterKeyProperties, TGridItem, TGridItemPTO>
        where TEntity : IGridItem<TEntity>
        where TCommonKeyProperties : Core.IGridCommonKeyProperties
        where TOuterKeyProperties : IGridOuterKeyProperties
        where TGridItem : TEntity, IGridOuterKeyProperties, IGridItem<TGridItem>
        where TGridItemPTO : IGridOuterKeyProperties, TGridItem, IGridItem<TGridItem>, IPersistenceTransferObject
    {

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        public PersisterBase
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
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
        /// </summary>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">">The time in seconds between operation retries.</param>
        /// <param name="operationTimeout">The timeout in seconds before terminating an operation against the SQL Server database.  A value of 0 indicates no limit.</param>
        /// <param name="logger">The logger for general logging.</param>
        /// <param name="metricLogger">The logger for metrics.</param>
        public PersisterBase
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
        /// Initialises a new instance of the PowerGrid.Persistence.SqlServer.PersisterBase class.
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
        public PersisterBase
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

        /// <inheritdoc/>
        public override (Int32 Version, GridComparisonStatistics GridComparisonStatistics) PersistGrid(TOuterKeyProperties gridOuterKeyProperties, IList<TEntity> items, PersistComparisonStrategy persistComparisonStrategy = PersistComparisonStrategy.Syncronise)
        {
            if (items.Count == 0)
                throw new ArgumentException($"Parameter '{nameof(items)}' contained no items.", nameof(items));

            using (var readConnection = new SqlConnection(connectionString))
            using (var writeConnection = new SqlConnection(connectionString))
            {
                Int32 gridVersion;
                GridComparisonStatistics comparisonStatistics;
                try
                {
                    PrepareConnection(readConnection);
                    sqlConnectionShim.Open(writeConnection);
                    PrepareConnection(writeConnection, SessionDeadlockPriority.High);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to connect to SQL Server.", e);
                }

                DateTime transactionTimestamp = dateTimeProvider.UtcNow();
                using (SqlTransaction transaction = sqlConnectionShim.BeginTransaction(writeConnection))
                {
                    Action<SqlConnection, SqlTransaction, TGridItem, DateTime> addedItemEmitterOperationAction = (SqlConnection connection, SqlTransaction transaction, TGridItem addedGridItem, DateTime transactionDateTime) =>
                    {
                        InsertGridItem(connection, transaction, addedGridItem, transactionDateTime);
                    };
                    DataBaseOperationEmitter<TGridItem> addedItemEmitter = new(writeConnection, transaction, transactionTimestamp, addedItemEmitterOperationAction);
                    Action<SqlConnection, SqlTransaction, Tuple<TGridItemPTO, TGridItem>, DateTime> updatedItemsEmitterOperationAction = (SqlConnection connection, SqlTransaction transaction, Tuple<TGridItemPTO, TGridItem> updatedGridItems, DateTime transactionDateTime) =>
                    {
                        UpdateGridItem(connection, transaction, updatedGridItems.Item1, updatedGridItems.Item2, transactionDateTime);
                    };
                    DataBaseOperationEmitter<Tuple<TGridItemPTO, TGridItem>> updatedItemsEmitter = new(writeConnection, transaction, transactionTimestamp, updatedItemsEmitterOperationAction);
                    Action<SqlConnection, SqlTransaction, TGridItemPTO, DateTime> deletedItemEmitterOperationAction = (SqlConnection connection, SqlTransaction transaction, TGridItemPTO deletedGridItem, DateTime transactionDateTime) =>
                    {
                        DeleteGridItem(connection, transaction, deletedGridItem, transactionDateTime);
                    };
                    DataBaseOperationEmitter<TGridItemPTO> deletedItemEmitter = new(writeConnection, transaction, transactionTimestamp, deletedItemEmitterOperationAction);
                    GridComparer<TGridItemPTO, TGridItem> gridComparer;
                    if (persistComparisonStrategy == PersistComparisonStrategy.AddAndUpdateOnly)
                    {
                        gridComparer = new(addedItemEmitter, updatedItemsEmitter, new NullEmitter<TGridItemPTO>());
                    }
                    else
                    {
                        gridComparer = new(addedItemEmitter, updatedItemsEmitter, deletedItemEmitter);
                    }

                    // Setup IEnumerable 'chains' 
                    GridContentsValidator<TEntity> newItemValidator = new();
                    GridContentsDuplicateChecker<TEntity> newItemsDuplicateChecker = new();
                    // Order of below chain is 1 validate, 2 order, 3 dup check
                    IEnumerable<TEntity> newGridContents = newItemsDuplicateChecker.CheckForDuplicates
                    (
                        newItemValidator.ValidateItems
                        (
                            items,
                            NewEntityValidationAction
                        ).Order(Comparer<TEntity>.Create
                        (
                            (TEntity first, TEntity second) => { return first.KeyCompareTo(second); }
                        ))
                    );
                    IEnumerable<TGridItem> ConvertEntitiesToGridItems(IEnumerable<TEntity> items)
                    {
                        foreach (TEntity currentItem in items)
                        {
                            yield return ConvertEntityAndOuterKeyPropertiesToGridItem(gridOuterKeyProperties, currentItem);
                        }
                    }

                    Guid beginId = metricLogger.Begin(new GridPersistTime());
                    try
                    {
                        sqlConnectionShim.Open(readConnection);
                        IEnumerable<TGridItemPTO> existingGridContents;
                        try
                        {
                            existingGridContents = GetGrid(readConnection, gridOuterKeyProperties, transactionTimestamp);
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"Failed to read existing {GridItemEntityName} grid from SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactSql126DateStyle)}'.", e);
                        }
                        {
                            try
                            {
                                comparisonStatistics = gridComparer.Compare(existingGridContents, ConvertEntitiesToGridItems(newGridContents));
                            }
                            catch (Exception e)
                            {
                                Exception compareException = new($"Failed to compare new {GridItemEntityName} grid to existing grid in SQL Server for {gridOuterKeyProperties.ToString()}, and transaction time '{transactionTimestamp.ToString(transactSql126DateStyle)}'.", e);
                                try
                                {
                                    // As per https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqltransaction.rollback?view=sqlclient-dotnet-core-6.1, exception can occur on rollback
                                    sqlTransactionShim.Rollback(transaction);
                                }
                                catch (Exception rollbackException)
                                {
                                    throw new AggregateException($"Failed to rollback transaction after exception comparing {GridItemEntityName} grid to existing data.", rollbackException, compareException);
                                }
                                throw compareException;
                            }
                        }
                        gridVersion = CreateGrid(readConnection, writeConnection, transaction, gridOuterKeyProperties, transactionTimestamp);
                        sqlTransactionShim.Commit(transaction);

                        sqlConnectionShim.Close(writeConnection);
                        sqlConnectionShim.Close(readConnection);
                    }
                    catch (Exception e)
                    {
                        metricLogger.CancelBegin(beginId, new GridPersistTime());
                        throw new Exception("Failed to persist grid to SQL Server.", e);
                    }
                    metricLogger.End(beginId, new GridPersistTime());
                    metricLogger.Increment(new GridPersisted());
                }
                TeardownConnection(readConnection);
                TeardownConnection(writeConnection);
                if (persistComparisonStrategy == PersistComparisonStrategy.AddAndUpdateOnly)
                {
                    comparisonStatistics = new GridComparisonStatistics(comparisonStatistics.ItemsAddedCount, comparisonStatistics.ItemsUpdatedCount, 0);
                }
                metricLogger.Add(new ComparedGridItemsAdded(), comparisonStatistics.ItemsAddedCount);
                metricLogger.Add(new ComparedGridItemsUpdated(), comparisonStatistics.ItemsUpdatedCount);
                metricLogger.Add(new ComparedGridItemsDeleted(), comparisonStatistics.ItemsDeletedCount);

                return (gridVersion, comparisonStatistics);
            }
        }
    }
}
