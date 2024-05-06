using Microsoft.Data.SqlClient;
using System.Net;
using System.Data;
namespace DbToCsv;

public class Program
{
    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;

        DateTime dateTime = GetProperDate(args);
        var fileName = $"{dateTime.ToString("yyyyMMdd")}-adventureworks.csv";
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var client = new WebClient();
        var currencyData = client.DownloadString("https://www.cnb.cz/cs/financni-trhy/devizovy-trh/kurzy-devizoveho-trhu/kurzy-devizoveho-trhu/rok.txt?rok=2024").Split("\n");
        string[] currencySplittedLine;
        var currencyValue = 0.0;
        foreach (var line in currencyData)
        {
            if (line.Split('|')[0] == dateTime.ToString("dd.MM.yyyy"))
            {
                currencySplittedLine = line.Split('|');
                var currencyValueString = currencySplittedLine[29];
                currencyValue = Double.Parse(currencyValueString);
                break;
            }
        }
        if (currencyValue == 0.0)
        {
            Console.WriteLine("If you used the date of today, CNB probably didn't provide the data yet. Try using the date of yesterday.\n");
            Console.WriteLine("Exitting application...");
        }
        else
        {
            PrintDelimiter();
            Console.WriteLine($"USD = {currencyValue} Kč\n");
            PrintDelimiter();
            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = "stbechyn-sql.database.windows.net";
                builder.UserID = "prvniit";
                builder.Password = "P@ssW0rd!";
                builder.InitialCatalog = "AdventureWorksDW2020";
                Console.ForegroundColor = ConsoleColor.Cyan;
                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    Console.WriteLine("\nQuery data:");
                    PrintDelimiter();
                    var query = "SELECT EnglishProductName, DealerPrice as [Price USD] FROM DimProduct WHERE DealerPrice IS NOT NULL";
                    using (SqlCommand command = new(query, connection))
                    {
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            //Anonymous object init
                            var data = new List<(string, string, string, string, string)>();
                            Console.WriteLine("Date;EnglishProductName;Price USD;Price CZK;Rate");
                            data.Add(("Date", "EnglishProductName", "Price USD", "Price CZK", "Rate"));
                            while (reader.Read())
                            {
                                //used keys [] as filter
                                string productName = reader["EnglishProductName"].ToString()!;
                                string priceUSD = reader["Price USD"].ToString()!;
                                var priceCZK = (SafelyConvertToDouble(priceUSD) * currencyValue).ToString().Replace(',', '.');
                                Console.WriteLine("{4};{0};{1};{2};{3}", productName, priceUSD = priceUSD.Replace(',', '.'), priceCZK, currencyValue.ToString().Replace(',', '.'), dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss"));
                                data.Add((dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss"), productName, priceUSD, priceCZK, $"{currencyValue.ToString().Replace(',', '.')}"));
                            }
                            connection.Close();
                            ExportToCsv(data, fileName);
                        }
                    }
                }
                Console.ForegroundColor = ConsoleColor.DarkRed;
            }
            catch (SqlException e) { Console.WriteLine(e.ToString()); }
        }
    }

    private static DateTime GetProperDate(string[] args)
    {
        DateTime dateTime;
        if (args.Length == 0)
        {
            dateTime = DateTime.Now;
        }
        else if (DateTime.TryParse(args[0], out dateTime))
        {
            if (dateTime > DateTime.Now)
            {
                Console.WriteLine("The USD rate was not yet given for this date.");
                dateTime = DateTime.Now;
                Console.WriteLine("Using todays date.");
            }
            else
            {
                Console.WriteLine("Parsed datetime: " + dateTime.ToString("dd.MM.yyyy"));
            }
        }
        else
        {
            Console.WriteLine("Invalid datetime format. Using the DateTime of today.");
            dateTime = DateTime.Now;
            Console.WriteLine("Parsed datetime: " + dateTime.ToString("dd.MM.yyyy"));
        }

        if (dateTime.DayOfWeek == DayOfWeek.Sunday)
        {
            dateTime = dateTime.AddDays(-2);
            Console.WriteLine($"Sunday, using the date of {dateTime.ToString("dd.MM.yyyy")}");
        }
        if (dateTime.DayOfWeek == DayOfWeek.Saturday)
        {
            dateTime = dateTime.AddDays(-1);
            Console.WriteLine($"Saturday, using the date of {dateTime.ToString("dd.MM.yyyy")}");
        }

        return dateTime;
    }

    static void ExportToCsv(List<(string, string, string, string, string)> data, string filePath)
    {
        File.WriteAllText(filePath, string.Join('\n', data.Select(x => $"{x.Item1};{x.Item2};{x.Item3};{x.Item4};{x.Item5}")));
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"\nData exported to {filePath}.");
    }
    static List<string> ReadByLine(string path, string dt)
    {
        var output = new List<string>();
        using var sr = new StreamReader(path);
        var line = string.Empty;

        while ((line = sr.ReadLine()) != null)
        {
            var splitLine = line.Split('|');
            if (splitLine[0] == dt)
            {
                output.Add(line);
                Console.WriteLine(line);
            }
        }
        return output;
    }
    static double SafelyConvertToDouble(string x)
    {
        if (Double.TryParse(x, out double res))
        {
            return res;
        }
        return 0.0;
    }
    static void PrintDelimiter()
    {
        Console.WriteLine("=========================================\n");
    }
}
