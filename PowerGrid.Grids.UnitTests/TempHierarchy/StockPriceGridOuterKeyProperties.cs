using System;
using PowerGrid.Grids;

namespace PowerGrid.Core.UnitTests.TempHierarchy
{
    public class StockPriceGridOuterKeyProperties : GridCommonKeyProperties, IGridOuterKeyProperties<StockPrice>
    {
        public String DataSource { get; protected set; }
        public DateOnly Date { get; protected set; }

        public StockPriceGridOuterKeyProperties(String tag, String dataSource, DateOnly date)
            : base(tag)
        {
            if (String.IsNullOrWhiteSpace(dataSource) == true)
                throw new ArgumentException($"Parameter '{nameof(dataSource)}' must contain a value.", nameof(dataSource));

            DataSource = dataSource;
            Date = date;
        }
    }
}
