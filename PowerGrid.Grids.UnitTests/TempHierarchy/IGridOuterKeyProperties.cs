using System;

namespace PowerGrid.Core.UnitTests.TempHierarchy
{
    /// <summary>
    /// Defines key properties for a specific type of grid, excluding the key properties of the items within the grid.
    /// </summary>
    public interface IGridOuterKeyProperties<T> : IGridCommonKeyProperties where T : IGridItem<T>
    {
    }
}
