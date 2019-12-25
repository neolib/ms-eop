using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        private const string NameId = "Prefix ID";
        private const string NameAddressSpace = "Address Space";
        private const string NameEnvironment = "Environment";
        private const string NameIpQuery = "IP Query";
        private const string NamePrefix = "Prefix";
        private const string NameForest = "Forest";
        private const string NameEopDc = "EOP DC";
        private const string NameIpamDc = "IPAM DC";
        private const string NameTitle = "Title";
        private const string NameRegion = "Region";
        private const string NameStatus = "Status";
        private const string NameSummary = "Summary";
        private const string TitlePattern = @"(?<h>EOP:\s+)(?<f>\w+)-(?<d>\w+?)(?<t>(FSPROD)?\s+-\s+IPv.+)";

        private string[] ExcelFieldNames = new[] {
            NameId, NameAddressSpace, NameEnvironment,
            NamePrefix, NameForest, NameEopDc, NameIpamDc,
            NameTitle, NameRegion, NameStatus, NameSummary
            };

        private class ValidationRecord
        {
            internal string Id;
            internal string AddressSpace;
            internal string Environment;
            internal string IpString;
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

        internal void Process(string resultExcelFile, string cacheFileName)
        {
            if (!File.Exists(resultExcelFile))
            {
                Error.WriteLine($"Specified Excel file does not exist ({resultExcelFile})");
                return;
            }

            var cacheList = File.Exists(cacheFileName) ? File.ReadAllLines(cacheFileName) : new string[0];
            using (var cacheFileWriter = new StreamWriter(cacheFileName, true))
            {
                if (cacheList == null || cacheList.Length == 0)
                {
                    cacheFileWriter.WriteLine($"{NameAddressSpace},{NameIpQuery},{NamePrefix},{NameForest},{NameEopDc},{NameIpamDc},{NameRegion},{NameTitle},New Title,{NameId}");
                }

                var titleRegex = new Regex(TitlePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var records = ReadRecords(resultExcelFile);

                if (records == null) return;
                if (records.Count == 0)
                {
                    Error.WriteLine("No result records");
                    return;
                }

                var changedCount = 0;

                foreach (var record in records)
                {
                    if (record.Id == null) continue; // static record
                    if (cacheList.Any((line_) => line_.Contains(record.Id)))
                    {
                        WriteLine($"Hit cache: {record.AddressSpace},{record.Prefix}");
                        continue;
                    }

                    if (record.Status == "InvalidTitle")
                    {
                        var match = titleRegex.Match(record.Title);
                        if (match.Success)
                        {
                            var headGroup = match.Groups["h"];
                            var forestGroup = match.Groups["f"];
                            var dcGroup = match.Groups["d"];
                            var tailGroup = match.Groups["t"];
                            string newTitle = null;

                            if (record.Summary.Contains("not contain forest name"))
                            {
                                newTitle = titleRegex.Replace(record.Title, (match_) =>
                                    $"{headGroup.Value}{record.Forest}-{dcGroup.Value}{tailGroup.Value}");
                            }
                            else if (record.Summary.Contains("not contain datacenter name"))
                            {
                                if (string.Compare(forestGroup.Value, record.Forest, true) != 0)
                                {
                                    // Title contains no forest name, need to replace forest part as well
                                    newTitle = titleRegex.Replace(record.Title, (match_) =>
                                        $"{headGroup.Value}{record.Forest}-{record.EopDcName}{tailGroup.Value}");
                                }
                                else
                                {
                                    // Need only to replace datacenter part
                                    newTitle = titleRegex.Replace(record.Title, (match_) =>
                                        $"{headGroup.Value}{forestGroup.Value}-{record.EopDcName}{tailGroup.Value}");
                                }
                            }

                            if (newTitle != null)
                            {
                                try
                                {
                                    //var success = UpdatePrefixTitle(record.AddressSpace,
                                    //    record.Prefix, record.Id, newTitle.ToString()).Result;

                                    //if (success)
                                    {
                                        changedCount++;
                                        var logLine = $"{record.AddressSpace},{record.IpString},{record.Prefix},{record.Forest},{record.EopDcName},{record.IpamDcName},{record.Region},{record.Title.ToCsvValue()},{newTitle.ToCsvValue()},{record.Id}";

                                        WriteLine($"{changedCount} {logLine}");
                                        //cacheFileWriter.WriteLine(logLine);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Error.WriteLine($"***{record.AddressSpace},{record.Prefix}: {ex}");
                                    break;
                                }
                            }
                        }
                    }
                    else if (record.Title == "Load from BGPL")
                    {
                        // Fix datacenter and title
                    }
                } // record

                WriteLine($"Total records changed: {changedCount}");
            }
        }

        async Task<bool> UpdatePrefixTitle(string addressSpace, string prefix, string prefixId, string newTitle)
        {
            var queryModel = AllocationQueryModel.Create(addressSpaceIdMap[addressSpace], prefix);
            var queryResult = await IpamClient.QueryAllocationsAsync(queryModel);
            var allocModel = queryResult.Single();
            if (allocModel.Id == prefixId)
            {
                allocModel.Tags["Title"] = newTitle;
                await IpamClient.UpdateAllocationTagsV2Async(allocModel);
                return true;
            }
            else
            {
                Error.WriteLine($"***Prefix ID mismatch: {addressSpace},{prefix},{prefixId},{allocModel.Id}");
                return false;
            }
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

                        var hasWrongNames = false;

                        foreach (var name in ExcelFieldNames)
                        {
                            if (Array.IndexOf(fieldNames, name) < 0)
                            {
                                Error.WriteLine($"Excel result sheet does not contain expected field {name}");
                                hasWrongNames = true;
                            }
                        }
                        if (hasWrongNames) return null;

                        string ReadString_(string name_) =>
                            reader.GetString(Array.IndexOf(fieldNames, name_));

                        // Read rest
                        while (reader.Read())
                        {
                            var addressSpace = ReadString_(NameAddressSpace);

                            if (string.IsNullOrEmpty(addressSpace)) continue;

                            if (!addressSpaceIdMap.ContainsKey(addressSpace))
                            {
                                Error.WriteLine($"Address space map has no key {addressSpace}");
                                return null;
                            }

                            list.Add(new ValidationRecord
                            {
                                Id = ReadString_(NameId),
                                AddressSpace = addressSpace,
                                Environment = ReadString_(NameEnvironment),
                                IpString = ReadString_(NameIpQuery),
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
                    } while (reader.NextResult());
                    return list;
                }
            }
        }
    }
}
