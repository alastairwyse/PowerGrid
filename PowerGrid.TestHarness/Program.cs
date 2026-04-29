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
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), sonyCompany, 3211),
                new StockPrice(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), toyotaCompany, 3255)
            };
            String connectionString = File.ReadAllText(@"..\..\..\..\Documentation\TempConnectionString.txt");

            StockPricePersister persister = new StockPricePersister(connectionString, 5, 5, 0);
            persister.PersistGrid(testGridItems);
            //persister.TestGetUpdate2Connections("Reuters");
        }
    }
}
