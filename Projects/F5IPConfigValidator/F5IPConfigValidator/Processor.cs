﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace F5IPConfigValidator
{
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using Common;
    using static System.Console;
    using StringList = List<string>;
    using StringMap = Dictionary<string, string>;
    using StringListMap = Dictionary<string, List<string>>;

    #region Classes

    public enum ValidationStatus
    {
        Unknown,
        Success,
        NoMatch,            // No matching record found in Kusto
        MultipleMatches,    // A specific IP has multiple prefixes
        WrongAddressSpace,  // In wrong addressspace
        Obsolete,           // Should be removed in config
        NoMappingDcName,    // EOP datacenter name has no mapping Azure name
        EmptyTitle,
        EmptyDatacenter,
        EmptyRegion,
        MismatchedDcName,   // Azure name does not match EOP name
        InvalidTitle,       // No datacenter/forest name in title
        InvalidRegion,
        DubiousTitle,       // Title has dubious words
    }

    public class ValidationRecord
    {
        public string Id;
        public string AddressSpace;
        public string IpString;
        public string Prefix;
        public string Environment;  // Forest-DC
        public string Forest;       // Forest name in _environments.xml
        public string EopDcName;    // EOP datacenter name obtained from config name
        public string IpamDcName;   // Name in IPAM
        public string Region;
        public string Title;
        public string Summary;
        public ValidationStatus Status;

        public ValidationRecord(ValidationStatus status, string summary = null)
        {
            this.Status = status;
            this.Summary = summary;
        }

        public static readonly ValidationRecord NoMatch = new ValidationRecord(ValidationStatus.NoMatch);
        public static readonly ValidationRecord Success = new ValidationRecord(ValidationStatus.Success);
    }

    #endregion

    class Processor
    {
        private static readonly char[] FieldSeparatorChars = new[] { ',', ' ' };

        internal IpamClient IpamClient { get; set; }
        private List<string> ipHotList = new List<string>();
        private List<string> prefixIdList = new List<string>();

        private StringMap dcNameMap = new StringMap();
        private StringMap suffixNameMap = new StringMap();
        private StringMap forestNameMap = new StringMap();
        private StringMap envSpaceMap = new StringMap();
        private StringMap dcNameExceptionMap = new StringMap();
        private StringListMap forestAliasMap = new StringListMap();
        private StringListMap azureNameMap = new StringListMap();
        private StringList ipStringExclusionList = new StringList();
        private Dictionary<string, StringMap> regionMaps = new Dictionary<string, StringMap>();
        private Dictionary<string, Regex> dubiousTitlePatterns = new Dictionary<string, Regex>();
        private StringList dubiousTitleWords = new StringList();

        private StringMap addressSpaceIdMap = new StringMap {
            { "Default", SpecialAddressSpaces.DefaultAddressSpaceId },
            { "GalaCake", SpecialAddressSpaces.GalaCakeAddressSpaceId },
            { "EX", SpecialAddressSpaces.EXAddressSpaceId },
            //{ "RX", SpecialAddressSpaces.RXAddressSpaceId },
            };

        private void LoadForestMap()
        {
            var myType = this.GetType();
            var rcName = myType.Namespace + ".Files._environments.xml";
            using (var rcs = myType.Assembly.GetManifestResourceStream(rcName))
            {
                var envDoc = XDocument.Load(rcs);

                foreach (var node in envDoc.Root.Elements("object"))
                {
                    var envName = node.Attribute("environment-name").Value;
                    if (!envName.StartsWith("_"))
                    {
                        var forestName = node.Attribute("forest-name").Value;
                        forestNameMap[envName.ToUpper()] = forestName;
                    }
                }
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
                    dcNameMap[eopName] = azureName;
                }

                foreach (var node in mapDoc.Root.Element("ForestAliases").Elements())
                {
                    var forestName = node.Attribute("ForestName").Value;
                    var aliases = node.Attribute("Aliases").Value;
                    forestAliasMap[forestName] = aliases.SplitWithoutEmpty(FieldSeparatorChars).ToList();
                }

                foreach (var node in mapDoc.Root.Element("DCSuffixes").Elements())
                {
                    var suffix = node.Attribute("Text").Value;
                    var forestNames = node.Attribute("ForestNames").Value;
                    foreach (var forestName in forestNames.SplitWithoutEmpty(FieldSeparatorChars))
                    {
                        suffixNameMap[forestName] = suffix;
                    }
                }

                foreach (var node in mapDoc.Root.Element("EnvironmentSpaces").Elements())
                {
                    var spaceName = node.Attribute("SpaceName").Value;
                    var envNames = node.Attribute("EnvironmentNames").Value;
                    foreach (var envName in envNames.SplitWithoutEmpty(FieldSeparatorChars))
                    {
                        envSpaceMap[envName] = spaceName;
                    }
                }

                foreach (var node in mapDoc.Root.Element("DCNameExceptions").Elements())
                {
                    var envName = node.Attribute("Environment").Value;
                    var dcName = node.Attribute("AzureDCName").Value;
                    dcNameExceptionMap[envName] = dcName;
                }

                foreach (var node in mapDoc.Root.Element("ExclusionList").Elements())
                {
                    var values = node.Attribute("StartsWith").Value;
                    foreach (var value in values.SplitWithoutEmpty(FieldSeparatorChars))
                    {
                        ipStringExclusionList.Add(value);
                    }
                }

                foreach (var node in mapDoc.Root.Element("TitleWordingIssues").Elements())
                {
                    if (node.Name == "Contains")
                    {
                        dubiousTitleWords.Add(node.Attribute("Text").Value);
                    }
                    else if (node.Name == "Pattern")
                    {
                        var text = node.Attribute("Text").Value;
                        dubiousTitlePatterns[text] = new Regex(
                            text,
                            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Singleline);
                    }
                    else
                    {
                        Error.WriteLine($"Invalid node name {node.Name} in TitleWordingIssues");
                    }
                }
            }

            /*
             * Build reverse Azure name map out of datacenter name map.
             *
             * EOP to Azure mapping is one/many to one, but Azure to EOP is one to many.
             *
             * */

            foreach (var entry in dcNameMap)
            {
                if (!azureNameMap.TryGetValue(entry.Value, out var list))
                {
                    list = new StringList();
                    azureNameMap[entry.Value] = list;
                }
                list.Add(entry.Key);
            }
        }

        private string GetAzureDcName(string eopName)
        {
            if (dcNameMap.TryGetValue(eopName.ToUpper(), out var name))
            { return name; }
            return null;
        }

        private string GetDcSuffix(string forestName)
        {
            if (suffixNameMap.TryGetValue(forestName.ToUpper(), out var suffix))
            { return suffix; }
            return null;
        }

        private string GetForestName(string forestName)
        {
            if (forestNameMap.TryGetValue(forestName.ToUpper(), out var name))
            { return name; }
            return null;
        }

        private string GetEnvSpaceName(string envName)
        {
            if (envSpaceMap.TryGetValue(envName.ToUpper(), out var name))
            { return name; }
            return null;
        }

        private string GetDcExceptionName(string envName)
        {
            if (dcNameExceptionMap.TryGetValue(envName.ToUpper(), out var dcExName))
            { return dcExName; }
            return null;
        }

        private StringList GetForestAliases(string forestName)
        {
            if (forestAliasMap.TryGetValue(forestName.ToUpper(), out var list))
            { return list; }
            return null;
        }

        private StringList GetEopNames(string azureName)
        {
            if (azureNameMap.TryGetValue(azureName.ToUpper(), out var list))
            { return list; }
            return null;
        }

        /// <summary>
        /// Loads datacenter to region mappings from IPAM.
        /// </summary>
        /// <returns></returns>
        private async Task LoadIpamRegionMap()
        {
            foreach (var entry in addressSpaceIdMap)
            {
                var tag = await this.IpamClient.GetTagAsync(entry.Value, SpecialTags.Datacenter);

                if (tag.ImpliedTags.TryGetValue(SpecialTags.Region, out var regionMap))
                {
                    this.regionMaps[entry.Key] = regionMap;
                }
            }
        }

        /// <summary>
        /// Processes input XML file and checks against IPAM for invalid IP addresses.
        /// </summary>
        /// <param name="resultFile">Result XML file generated by the IpTagFinder applet.</param>
        /// <remarks>
        /// Output is in CSV format.
        /// </remarks>
        internal async Task Process(string resultFile)
        {
            await LoadIpamRegionMap();

            // Verify if region maps are good.
            var allRegionsGood = true;
            foreach (var entry in regionMaps)
            {
                if (entry.Value.Count == 0)
                {
                    allRegionsGood = false;
                    Error.WriteLine($"Address space {entry.Key} has no region map!");
                }
            }
            if (!allRegionsGood) return;

            LoadForestMap();
            LoadNameMap();

            // CSV header row
            WriteLine("Address Space,Comment,Environment,Forest,EOP DC,IP Query,Prefix,IPAM DC,Region,Status,Summary,Title,Prefix ID");

            var xd = XDocument.Load(resultFile);
            var envNodes = xd.Root.XPathSelectElements("//file[not(starts-with(@name, '_'))]");

            if (!envNodes.Any())
            {
                throw new Exception("No environment nodes!");
            }

            // Validate EOP to Azure name mapping.
            foreach (var fileNode in envNodes)
            {
                var configName = fileNode.Attribute("name").Value;
                var envName = ExtractEnvironmentName_(configName);
                var eopDcName = ExtractDcName_(envName);
                var forestName = GetForestName(envName);

                /*
                 * Pre-validate forest/datacenter names.
                 *
                 * */

                if (eopDcName == null)
                {
                    Error.WriteLine($"***Environment {envName} has no datacenter name");
                }
                else
                {
                    var azureName = GetAzureDcName(eopDcName);
                    if (azureName == null)
                    {
                        var summary = $"EOP datacenter {eopDcName} has no Azure name";
                        WriteLine($",,{envName},{forestName},{eopDcName},,,,,{ValidationStatus.NoMappingDcName},{summary}");
                    }
                }

                if (forestName == null)
                {
                    Error.WriteLine($"***Environment {envName} has no mapped forest name");
                    forestName = ExtractForestName_(envName);
                    if (forestName == null)
                    {
                        Error.WriteLine($"***Environment {envName} has no forest name section");
                        return;
                    }
                }
            }

            var tasks = new List<Task>();
            foreach (var fileNode in envNodes)
            {
                tasks.Add(ProcessFileNode_(fileNode));
            }
            Task.WaitAll(tasks.ToArray());

            async Task ProcessFileNode_(XElement fileNode_)
            {
                var configName = fileNode_.Attribute("name").Value;
                Error.WriteLine(configName);

                var envName = ExtractEnvironmentName_(configName);
                var eopDcName = ExtractDcName_(envName);
                var forestName = GetForestName(envName);

                if (envName.StartsWithText("GTM"))
                {
                    Error.WriteLine($"Skipping {configName}");
                    return;
                }

                if (eopDcName == null)
                {
                    Error.WriteLine($"Skipping {envName} without EOP datacenter name");
                    return;
                }
                if (forestName == null)
                {
                    forestName = ExtractForestName_(envName);
                    if (forestName == null)
                    {
                        Error.WriteLine($"Skipping {envName} without forest name");
                        return;
                    }
                }

                var FieldFieldSeparatorChars = new[] { ',', ' ' };

                foreach (var node in fileNode_.Elements())
                {
                    foreach (var attr in node.Attributes())
                    {
                        if (attr.Name == "path") continue;

                        if (attr.Value.IndexOfAny(FieldFieldSeparatorChars) > 0)
                        {
                            var a = attr.Value.SplitWithoutEmpty(FieldFieldSeparatorChars);
                            foreach (var ipString in a)
                            {
                                await ProcessIpString_(ipString);
                            }
                        }
                        else
                        {
                            await ProcessIpString_(attr.Value);
                        }
                    }
                }

                async Task ProcessIpString_(string ipString_)
                {
                    // A valid IPv6 string must have at least 5 groups.
                    if (ipString_.Contains(':') &&
                        !ipString_.EndsWith("::") &&
                        ipString_.Count((c) => c == ':') <= 4) { return; }

                    // Skip any IP string that has any prefix in the exclusion list.
                    if (ipStringExclusionList.Any((text_) => ipString_.StartsWith(text_)))
                    {
                        Error.WriteLine($"Skipping IP string {ipString_}");
                        return;
                    }

                    lock (ipHotList)
                    {
                        if (ipHotList.Contains(ipString_))
                        {
                            return;
                        }
                        else
                        {
                            ipHotList.Add(ipString_);
                        }
                    }

                    var hasAnyMatch = false;

                    foreach (var entry in addressSpaceIdMap)
                    {
                        try
                        {
                            var addressSpace = entry.Key;
                            var record = await ValidateIpString(addressSpace, forestName, eopDcName, ipString_);
                            if (record.Status == ValidationStatus.NoMatch) continue;
                            if (record.Status == ValidationStatus.Unknown)
                            {
                                Error.WriteLine("Program logic error, got Unknown status!");
                                continue;
                            }

                            hasAnyMatch = true;

                            /*
                             * If the IP string has a parent prefix in this address space,
                             * then check if its environment should really be in it.
                             *
                             * */

                            if (record.Status != ValidationStatus.MultipleMatches &&
                                // Is cached success?
                                record != ValidationRecord.Success)
                            {
                                var mappedSpaceName = GetEnvSpaceName(envName) ?? "Default";

                                if (!addressSpace.IsSameTextAs(mappedSpaceName))
                                {
                                    var wrongSpaceRecord = new ValidationRecord(
                                        ValidationStatus.WrongAddressSpace,
                                        $"Should be in address space {mappedSpaceName}")
                                    {
                                        Id = record.Id,
                                        AddressSpace = record.AddressSpace,
                                        Environment = record.Environment,
                                        Forest = record.Forest,
                                        EopDcName = record.EopDcName,
                                        Prefix = record.Prefix,
                                        IpString = ipString_,
                                        IpamDcName = record.IpamDcName,
                                        Region = record.Region,
                                        Title = record.Title,
                                    };
                                    DumpValidationRecord(wrongSpaceRecord);
                                }
                            }

                            if (record.Status != ValidationStatus.Success)
                            {
                                DumpValidationRecord(record);
                            }
                        }
                        catch (Exception ex)
                        {
                            Error.WriteLine($"\r\n!!!{envName} {ipString_}:\r\n{ex}");
                        }
                    }

                    if (!hasAnyMatch)
                    {
                        var noMatchRecord = new ValidationRecord(ValidationStatus.NoMatch)
                        {
                            Forest = forestName,
                            EopDcName = eopDcName,
                            Environment = envName,
                            IpString = ipString_,
                        };
                        DumpValidationRecord(noMatchRecord);
                    }
                }
            }

            string ExtractEnvironmentName_(string filename_)
            {
                var index = filename_.LastIndexOf('.');
                if (index > 0) return filename_.Substring(0, index);
                return filename_;
            }

            string ExtractForestName_(string envName_)
            {
                var index = envName_.LastIndexOf('-');
                if (index > 0) return envName_.Substring(0, index);
                return null;
            }

            string ExtractDcName_(string envName_)
            {
                var index = envName_.LastIndexOf('-');
                if (index > 0) return envName_.Substring(index + 1);
                return null;
            }
        }

        private void DumpValidationRecord(ValidationRecord record)
        {
            WriteLine($"{record.AddressSpace},,{record.Environment},{record.Forest},{record.EopDcName},{record.IpString},{record.Prefix},{record.IpamDcName},{record.Region.ToCsvValue()},{record.Status},{record.Summary.ToCsvValue()},{record.Title.ToCsvValue()},{record.Id}");
        }

        private async Task<ValidationRecord> ValidateIpString(
            string addressSpace,
            string forestName,
            string eopDcName,
            string ipString)
        {
            var envName = $"{forestName}-{eopDcName}";
            var queryModel = AllocationQueryModel.Create(addressSpaceIdMap[addressSpace], ipString);
            queryModel.ReturnParentWhenNotFound = !ipString.Contains('/');
            queryModel.MaxResults = 1000;

            var queryResult = await this.IpamClient.QueryAllocationsAsync(queryModel);
            if (queryResult.Count == 0) return ValidationRecord.NoMatch;

            if (queryResult.Count > 1)
            {
                var summaryBuilder = new StringBuilder();

                foreach (var allocation in queryResult)
                {
                    allocation.Tags.TryGetValue(SpecialTags.Region, out var allocRegion);
                    allocation.Tags.TryGetValue(SpecialTags.PhysicalNetwork, out var network);
                    allocation.Tags.TryGetValue(SpecialTags.PropertyGroup, out var propertyGroup);

                    summaryBuilder.AppendFormat(
                        "Prefix: {0}, Region: {1}, Physical Network: {2}, Property Group: {3}",
                        allocation.Prefix, allocRegion, network, propertyGroup);
                    summaryBuilder.AppendLine();
                }
                return new ValidationRecord(
                    ValidationStatus.MultipleMatches,
                    summaryBuilder.ToString())
                {
                    AddressSpace = addressSpace,
                    Forest = forestName,
                    EopDcName = eopDcName,
                    Environment = envName,
                    IpString = ipString,
                };
            }

            var parent = queryResult[0];
            lock (prefixIdList)
            {
                if (prefixIdList.Contains(parent.Id)) return ValidationRecord.Success;
                prefixIdList.Add(parent.Id);
            }

            var prefix = parent.Prefix;
            parent.Tags.TryGetValue(SpecialTags.Title, out var title);
            parent.Tags.TryGetValue(SpecialTags.Datacenter, out var ipamDcName);
            parent.Tags.TryGetValue(SpecialTags.Region, out var region);

            var validationRecord = new ValidationRecord(ValidationStatus.Unknown)
            {
                Id = parent.Id,
                AddressSpace = addressSpace,
                Environment = envName,
                Forest = forestName,
                EopDcName = eopDcName,
                Prefix = prefix,
                IpString = ipString,
                IpamDcName = ipamDcName,
                Region = region,
                Title = title,
            };

            var isIPv6 = prefix.Contains(':');
            var prefixNumber = int.Parse(prefix.Substring(prefix.LastIndexOf('/') + 1));

            //
            // Skip large blocks
            //

            if (isIPv6) // IPv6
            {
                if (prefixNumber < 64)
                {
                    validationRecord.Status = ValidationStatus.Success;
                    validationRecord.Summary = "Large block";
                    return validationRecord;
                }
            }
            else // IPv4
            {
                if (prefixNumber < 23)
                {
                    validationRecord.Status = ValidationStatus.Success;
                    validationRecord.Summary = "Large block";
                    return validationRecord;
                }
            }

            if (string.IsNullOrWhiteSpace(ipamDcName))
            {
                validationRecord.Status = ValidationStatus.EmptyDatacenter;
                validationRecord.Summary = "Datacenter should not be empty";
                return validationRecord;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                if (!isIPv6 && prefixNumber == 32)
                {
                    validationRecord.Status = ValidationStatus.Obsolete;
                    validationRecord.Summary = "Should be deleted";
                    return validationRecord;
                }

                validationRecord.Status = ValidationStatus.EmptyTitle;
                validationRecord.Summary = "Title should not be empty";
                return validationRecord;
            }

            var azureDcName = GetAzureDcName(eopDcName);
            var suffix = GetDcSuffix(forestName);
            string normalizedDcName = ipamDcName.EndsWithText(suffix)
                ?
                ipamDcName.Substring(0, ipamDcName.Length - suffix.Length)
                :
                null;

            // Test if datacenter name matches any of EOP/mapped/normalized names.
            if (!(
                ipamDcName.IsSameTextAs(eopDcName) ||
                ipamDcName.IsSameTextAs(azureDcName) ||
                normalizedDcName.IsSameTextAs(eopDcName) ||
                normalizedDcName.IsSameTextAs(azureDcName)
                ))
            {
                // If not, check if this environment has an exception datacenter name.
                var dcExName = GetDcExceptionName(envName);

                if (!ipamDcName.IsSameTextAs(dcExName))
                {
                    validationRecord.Status = ValidationStatus.MismatchedDcName;
                    validationRecord.Summary = azureDcName == null || azureDcName.IsSameTextAs(eopDcName)
                        ?
                        $"Datacenter {ipamDcName} does not match EOP name {eopDcName}"
                        :
                        $"Datacenter {ipamDcName} does not match EOP name {eopDcName} or mapped Azure name {azureDcName}";
                    return validationRecord;
                }
            }

            /*
             * Build a map that contains 4 possible names: IPAM, EOP, Azure and
             * normalized. This will greatly simplify name checking by flattening
             * nested if-else blocks to a simple loop.
             *
             * */

            var dcNameMap = new StringMap();
            Func<string, bool?> dcNameMapcontains = (name_) =>
            {
                if (string.IsNullOrEmpty(name_)) return null;
                return dcNameMap.Values.Any((dcName_) => dcName_.IsSameTextAs(name_));
            };

            dcNameMap["datacenter"] = ipamDcName;
            if (dcNameMapcontains(eopDcName) == false)
            {
                dcNameMap["EOP"] = eopDcName;
            }
            if (dcNameMapcontains(azureDcName) == false)
            {
                dcNameMap["mapped Azure"] = azureDcName;
            }
            if (dcNameMapcontains(normalizedDcName) == false)
            {
                dcNameMap["normalized"] = normalizedDcName;
            }

            if (string.IsNullOrEmpty(region))
            {
                validationRecord.Status = ValidationStatus.EmptyRegion;
                validationRecord.Summary = "Region is empty";
                return validationRecord;
            }
            else
            {
                var regionMap = regionMaps[addressSpace];
                string mappedRegion = null;

                // Find mapped region in region map.
                foreach (var regionEntry in regionMap)
                {
                    if (ipamDcName.IsSameTextAs(regionEntry.Key))
                    {
                        mappedRegion = regionEntry.Value;
                        break;
                    }
                }

                if (!region.IsSameTextAs(mappedRegion))
                {
                    validationRecord.Status = ValidationStatus.InvalidRegion;
                    validationRecord.Summary = $"Region does not match implied region ({mappedRegion ?? "<none>"})";
                    return validationRecord;
                }
            }

            // Check if title contains datacenter name.
            var containsDcName = false;

            foreach (var entry in dcNameMap)
            {
                if (title.ContainsText(entry.Value))
                {
                    containsDcName = true;
                    break;
                }
            }

            /*
             * I'm not sure if this is helpful but I do observe some titles
             * contain EOP datacenter name by Azure name.
             *
             * */

            if (!containsDcName)
            {
                var dcNameList = GetEopNames(ipamDcName);
                if (dcNameList != null)
                {
                    foreach (var name in dcNameList)
                    {
                        if (title.ContainsText(name))
                        {
                            containsDcName = true;
                            break;
                        }
                    }
                }

                if (!containsDcName)
                {
                    // It's a little tricky to get a useful description...
                    var summaryBuilder = new StringBuilder();
                    summaryBuilder.AppendFormat("Title does not contain datacenter name {0}", ipamDcName);
                    foreach (var entry in dcNameMap)
                    {
                        if (entry.Key != "datacenter")
                        {
                            summaryBuilder.AppendFormat(" or {0} name {1}", entry.Key, entry.Value);
                        }
                    }

                    validationRecord.Status = ValidationStatus.InvalidTitle;
                    validationRecord.Summary = summaryBuilder.ToString();
                    return validationRecord;
                }
            }

            if (!title.ContainsText(forestName))
            {
                var containsAlias = false;
                var aliasList = GetForestAliases(forestName);

                if (aliasList?.Count > 0)
                {
                    foreach (var forestAlias in aliasList)
                    {
                        if (title.ContainsText(forestAlias))
                        {
                            containsAlias = true;
                            break;
                        }
                    }
                }

                if (!containsAlias)
                {
                    validationRecord.Status = ValidationStatus.InvalidTitle;
                    validationRecord.Summary = aliasList?.Count > 0
                        ?
                        $"Title does not contain forest name ({forestName} or any of aliases ({string.Join(",", aliasList)})"
                        :
                        $"Title does not contain forest name ({forestName})";
                    return validationRecord;
                }
            }

            /*
             * Check for wording issues in title.
             *
             * */

            foreach (var word in dubiousTitleWords)
            {
                if (title.ContainsText(word))
                {
                    validationRecord.Status = ValidationStatus.DubiousTitle;
                    validationRecord.Summary = $"Title contains text '{word}'";
                    return validationRecord;
                }
            }
            foreach (var pattern in dubiousTitlePatterns.Keys)
            {
                if (dubiousTitlePatterns[pattern].IsMatch(title))
                {
                    validationRecord.Status = ValidationStatus.DubiousTitle;
                    validationRecord.Summary = $"Title matches pattern '{pattern}'";
                    return validationRecord;
                }
            }

            validationRecord.Status = ValidationStatus.Success;
            validationRecord.Summary = "All good";
            return validationRecord;
        }
    }
}
