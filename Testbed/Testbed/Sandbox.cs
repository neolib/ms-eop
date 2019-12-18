using System.IO;

namespace Testbed.Applets
{
    using ExcelDataReader;
    using static System.Console;

    class Sandbox : IApplet
    {
        public int Run(string[] args)
        {
            TestExcelReader(args[0]);

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
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var s = reader.GetValue(i);
                                Write($"{s},");
                            }
                            WriteLine();
                        }
                        WriteLine();
                    } while (reader.NextResult());
                }
            }
        }

        private void TestCsvReader()
        {
            var filename = @"..\..\Files\test.csv";
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
                                var s = reader.GetValue(i);
                                Write($"{s},");
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
