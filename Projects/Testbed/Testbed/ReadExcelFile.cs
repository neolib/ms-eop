using System.IO;
using System.Linq;

namespace Testbed.Applets
{
    using ExcelDataReader;
    using static System.Console;

    class ReadExcelFile : IApplet
    {
        public int Run(string[] args)
        {
            TestExcelReader(args.FirstOrDefault() ?? @"C:\My\dev\v\result csv.xlsx");

            return 0;
        }

        private void TestExcelReader(string filename)
        {
            WriteLine($"Reading file {filename}");
            using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        WriteLine($"***{reader.Name}***");
                        var col = new string[reader.FieldCount];
                        if (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                col[i] = reader.GetString(i);
                                WriteLine($"col#{i}:{col[i]}");
                            }
                        }

                        var lines = 0;
                        while (reader.Read())
                        {
                            Write($"{lines}:");
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var s = reader.GetValue(i) as string;
                                if (s == null)
                                {
                                    Write("|0|,");
                                }
                                else
                                {
                                    s = s.Replace(",", "|,|").Replace("\r", "|r|")
                                        .Replace("\n", "|n|").Replace("\"", "|\"|");
                                    Write($"{s},");
                                }
                            }
                            lines++;
                            WriteLine();
                        }
                        WriteLine();
                    } while (reader.NextResult());
                }
            }
        }
    }
}
