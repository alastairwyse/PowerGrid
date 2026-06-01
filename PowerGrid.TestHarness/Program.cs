using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
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
                new StockPrice(canonCompany, 4448),
                new StockPrice(hitachiCompany, 4740),
                new StockPrice(sonyCompany, 3221),
                //new StockPrice(toyotaCompany, 3261),
                new StockPrice("Kamispring", 10000000002)
            };
            String connectionString = File.ReadAllText(@"..\..\..\..\Documentation\TempConnectionString.txt");

            StockPricePersister persister = new StockPricePersister(connectionString, 5, 5, 0);
            StockPriceOuterKeyProperties outerKeyProps = new("Test2", bloombergDataSource, utils.CreateDateOnlyFromString("2026-05-29"));
            //persister.PersistGrid(outerKeyProps, testGridItems);
            //persister.TestGetUpdate2Connections("Reuters");

            outerKeyProps = new("Test2", bloombergDataSource, utils.CreateDateOnlyFromString("2026-05-29"));
            Console.WriteLine("-- GetGrid() Results --");
            foreach (StockPriceGridItemPTO currentPTO in persister.GetGrid(outerKeyProps, 1))
            {
                Console.WriteLine(currentPTO);
            }
        }
    }
}
