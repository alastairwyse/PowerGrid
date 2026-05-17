using System;

namespace PowerGrid.Core.UnitTests.TempHierarchy
{
    public class GridCommonKeyProperties : IGridCommonKeyProperties
    {
        public String Tag { get; protected set; }

        public GridCommonKeyProperties(String tag)
        {
            if (String.IsNullOrWhiteSpace(tag) == true)
                throw new ArgumentException($"Parameter '{nameof(tag)}' must contain a value.", nameof(tag));

            Tag = tag;
        }
    }
}
