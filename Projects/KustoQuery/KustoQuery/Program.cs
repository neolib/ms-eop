using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace KustoQuery
{
    using Kusto.Data.Net.Client;
    using static Console;

    class Program
    {
        static string[] IpamTableNames = new[] { "Default", "Galacake" };

        static void Main(string[] args)
        {
            var inputFile = args[0];

            try
            {
                //QueryBGPLUpdates(inputFile);
                QueryIpamReportOnBGPL(inputFile);
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex.Message);
            }

            Error.WriteLine("Hit ENTER to exit");
            ReadLine();
        }

        static void QueryBGPLUpdates(string inputFile)
        {
            using (var sr = new StreamReader(inputFile))
            {
                var query = "bgplUpdates | where Timestamp >= ago(90d)| where Nlri startswith '{0}' | count";
                var client = KustoClientFactory.CreateCslQueryProvider("https://azurenda.kusto.windows.net/;Fed=true;Database=BGPL;");

                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    var fields = line.Split(',');
                    var prefix = fields[0];

                    Error.Write(prefix);
                    var reader = client.ExecuteQuery(query.Replace("{0}", prefix));

                    reader.Read();
                    var exists = reader.GetInt64(0) != 0;
                    var yesNo = exists ? "Yes" : "No";

                    Error.WriteLine($" => {yesNo}");
                    Write(yesNo);
                    foreach (var field in fields)
                    {
                        Write($",{field}");
                    }
                    WriteLine();
                }
            }
        }

        static void QueryIpamReportOnBGPL(string inputFile)
        {
            using (var sr = new StreamReader(inputFile))
            {
                var queryTemplate = GetResourceString("KustoQuery.Files.KustoIpamReport.txt");
                var client = KustoClientFactory.CreateCslQueryProvider("https://ipam.kusto.windows.net/;Fed=true;Database=IpamReport;");

                var needle = "| project";
                var header = queryTemplate.Substring(queryTemplate.LastIndexOf(needle) + needle.Length);

                header = Regex.Replace(header, @"\s+", string.Empty);
                header = "Address Space," + header;

                WriteLine(header);
                Error.WriteLine(header);

                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    var fields = line.Split(',');
                    var prefix = fields[0];
                    var found = false;

                    Error.Write(prefix);

                    foreach (var tableName in IpamTableNames)
                    {
                        var query = string.Format(queryTemplate, tableName, prefix);
                        var reader = client.ExecuteQuery(query);

                        if (reader.Read())
                        {
                            found = true;

                            Write($"{tableName},");
                            Error.WriteLine($" => {tableName}");

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Write(reader.GetValue(i));
                                if (i < reader.FieldCount - 1) Write(",");
                            }
                            WriteLine();

                            break;
                        }
                    }

                    if (!found)
                    {
                        WriteLine($"None,{prefix}");
                        Error.WriteLine(" => Not found");
                    }
                }
            }
        }

        static string GetResourceString(string rcName)
        {
            using (var rcs = Assembly.GetExecutingAssembly().GetManifestResourceStream(rcName))
            using (var sr = new StreamReader(rcs))
            {
                return sr.ReadToEnd();
            }
        }
    }
}
