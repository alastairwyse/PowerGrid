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

namespace PowerGrid.Grids
{
    /// <summary>
    /// An <see cref="IGridLockKey"/> implementation for <see cref="GridCommonKeyProperties"/> instances.
    /// </summary>
    public class GridCommonKeyPropertiesLockKey : GridCommonKeyPropertiesLockKeyBase<GridCommonKeyProperties>
    {
        /// <inheritdoc/>
        protected override Object[] UnderlyingGridKeyPropertyValues
        {
            get
            {
                return new Object[1] { underlyingGridKeyProperties.Tag };
            }
        }

        /// <summary>
        /// Initialises a new instance of the PowerGrid.Grids.GridCommonKeyPropertiesLockKey class.
        /// </summary>
        /// <param name="underlyingStockPrice">The common key properties object to create a <see cref="Dictionary{TKey, TValue}"/> key for.</param>
        public GridCommonKeyPropertiesLockKey(GridCommonKeyProperties gridCommonKeyProperties)
            : base(gridCommonKeyProperties)
        {
        }
    }
}
