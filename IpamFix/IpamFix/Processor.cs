using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace IpamFix
{
    using ExcelDataReader;
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using F5IPConfigValidator;
    using static Console;
    using StringMap = Dictionary<string, string>;

    class Processor
    {
        private const string NameId = "Id";
        private const string NameAddressSpace = "Address Space";
        private const string NameEnvironment = "Environment";
        private const string NamePrefix = "Prefix";
        private const string NameForest = "Forest";
        private const string NameEopDc = "EOP DC";
        private const string NameIpamDc = "IPAM DC";
        private const string NameTitle = "Title";
        private const string NameRegion = "Region";
        private const string NameStatus = "Status";
        private const string NameSummary = "Summary";

        private class ValidationRecord
        {
            internal string Id;
            internal string AddressSpace;
            internal string Environment;
            internal string Prefix;
            internal string Forest;
            internal string EopDcName;
            internal string IpamDcName;
            internal string Title;
            internal string Region;
            internal string Status;
            internal string Summary;
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
            var titlePattern = new Regex(@"(?<h>EOP:\s+)(?<f>\w+)-(?<dc>\w+)(?<t>\s+-\s+IPv.+)",
                 RegexOptions.Singleline);
            var list = ReadRecords(excelFileName);

            foreach (var record in list)
            {
                if (record.Status == "InvalidTitle")
                {
                    var match = titlePattern.Match(record.Title);
                    if (match.Success)
                    {
                        if (Array.IndexOf(cacheList, record.Id) >= 0)
                        {
                            WriteLine($"Hit cache: {record.AddressSpace},{record.Prefix}");
                            continue;
                        }

                        var headGroup = match.Groups["h"];
                        var forestGroup = match.Groups["f"];
                        var dcGroup = match.Groups["dc"];
                        var tailGroup = match.Groups["t"];
                        string newTitle = null;

                        if (record.Summary.Contains("not contain forest name"))
                        {
                            newTitle = titlePattern.Replace(record.Title, (match_) =>
                                $"{headGroup.Value}{record.Forest}-{dcGroup.Value}{tailGroup.Value}");
                        }
                        else if (record.Summary.Contains("not contain datacenter name"))
                        {
                            if (string.Compare(forestGroup.Value, record.Forest, true) != 0)
                            {
                                // Title contains no forest name, need to replace forest part as well
                                newTitle = titlePattern.Replace(record.Title, (match_) =>
                                    $"{headGroup.Value}{record.Forest}-{record.EopDcName}{tailGroup.Value}");
                            }
                            else
                            {
                                // Need only to replace datacenter part
                                newTitle = titlePattern.Replace(record.Title, (match_) =>
                                    $"{headGroup.Value}{forestGroup.Value}-{record.EopDcName}{tailGroup.Value}");
                            }
                        }

                        if (newTitle != null)
                        {
                            UpdatePrefixTitle(record.AddressSpace, record.Prefix, newTitle.ToString());
                            var logLine = $"{record.AddressSpace},{record.Prefix},{record.Id},{record.Title.ToCsvValue()},{newTitle.ToCsvValue()}";
                            cacheFileWriter.Write(logLine);
                        }
                    }
                }
            }
        }

        async void UpdatePrefixTitle(string addressSpace, string prefixId, string newTitle)
        {
            var model = new AllocationModel
            {
                Id = prefixId,
                AddressSpaceId = addressSpaceIdMap[addressSpace],
            };
            model.Tags["Title"] = newTitle;
            await IpamClient.UpdateAllocationTagsV2Async(model);
        }

        private List<ValidationRecord> ReadRecords(string excelFileName)
        {
            using (var stream = File.Open(excelFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var list = new List<ValidationRecord>();
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

                        string ReadString_(string name_) => reader.GetString(Array.IndexOf(fieldNames, name_);

                        // Read rest
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                list.Add(new ValidationRecord
                                {
                                    Id = ReadString_(NameId),
                                    AddressSpace = ReadString_(NameAddressSpace),
                                    Environment = ReadString_(NameEnvironment),
                                    Prefix = ReadString_(NamePrefix),
                                    Forest = ReadString_(NameForest).ToUpper(),
                                    EopDcName = ReadString_(NameEopDc).ToUpper(),
                                    IpamDcName = ReadString_(NameIpamDc),
                                    Title = ReadString_(NameTitle),
                                    Region = ReadString_(NameRegion),
                                    Status = ReadString_(NameStatus),
                                    Summary = ReadString_(NameSummary),
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
