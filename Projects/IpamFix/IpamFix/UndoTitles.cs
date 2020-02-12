﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IpamFix
{
    using ExcelDataReader;
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using Common;
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
            var records = ExcelHelper.ReadSheet(excelFileName, "result", true);

            if (!records.Any())
            {
                WriteLine("No records found.");
                return;
            }

            using (var cacheFileWriter = new StreamWriter(cacheFileName, true))
            {
                var cacheLines = File.Exists(cacheFileName) ? File.ReadAllLines(cacheFileName) : null;

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
                            Error.WriteLine($"Skipping {addressSpace} {prefix}");
                        }
                        else
                        {
                            WriteLine($"Reverting title:");
                            WriteLine(newTitle);
                            WriteLine(">>>");
                            WriteLine(oldTitle);

                            UpdateTitle(addressSpace, prefix, prefixId, oldTitle).Wait();
                        }
                    }
                }
            }
        }
    }
}