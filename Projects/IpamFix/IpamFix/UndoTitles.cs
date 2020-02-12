using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (args.Length == 0)
            {
                Error.WriteLine("Need Excel filename.");
                return;
            }

            var excelFileName = args[0];
            var records = ExcelHelper.ReadSheet(excelFileName, "result", true);

            if (!records.Any())
            {
                WriteLine("No records found.");
                return;
            }

            foreach (var record in records)
            {
                if (record["Undo"] == "Yes")
                {
                    var addressSpace = record["Address Space"];
                    var prefix = record["Prefix"];
                    var prefixId = record["Prefix ID"];
                    var oldTitle = record["Title"];
                    var newTitle = record["New Title"];

                    WriteLine($"Reverting title:");
                    WriteLine(newTitle);
                    WriteLine("to");
                    WriteLine(oldTitle);

                    UpdateTitle(addressSpace, prefix, prefixId, oldTitle).Wait();
                }
            }
        }
    }
}
