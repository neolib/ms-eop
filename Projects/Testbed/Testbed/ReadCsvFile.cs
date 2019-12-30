using System.IO;
using System.Linq;

namespace Testbed.Applets
{
    using ExcelDataReader;
    using static System.Console;

    class ReadCsvFile : IApplet
    {
        public int Run(string[] args)
        {
            TestCsvReader(args.FirstOrDefault() ?? @"..\..\Files\test.csv");

            return 0;
        }

        private void TestCsvReader(string filename)
        {
            using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateCsvReader(stream))
                {
                    do
                    {
                        WriteLine($"***{reader.Name}***");
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var s = reader.GetValue(i) as string;
                                if (s == null)
                                {
                                    Write("<0>,");
                                }
                                else
                                {
                                    s = s.Replace(",", "|,|").Replace("\r", "|r|")
                                        .Replace("\n", "|n|").Replace("\"", "|\"|");
                                    Write($"{s},");
                                }
                            }
                            WriteLine();
                        }
                        WriteLine();
                    } while (reader.NextResult());
                }
            }
        }
    }
}
