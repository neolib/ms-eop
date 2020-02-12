using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace IpamFix
{
    using ExcelDataReader;
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using Common;
    using static Console;
    using static IpamHelper;
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
            FixEmptyRegion,
            FixWrongRegion,
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
            var resultExcelFile = args[1];
            var cacheFileName = args[2];
            var ipXmlFile = args.Length > 3 ? args[3] : "result.xml";

            if (!File.Exists(resultExcelFile))
            {
                Environment.ExitCode = (int)ExitCode.FileNotFound;
                Error.WriteLine($"Specified Excel file does not exist ({resultExcelFile})");
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
                    headerText = $"{NameAddressSpace},{NameIpQuery},{NamePrefix},{NameForest},{NameEopDc},{NameIpamDc},{NameRegion},New Datacenter,{NameId}";
                    break;

                case Command.FixEmptyRegion:
                case Command.FixWrongRegion:
                    headerText = $"{NameAddressSpace},{NameIpQuery},{NamePrefix},{NameForest},{NameEopDc},{NameIpamDc},{NameRegion},New Region,{NameId}";
                    break;

                default:
                    Environment.ExitCode = (int)ExitCode.BadArgs;
                    Error.WriteLine($"Unhandled command {cmdName}");
                    return;
            }

            WriteLine("Loading IPAM maps...");
            var tagMap = LoadIpamMaps().Result;
            WriteLine("Loading name maps...");
            var dcNameMap = LoadNameMap();

            var ipXDoc = File.Exists(ipXmlFile) ? XDocument.Load(ipXmlFile) : null;
            var cacheList = File.Exists(cacheFileName) ? File.ReadAllLines(cacheFileName) : new string[0];

            using (var cacheFileWriter = new StreamWriter(cacheFileName, true))
            {
                if (cacheList == null || cacheList.Length == 0)
                {
                    cacheFileWriter.WriteLine(headerText);
                }

                var titleRegex = new Regex(TitlePattern,
                    RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
                var records = ReadRecords(resultExcelFile);

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
                            if (FixTitle_() == null) break;
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
                                //if (FixDatacenter_() == null) ; // break;
                            }
                        }
                    }
                    else if (record.Status == "EmptyRegion")
                    {
                        if (cmd == Command.FixEmptyRegion && record.IpamDcName == "PUS01")
                        {
                            //if (FixRegion_() == null) ; // break;
                        }
                    }

                    string FindVlanName_(string prefix, string filename)
                    {
                        var vlanNode = ipXDoc.XPathSelectElement($"//file[@name='{filename}.xml']/VLANs/VLAN[@*='{prefix}']");
                        return vlanNode?.Attribute("name").Value;
                    }

                    bool? FixTitle_()
                    {
                        string newTitle = null;
                        string description = null;

                        if (record.Title.StartsWith("Load from BGPL"))
                        {
                            var vlan = FindVlanName_(record.Prefix, record.Environment);
                            var ipVersion = record.Prefix.Contains(':') ? "IPv6" : "IPv4";
                            newTitle = $"EOP: {record.Forest}-{record.EopDcName} - {ipVersion}_{vlan}";
                            description = record.Title;
                        }
                        else if (false)
                        {
                            var match = titleRegex.Match(record.Title);
                            if (match.Success)
                            {
                                var headGroup = match.Groups["h"];
                                var forestGroup = match.Groups["f"];
                                var dcGroup = match.Groups["d"];
                                var tailGroup = match.Groups["t"];

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
                            }
                            else
                            {
                                WriteLine($"InvalidTitle pattern match failed: {record.AddressSpace},{record.Prefix},{record.Title}");
                            }
                        }

                        if (newTitle != null)
                        {
                            try
                            {
                                if (UpdateTitle(record.AddressSpace, record.Prefix, record.Id, newTitle, description).Result)
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
                                var logLine = $"{record.AddressSpace},{record.IpString},{record.Prefix},{record.Forest},{record.EopDcName},{record.IpamDcName},{record.Region},{newDcName},{record.Id}";

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

                    bool? FixRegion_()
                    {
                        try
                        {
                            var newRegion = UpdateRegion(record.AddressSpace, record.Prefix, record.Id, record.IpamDcName).Result;
                            if (newRegion != null)
                            {
                                changedCount++;
                                var logLine = $"{record.AddressSpace},{record.IpString},{record.Prefix},{record.Forest},{record.EopDcName},{record.IpamDcName},{record.Region},{newRegion},{record.Id}";

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

        private List<ValidationRecord> ReadRecords(string excelFileName)
        {
            using (var stream = File.Open(excelFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var list = new List<ValidationRecord>();
                    do
                    {
                        WriteLine($"***Reading sheet {reader.Name}...");

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
