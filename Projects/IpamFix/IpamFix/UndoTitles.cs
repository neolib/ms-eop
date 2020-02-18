using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IpamFix
{
    using static Console;
    using static IpamHelper;
    using StringMap = Dictionary<string, string>;

    class UndoTitles
    {
        internal void Run(string[] args)
        {
            if (args.Length != 2)
            {
                Error.WriteLine("Need Excel filename.");
                return;
            }

            var excelFileName = args[0];
            var cacheFileName = args[1];
            var records = ExcelHelper.ReadSheet(excelFileName, null, true);

            if (!records.Any())
            {
                WriteLine("No records found.");
                return;
            }

            var cacheLines = File.Exists(cacheFileName) ? File.ReadAllLines(cacheFileName) : null;

            using (var cacheFileWriter = new StreamWriter(cacheFileName, true))
            {
                foreach (var record in records)
                {
                    if (record["Undo"] == "Yes")
                    {
                        var addressSpace = record["Address Space"];
                        var prefix = record["Prefix"];
                        var prefixId = record["Prefix ID"];
                        var oldTitle = record["Title"];
                        var newTitle = record["New Title"];
                        var msg = $"{addressSpace},{prefix},{prefixId},{oldTitle},{newTitle}";

                        if (cacheLines?.Contains(msg) == true)
                        {
                            Error.WriteLine($"Skipping {prefix} in {addressSpace}");
                        }
                        else
                        {
                            WriteLine($"Reverting title of {prefix} in {addressSpace}:");
                            WriteLine(newTitle);
                            WriteLine(">>>");
                            WriteLine(oldTitle);
                            WriteLine();

                            UpdateTitle(addressSpace, prefix, prefixId, oldTitle).Wait();
                            cacheFileWriter.WriteLine(msg);
                        }
                    }
                }
            }
        }
    }
}
