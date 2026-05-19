using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence.SqlServer;

namespace PowerGrid.TestHarness
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TestPersistGrid();
        }

        static void TestPersistGrid()
        {
            const String bloombergDataSource = "Bloomberg";
            const String refinitivDataSource = "Refinitiv";
            const String canonCompany = "Canon";
            const String hitachiCompany = "Hitachi";
            const String sonyCompany = "Sony";
            const String toyotaCompany = "Toyota";

            TestUtilities utils = new();

            List<StockPrice> testGridItems = new()
            {
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4447),
                //new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4739),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3220),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 3261),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), "Kamispring", 10000000002)
            };
            String connectionString = File.ReadAllText(@"..\..\..\..\Documentation\TempConnectionString.txt");

            StockPricePersister persister = new StockPricePersister(connectionString, 5, 5, 0);
            persister.PersistGrid(testGridItems);
            //persister.TestGetUpdate2Connections("Reuters");
        }
    }
}
