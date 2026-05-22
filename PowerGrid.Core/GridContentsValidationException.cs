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

namespace PowerGrid.Core
{
    /// <summary>
    /// The exception that is thrown when an item in a grid fails validation.
    /// </summary>
    /// <typeparam name="T">The type of data stored in the grid.</typeparam>
    public class GridContentsValidationException<T> : Exception where T : IGridItem<T>
    {
        /// <summary>The item in the grid which failed to validate.</summary>
        protected T gridItem;

        /// <summary>
        /// The item in the grid which failed to validate.
        /// </summary>
        public T GridItem
        {
            get
            {
                return gridItem;
            }
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Core.GridContentsValidationException class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="gridItem">The item in the grid which failed to validate.</param>
        public GridContentsValidationException(String message, T gridItem)
            : base(message)
        {
            this.gridItem = gridItem;
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Core.GridContentsValidationException class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="gridItem">The item in the grid which failed to validate.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public GridContentsValidationException(String message, T gridItem, Exception innerException)
            : base(message, innerException)
        {
            this.gridItem = gridItem;
        }
    }
}
