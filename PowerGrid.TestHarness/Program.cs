using System.Diagnostics;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence.Models;
using PowerGrid.Persistence.Models.PersistenceTransferObjects;
using PowerGrid.Persistence.SqlServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApplicationLogging;

namespace PowerGrid.TestHarness
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TestBulkApi();
        }

        static void TestBulkApi()
        {
            const Int32 itemCount = 500_000;
            //const Int32 itemCount = 100;
            const String gridOuterKeyPropertiesPropertyName = "stockPriceGridOuterKeyProperties";
            const String itemsPropertyName = "items";
            const String companyPropertyName = "company";
            const String pricePropertyName = "price";
            const String tag = "VolumeTest";
            const String dataSource = "TestHarness";
            DateOnly date = DateOnly.Parse("2026-10-16");
            const String requestUrl = "http://localhost:5000/api/v1/stockPrices";

            // Generate the payload / body
            JObject requestBody = new();
            requestBody[gridOuterKeyPropertiesPropertyName] = new JObject();
            requestBody[gridOuterKeyPropertiesPropertyName]["tag"] = tag;
            requestBody[gridOuterKeyPropertiesPropertyName]["dataSource"] = dataSource;
            requestBody[gridOuterKeyPropertiesPropertyName]["date"] = date.ToString("yyyy-MM-dd");
            JArray items = new();
            Random randGen = new();
            for (Int32 i = 0; i < itemCount; i++)
            {
                JObject currentItem = new();
                currentItem[companyPropertyName] = Guid.NewGuid().ToString();
                currentItem[pricePropertyName] = randGen.NextDouble();
                items.Add(currentItem);
            }
            requestBody[itemsPropertyName] = items;

            // Setup to call API
            Stopwatch stopwatch = Stopwatch.StartNew();
            using (HttpClient client = new())
            using (HttpRequestMessage request = new(HttpMethod.Post, requestUrl))
            using (StringContent content = new(requestBody.ToString(Formatting.None)))
            {
                client.Timeout = TimeSpan.FromSeconds(300);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Content = content;
                using (HttpResponseMessage response = client.Send(request))
                {
                    if (response.IsSuccessStatusCode == false)
                    {
                        throw new Exception($"Error calling Powergrid API.  Received status code {response.StatusCode}.");
                    }
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"Completed call to Powergrid in {stopwatch.ElapsedMilliseconds} milliseconds.");
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
                new StockPrice(hitachiCompany, 4738),
                //new StockPrice(sonyCompany, 3221),
                new StockPrice(toyotaCompany, 3666),
                new StockPrice("Kamispring", 10000000004)
            };
            String connectionString = File.ReadAllText(@"..\..\..\..\Documentation\TempConnectionString.txt");
            ConsoleApplicationLogger consoleLogger = new(LogLevel.Debug, '|', "  ");
            StockPricePersister persister = new StockPricePersister(connectionString, 5, 5, 0, consoleLogger);
            StockPriceGridOuterKeyProperties outerKeyProps = new("Test", bloombergDataSource, utils.CreateDateOnlyFromString("2026-07-08"));
            
            persister.PersistGrid(outerKeyProps, testGridItems);
            /*
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
            IList<Tuple<StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp>> gridDetails = persister.GetGridDetails(commonKeyProperties);
            foreach (Tuple<StockPriceGridOuterKeyProperties, GridVersionAndTransactionTimestamp> currentResult in gridDetails)
            {
                Console.WriteLine($"{currentResult.Item1.ToString()}, {currentResult.Item2.Version}, {currentResult.Item2.TransactionTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")}");
            }

            //persister.SoftDeleteLatestGrid(outerKeyProps);
            GridCommonKeyProperties commonKeyProps = new("Test");
            persister.HardDeleteGrids(commonKeyProps);
            */
        }
    }
}
