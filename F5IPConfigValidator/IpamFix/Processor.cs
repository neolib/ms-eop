using System;
using System.Collections.Generic;
using System.IO;

namespace IpamFix
{
    using ExcelDataReader;
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using System.Collections.Generic;
    using static Console;
    using StringMap = Dictionary<string, string>;

    class Processor
    {
        private class PrefixRecord
        {
            internal string AddressSpace;
            internal string Prefix;
            internal string Forest;
            internal string EopDcName;
            internal string IpamDcName;
            internal string Title;
        }

        internal IpamClient IpamClient { get; set; }

        private StringMap addressSpaceIdMap = new StringMap {
            { "Default", SpecialAddressSpaces.DefaultAddressSpaceId },
            { "GalaCake", SpecialAddressSpaces.GalaCakeAddressSpaceId },
            { "EX", SpecialAddressSpaces.EXAddressSpaceId },
            { "RX", SpecialAddressSpaces.RXAddressSpaceId },
            };

        internal void Process(string excelFileName, string cacheFileName)
        {
            var cacheList = File.ReadAllLines(cacheFileName);
            var cacheFileWriter = new StreamWriter(cacheFileName, true);

            var list = GetInvalidTitlePrefixes(excelFileName);
            foreach (var record in list)
            {
                var logLine = $"{record.AddressSpace},{record.Prefix}";
                if (Array.IndexOf(cacheList, logLine) >= 0)
                {
                    WriteLine($"Hit cache: {logLine}");
                    continue;
                }
                var newTitle = GetNewTitle_(record.Title);
                UpdatePrefixTitle(record.AddressSpace, record.Prefix, newTitle);
                cacheFileWriter.Write(logLine);
            }

            string GetNewTitle_(string title_)
            {
                return null;
            }
        }

        async void UpdatePrefixTitle(string addressSpace, string prefix, string newTitle)
        {
            var model = new AllocationModel
            {
                Prefix = prefix,
                AddressSpaceId = addressSpaceIdMap[addressSpace],
            };
            model.Tags["Title"] = newTitle;
            await IpamClient.UpdateAllocationTagsV2Async(model);
        }

        private List<PrefixRecord> GetInvalidTitlePrefixes(string excelFileName)
        {
            using (var stream = File.Open(excelFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var list = new List<PrefixRecord>();
                    do
                    {
                        WriteLine($"***{reader.Name}***");
                        if (reader.Name != "result") continue;

                        // First row is header
                        var fieldNames = new string[reader.FieldCount];
                        if (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                fieldNames[i] = reader.GetString(i);
                            }
                        }

                        // Read rest
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                list.Add(new PrefixRecord
                                {
                                    AddressSpace = reader.GetString(Array.IndexOf(fieldNames, "Address Space")),
                                    Prefix = reader.GetString(Array.IndexOf(fieldNames, "Prefix")),
                                    Forest = reader.GetString(Array.IndexOf(fieldNames, "Forest")),
                                    EopDcName = reader.GetString(Array.IndexOf(fieldNames, "EOP DC")),
                                    IpamDcName = reader.GetString(Array.IndexOf(fieldNames, "IPAM DC")),
                                    Title = reader.GetString(Array.IndexOf(fieldNames, "Title")),
                                });
                            }
                            WriteLine();
                        }
                        WriteLine();
                    } while (reader.NextResult());
                    return list;
                }
            }
        }
    }
}
