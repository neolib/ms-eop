using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoQuery
{
    using Kusto.Data;
    using Kusto.Data.Net.Client;
    using System.IO;
    using static Console;

    class Program
    {
        static void Main(string[] args)
        {
            var inputFile = args[0];

            using (var sr = new StreamReader(inputFile))
            {
                var query = "bgplUpdates | where Timestamp >= ago(90d)| where Nlri startswith \"{0}\" | count";
                var client = KustoClientFactory.CreateCslQueryProvider("https://azurenda.kusto.windows.net/;Fed=true;Database=BGPL;");

                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    var fields = line.Split(',');
                    var prefix = fields[0];

                    Error.WriteLine(prefix);
                    var reader = client.ExecuteQuery(query.Replace("{0}", prefix));

                    reader.Read();
                    var exists = reader.GetInt64(0) != 0;

                    Write(exists ? "Yes" : "No");
                    foreach (var field in fields)
                    {
                        Write($",{field}");
                    }
                    WriteLine();
                }
            }

            /*for (int i = 0; i < reader.FieldCount; i++)
            {
                Write(reader.GetName(i));
                Write("|");
            }
            WriteLine();

            while (reader.Read())
            {
                count++;

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    Write(reader.GetValue(i));
                    Write("|");
                }
                WriteLine();
            }
            WriteLine($"{count} record(s) found");
            */

            Error.WriteLine("Hit ENTER to exit");
            ReadLine();
        }
    }
}
