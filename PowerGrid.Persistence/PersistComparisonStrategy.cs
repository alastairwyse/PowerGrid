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

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Defines methods of comparing a grid against an existing grid when writing the grid to persistent storage.
    /// </summary>
    public enum PersistComparisonStrategy
    {
        /// <summary>Items in the new grid which are not in the existing grid will be added.  Items in the new grid which are in the existing grid will be updated.  Items in the existing grid which are not in the new grid will be left unchanged.</summary>
        AddAndUpdateOnly,
        /// <summary>Results in the persisted grid exactly matching the new grid.  Items in the new grid which are not in the existing grid will be added.  Items in the new grid which are in the existing grid will be updated.  Items in the existing grid which are not in the new grid will deleted.</summary>
        Syncronise
    }
}
