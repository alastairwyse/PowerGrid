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

namespace PowerGrid.Persistence.Metrics
{
    #pragma warning disable 1591

    /// <summary>
    /// Interval metric which records the time taken to soft delete the latest grid defined by outer key properties from persistent storage.
    /// </summary>
    public class LatestGridSoftDeleteTime : IntervalMetric
    {
        protected static String staticName = "LatestGridSoftDeleteTime";
        protected static String staticDescription = "The time taken to soft delete the latest grid defined by outer key properties from persistent storage";

        public LatestGridSoftDeleteTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that the latest grid defined by outer key properties was soft deleted from persistent storage.
    /// </summary>
    public class LatestGridSoftDeleted : CountMetric
    {
        protected static String staticName = "LatestGridSoftDeleted";
        protected static String staticDescription = "The latest grid defined by outer key properties was soft deleted from persistent storage";

        public LatestGridSoftDeleted()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to hard delete a set of grids defined by outer key properties from persistent storage.
    /// </summary>
    public class GridsHardDeleteByOuterKeyPropertiesTime : IntervalMetric
    {
        protected static String staticName = "GridsHardDeleteByOuterKeyPropertiesTime";
        protected static String staticDescription = "The time taken to hard delete a set of grids defined by outer key properties from persistent storage";

        public GridsHardDeleteByOuterKeyPropertiesTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that a set of grids defined by outer key properties were deleted from persistent storage.
    /// </summary>
    public class GridsHardDeletedByOuterKeyProperties : CountMetric
    {
        protected static String staticName = "GridsHardDeletedByOuterKeyProperties";
        protected static String staticDescription = "A set of grids defined by outer key properties were deleted from persistent storage";

        public GridsHardDeletedByOuterKeyProperties()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to hard delete a set of grids defined by common key properties from persistent storage.
    /// </summary>
    public class GridsHardDeleteByCommonKeyPropertiesTime : IntervalMetric
    {
        protected static String staticName = "GridsHardDeleteByCommonKeyPropertiesTime";
        protected static String staticDescription = "The time taken to hard delete a set of grids defined by common key properties from persistent storage";

        public GridsHardDeleteByCommonKeyPropertiesTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that a set of grids defined by common key properties were deleted from persistent storage.
    /// </summary>
    public class GridsHardDeletedByCommonKeyProperties : CountMetric
    {
        protected static String staticName = "GridsHardDeletedByCommonKeyProperties";
        protected static String staticDescription = "A set of grids defined by common key properties were deleted from persistent storage";

        public GridsHardDeletedByCommonKeyProperties()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to retrieve the current maximum grid version and corresponding transaction timestamp from persistent storage.
    /// </summary>
    public class GridMaximumVersionAndTransactionTimestampRetrieveTime : IntervalMetric
    {
        protected static String staticName = "GridMaximumVersionAndTransactionTimestampRetrieveTime";
        protected static String staticDescription = "The time taken to retrieve the current maximum grid version and corresponding transaction timestamp from persistent storage";

        public GridMaximumVersionAndTransactionTimestampRetrieveTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that the current maximum grid version and corresponding transaction timestamp were retrieved from persistent storage.
    /// </summary>
    public class GridMaximumVersionAndTransactionTimestampRetrieved : CountMetric
    {
        protected static String staticName = "GridMaximumVersionAndTransactionTimestampRetrieved";
        protected static String staticDescription = "The current maximum grid version and corresponding transaction timestamp were retrieved from persistent storage";

        public GridMaximumVersionAndTransactionTimestampRetrieved()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to retrieve a grid transaction timestamp from persistent storage.
    /// </summary>
    public class GridTransactionTimestampRetrieveTime : IntervalMetric
    {
        protected static String staticName = "GridTransactionTimestampRetrieveTime";
        protected static String staticDescription = "The time taken to retrieve a grid transaction timestamp from persistent storage";

        public GridTransactionTimestampRetrieveTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that an item was inserted into a grid as part of a PersistGrid() operation.
    /// </summary>
    public class GridItemInserted : CountMetric
    {
        protected static String staticName = "GridItemInserted";
        protected static String staticDescription = "An item was inserted into a grid as part of a PersistGrid() operation";

        public GridItemInserted()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to insert an item into a grid as part of a PersistGrid() operation.
    /// </summary>
    public class GridItemInsertTime : IntervalMetric
    {
        protected static String staticName = "GridItemInsertTime";
        protected static String staticDescription = "The time taken to insert an item into a grid as part of a PersistGrid() operation";

        public GridItemInsertTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that an item was updated in a grid as part of a PersistGrid() operation.
    /// </summary>
    public class GridItemUpdated : CountMetric
    {
        protected static String staticName = "GridItemUpdated";
        protected static String staticDescription = "An item was updated in a grid as part of a PersistGrid() operation";

        public GridItemUpdated()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to update an item in a grid as part of a PersistGrid() operation.
    /// </summary>
    public class GridItemUpdateTime : IntervalMetric
    {
        protected static String staticName = "GridItemUpdateTime";
        protected static String staticDescription = "The time taken to update an item in a grid as part of a PersistGrid() operation";

        public GridItemUpdateTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that an item was deleted from a grid as part of a PersistGrid() operation.
    /// </summary>
    public class GridItemDeleted : CountMetric
    {
        protected static String staticName = "GridItemDeleted";
        protected static String staticDescription = "An item was deleted from a grid as part of a PersistGrid() operation";

        public GridItemDeleted()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to delete an item from a grid as part of a PersistGrid() operation.
    /// </summary>
    public class GridItemDeleteTime : IntervalMetric
    {
        protected static String staticName = "GridItemDeleteTime";
        protected static String staticDescription = "The time taken to delete an item from a grid as part of a PersistGrid() operation";

        public GridItemDeleteTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to retrieve the current maximum grid version from persistent storage.
    /// </summary>
    public class GridMaximumVersionRetrieveTime : IntervalMetric
    {
        protected static String staticName = "GridMaximumVersionRetrieveTime";
        protected static String staticDescription = "The time taken to retrieve the current maximum grid version from persistent storage";

        public GridMaximumVersionRetrieveTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which records the time taken to create a grid in persistent storage.
    /// </summary>
    public class GridCreateTime : IntervalMetric
    {
        protected static String staticName = "GridCreateTime";
        protected static String staticDescription = "The time taken to create a grid in persistent storage";

        public GridCreateTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which records that a grid was created in persistent storage.
    /// </summary>
    public class GridCreated : CountMetric
    {
        protected static String staticName = "GridCreated";
        protected static String staticDescription = "A grid was created in persistent storage";

        public GridCreated()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    #pragma warning restore 1591
}
