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
    /// Defines key properties of grids that are specific to a particular type of grid (i.e. not common across all grids), but not key properties of the grid item itself.
    /// </summary>
    /// <remarks><see cref="IKeyPropertyComparable{T}">Key properties</see> refer to properties of items in a grid which collectively must be unique within a single grid.  An example of an outer key property for a grid item class representing the price of a stock might be the date/time the price was valid at.  By contrast an 'inner' key property would be the company the price was quoted for.</remarks>
    public interface IGridItemOuterKeyProperties
    {
    }
}
