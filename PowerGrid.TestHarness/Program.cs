using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer;
using ApplicationLogging;

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
                //new StockPrice(canonCompany, 4448),
                new StockPrice(hitachiCompany, 4738),
                new StockPrice(sonyCompany, 3221),
                new StockPrice(toyotaCompany, 3265),
                new StockPrice("Kamispring", 10000000003)
            };
            String connectionString = File.ReadAllText(@"..\..\..\..\Documentation\TempConnectionString.txt");
            ConsoleApplicationLogger consoleLogger = new(LogLevel.Debug, '|', "  ");
            StockPricePersister persister = new StockPricePersister(connectionString, 5, 5, 0, consoleLogger);
            StockPriceOuterKeyProperties outerKeyProps = new("Test", bloombergDataSource, utils.CreateDateOnlyFromString("2026-05-29"));
            /*
            persister.PersistGrid(outerKeyProps, testGridItems);
            outerKeyProps = new("Test2", bloombergDataSource, utils.CreateDateOnlyFromString("2026-05-29"));
            Console.WriteLine("-- GetGrid() Results --");
            foreach (StockPriceGridItemPTO currentPTO in persister.GetGrid(outerKeyProps, 2))
            {
                Console.WriteLine(currentPTO);
            }
            IList<GridVersionAndTransactionTimestamp> gridDetails = persister.GetGridDetails(outerKeyProps);
            foreach (GridVersionAndTransactionTimestamp currentGridDetails in gridDetails)
            {
                Console.WriteLine($"{currentGridDetails.Version}, {currentGridDetails.TransactionTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")}");
            }
            
            GridCommonKeyProperties commonKeyProperties = new("Test");
            IList<Tuple<StockPriceOuterKeyProperties, GridVersionAndTransactionTimestamp>> gridDetails = persister.GetGridDetails(commonKeyProperties);
            foreach (Tuple<StockPriceOuterKeyProperties, GridVersionAndTransactionTimestamp> currentResult in gridDetails)
            {
                Console.WriteLine($"{currentResult.Item1.ToString()}, {currentResult.Item2.Version}, {currentResult.Item2.TransactionTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")}");
            }
            */

            persister.SoftDeleteLatestGrid(outerKeyProps);
        }
    }
}
