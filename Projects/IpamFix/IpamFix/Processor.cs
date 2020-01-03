using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        private const string NameComment = "Comment";
        private const string TitlePattern = @"(?<h>EOP:\s+)(?<f>\w+)(\s+)?-(\s+)?(?<d>\w+?)(?<t>(FSPROD)?\s+(-\s+)?IPv\d.+)";

        private string[] ExcelFieldNames = new[] {
            NameId, NameAddressSpace, NameEnvironment,
            NamePrefix, NameForest, NameEopDc, NameIpamDc,
            NameTitle, NameRegion, NameStatus, NameSummary, NameComment
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
            internal string Comment;
        }

        private enum Command
        {
            Unknown,
            FixTitle,
            FixEmptyDC,
            FixWrongDC,
        }

        internal IpamClient IpamClient { get; set; }

        private StringMap addressSpaceIdMap = new StringMap {
            { "Default", SpecialAddressSpaces.DefaultAddressSpaceId },
            { "GalaCake", SpecialAddressSpaces.GalaCakeAddressSpaceId },
            { "EX", SpecialAddressSpaces.EXAddressSpaceId },
            { "RX", SpecialAddressSpaces.RXAddressSpaceId },
            };

        private StringMap datacenterNameMap = new StringMap();
        private Dictionary<string, TagModel> datacenterMaps = new Dictionary<string, TagModel>();

        private async Task LoadDatacenterMaps()
        {
            foreach (var entry in addressSpaceIdMap)
            {
                var tag = await this.IpamClient.GetTagAsync(entry.Value, SpecialTags.Datacenter);
                datacenterMaps[entry.Key] = tag;
            }
        }

        private void LoadNameMap()
        {
            var myType = this.GetType();
            var rcName = myType.Namespace + ".Files.NameMap.xml";
            using (var rcs = myType.Assembly.GetManifestResourceStream(rcName))
            {
                var mapDoc = XDocument.Load(rcs);

                foreach (var node in mapDoc.Root.Element("DCNames").Elements())
                {
                    var eopName = node.Attribute("EOPName").Value;
                    var azureName = node.Attribute("AzureName").Value;
                    datacenterNameMap[eopName] = azureName;
                }
            }
        }

        internal void Run(string[] args)
        {
            if (args.Length != 3)
            {
                Environment.ExitCode = (int)ExitCode.BadArgs;
                Error.WriteLine($"SYNTAX: CMD rsult.xlsx cachefile.csv");
                return;
            }

            var cmdName = args[0];
            var resultFile = args[1];
            var cacheFileName = args[2];

            if (!File.Exists(resultFile))
            {
                Environment.ExitCode = (int)ExitCode.FileNotFound;
                Error.WriteLine($"Specified Excel file does not exist ({resultFile})");
                return;
            }

            string headerText = null;
            if (!Enum.TryParse(cmdName, out Command cmd))
            {
                Environment.ExitCode = (int)ExitCode.BadArgs;
                throw new Exception($"Invalid command {cmdName}");
            }

            switch (cmd)
            {
                case Command.FixTitle:
                    headerText = $"{NameAddressSpace},{NameIpQuery},{NamePrefix},{NameForest},{NameEopDc},{NameIpamDc},{NameRegion},{NameTitle},New Title,{NameId}";
                    break;
                case Command.FixEmptyDC:
                case Command.FixWrongDC:
                    headerText = $"{NameAddressSpace},{NameIpQuery},{NamePrefix},{NameForest},{NameEopDc},{NameIpamDc},{NameRegion},{NameId}";
                    break;

                default:
                    Environment.ExitCode = (int)ExitCode.BadArgs;
                    Error.WriteLine($"Unhandled command {cmdName}");
                    return;
            }

            WriteLine("Loading datacenter maps...");
            LoadDatacenterMaps().Wait();
            WriteLine("Loading name maps...");
            LoadNameMap();

            var cacheList = File.Exists(cacheFileName) ? File.ReadAllLines(cacheFileName) : new string[0];
            using (var cacheFileWriter = new StreamWriter(cacheFileName, true))
            {
                if (cacheList == null || cacheList.Length == 0)
                {
                    cacheFileWriter.WriteLine(headerText);
                }

                var titleRegex = new Regex(TitlePattern,
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
                var records = ReadRecords(resultFile);

                if (records == null)
                {
                    Environment.ExitCode = (int)ExitCode.NoRecords;
                    return;
                }

                if (records.Count == 0)
                {
                    Error.WriteLine("No result records");
                    Environment.ExitCode = (int)ExitCode.NoRecords;
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
                        if (cmd == Command.FixTitle)
                        {
                            //if (FixTitle_() == null) break;
                        }
                    }
                    else if (record.Status == "EmptyDatacenter")
                    {
                        if (cmd == Command.FixEmptyDC)
                        {
                            //if (FixWrongDatacenter_() == null) ; // break;
                        }
                    }
                    else if (record.Status == "MismatchedDcName")
                    {
                        if (cmd == Command.FixWrongDC)
                        {
                            if (record.Comment == "Valid")
                            {
                                if (FixDatacenter_() == null) ; // break;
                            }
                        }
                    }

                    bool? FixTitle_()
                    {
                        if (record.Title.ContainsText("UNKNOWN"))
                        {
                            WriteLine($"Skipping UNKNOWN title: {record.AddressSpace},{record.Prefix},{record.Title}");
                            return false;
                        }

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
                                    if (UpdateTitle(record.AddressSpace, record.Prefix, record.Id, newTitle.ToString()).Result)
                                    {
                                        changedCount++;
                                        var logLine = $"{record.AddressSpace},{record.IpString},{record.Prefix},{record.Forest},{record.EopDcName},{record.IpamDcName},{record.Region},{record.Title.ToCsvValue()},{newTitle.ToCsvValue()},{record.Id}";

                                        WriteLine($"{changedCount} {logLine}");
                                        cacheFileWriter.WriteLine(logLine);
                                    }
                                    return true;
                                }
                                catch (Exception ex)
                                {
                                    Error.WriteLine($"***{record.AddressSpace},{record.Prefix}: {ex.Message}");
                                    return null;
                                }
                            }
                        }
                        else
                        {
                            WriteLine($"InvalidTitle pattern match failed: {record.AddressSpace},{record.Prefix},{record.Title}");
                        }
                        return false;
                    }

                    bool? FixDatacenter_()
                    {
                        try
                        {
                            var newDcName = UpdateDatacenter(record.AddressSpace, record.Prefix, record.Id, record.EopDcName).Result;
                            if (newDcName != null)
                            {
                                changedCount++;
                                var logLine = $"{record.AddressSpace},{record.IpString},{record.Prefix},{record.Forest},{record.EopDcName},{newDcName},{record.Region},{record.Id}";

                                WriteLine($"{changedCount} {logLine}");
                                cacheFileWriter.WriteLine(logLine);
                            }
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Error.WriteLine($"***{record.AddressSpace},{record.Prefix}: {ex.Message}");
                            return null;
                        }
                    }
                } // record

                WriteLine($"Total records changed: {changedCount}");
            }

            Environment.ExitCode = (int)ExitCode.Success;
        }

        async Task<AllocationModel> QueryIpam(string addressSpace, string prefix, string prefixId)
        {
            var queryModel = AllocationQueryModel.Create(addressSpaceIdMap[addressSpace], prefix);
            var queryResult = await IpamClient.QueryAllocationsAsync(queryModel);
            foreach (var allocation in queryResult)
            {
                if (allocation.Id == prefixId) return allocation;
            }

            Error.WriteLine($"***Not found in IPAM: {addressSpace},{prefix},{prefixId}");
            if (queryResult.Count > 0)
            {
                foreach (var allocation in queryResult)
                {
                    allocation.Tags.TryGetValue(SpecialTags.Region, out var allocRegion);
                    allocation.Tags.TryGetValue(SpecialTags.PhysicalNetwork, out var network);
                    allocation.Tags.TryGetValue(SpecialTags.PropertyGroup, out var propertyGroup);

                    Error.WriteLine("  Region: {0}, Physical Network: {1}, Property Group: {2}",
                        allocRegion, network, propertyGroup);
                }
            }
            return null;
        }

        async Task<bool> UpdateTitle(string addressSpace, string prefix, string prefixId, string newTitle)
        {
            var allocation = await QueryIpam(addressSpace, prefix, prefixId);
            if (allocation != null)
            {
                allocation.Tags[SpecialTags.Title] = newTitle;
                await IpamClient.UpdateAllocationTagsV2Async(allocation);
                return true;
            }
            return false;
        }

        async Task<string> UpdateDatacenter(string addressSpace, string prefix, string prefixId, string eopDcName)
        {
            if (!datacenterNameMap.TryGetValue(eopDcName, out var newDcName))
            {
                newDcName = eopDcName;
                WriteLine($"EOP datacenter {eopDcName} has no azure mapping");
            }

            var allocation = await QueryIpam(addressSpace, prefix, prefixId);
            if (allocation != null)
            {
                allocation.Tags.Clear();
                allocation.Tags[SpecialTags.Datacenter] = newDcName;
                await IpamClient.PatchAllocationTagsV2Async(allocation);
                return newDcName;
            }
            return null;
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
                                Comment = ReadString_(NameComment),
                            });
                        }
                    } while (reader.NextResult());
                    return list;
                }
            }
        }
    }
}
