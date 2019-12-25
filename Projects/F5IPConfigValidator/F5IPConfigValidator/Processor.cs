using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace F5IPConfigValidator
{
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using static System.Console;
    using StringMap = Dictionary<string, string>;

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
        MismatchedDcName,   // Azure name does not match EOP name
        InvalidTitle,       // No datacenter/forest name in title
    }

    public class ValidationRecord
    {
        public string Id;
        public string AddressSpace;
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

    class Processor
    {
        internal IpamClient IpamClient { get; set; }
        private List<string> ipHotList = new List<string>();
        private List<string> prefixIdList = new List<string>();
        private StringMap datacenterNameMap = new StringMap();
        private StringMap forestAliasMap = new StringMap();
        private StringMap suffixNameMap = new StringMap();
        private StringMap forestNameMap = new StringMap();
        private StringMap envSpaceMap = new StringMap();
        private List<string> ipStringExclusionList = new List<string>();

        private StringMap addressSpaceIdMap = new StringMap {
            { "Default", SpecialAddressSpaces.DefaultAddressSpaceId },
            { "GalaCake", SpecialAddressSpaces.GalaCakeAddressSpaceId },
            { "EX", SpecialAddressSpaces.EXAddressSpaceId },
            { "RX", SpecialAddressSpaces.RXAddressSpaceId },
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
                    datacenterNameMap[eopName] = azureName;
                }

                foreach (var node in mapDoc.Root.Element("ForestAliases").Elements())
                {
                    var alias = node.Attribute("Name").Value;
                    var forestNames = node.Attribute("ForestNames").Value;
                    foreach (var forestName in forestNames.SplitWithoutEmpty(','))
                    {
                        forestAliasMap[forestName] = alias;
                    }
                }

                foreach (var node in mapDoc.Root.Element("DCSuffixes").Elements())
                {
                    var suffix = node.Attribute("Text").Value;
                    var forestNames = node.Attribute("ForestNames").Value;
                    foreach (var forestName in forestNames.SplitWithoutEmpty(','))
                    {
                        suffixNameMap[forestName] = suffix;
                    }
                }

                foreach (var node in mapDoc.Root.Element("EnvironmentSpaces").Elements())
                {
                    var spaceName = node.Attribute("SpaceName").Value;
                    var envNames = node.Attribute("EnvironmentNames").Value;
                    foreach (var envName in envNames.SplitWithoutEmpty(','))
                    {
                        envSpaceMap[envName] = spaceName;
                    }
                }

                foreach (var node in mapDoc.Root.Element("ExclusionList").Elements())
                {
                    var values = node.Attribute("StartsWith").Value;
                    foreach (var value in values.SplitWithoutEmpty(','))
                    {
                        ipStringExclusionList.Add(value);
                    }
                }
            }
        }

        private string GetAzureDcName(string eopName)
        {
            if (datacenterNameMap.TryGetValue(eopName.ToUpper(), out var name))
            { return name; }
            return null;
        }

        private string GetForestAlias(string forestName)
        {
            if (forestAliasMap.TryGetValue(forestName.ToUpper(), out var alias))
            { return alias; }
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

        /// <summary>
        /// Processes input XML file and checks against IPAM for invalid IP addresses.
        /// </summary>
        /// <param name="resultFile">Result XML file generated by the IpTagFinder applet.</param>
        /// <remarks>
        /// Output is in CSV format.
        /// </remarks>
        internal async Task Process(string resultFile)
        {
            // CSV header row
            WriteLine("Address Space,Comment,Environment,Forest,EOP DC,IP Query,Prefix,IPAM DC,Region,Status,Summary,Title,Prefix ID");

            //var debug = false;
            var ipStringSeparators = new[] { ',', ' ' };
            var xd = XDocument.Load(resultFile);

            LoadForestMap();
            LoadNameMap();
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

                // Need to normalize GTM environment name.
                if (envName.StartsWithText("GTM-"))
                {
                    envName = $"{forestName}-{eopDcName}";
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

                foreach (var node in fileNode_.Elements())
                {
                    foreach (var attr in node.Attributes())
                    {
                        if (attr.Name == "path") continue;
                        //if (debug) continue;
                        //else debug = true;

                        if (attr.Value.IndexOfAny(ipStringSeparators) > 0)
                        {
                            var a = attr.Value.SplitWithoutEmpty(ipStringSeparators);
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

                            // The following properties are not set in ValidateIpString.
                            record.Environment = envName;

                            /*
                             * If the IP string has a parent prefix in this address space,
                             * then check if its environment should really be in it.
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
                                        AddressSpace = record.AddressSpace,
                                        Environment = record.Environment,
                                        Forest = record.Forest,
                                        EopDcName = record.EopDcName,
                                        Id = record.Id,
                                        Prefix = record.Prefix,
                                        IpamDcName = record.IpamDcName,
                                        Region = record.Region,
                                        Title = record.Title,
                                    };
                                    DumpValidationRecord(wrongSpaceRecord, envName, ipString_);
                                }
                            }

                            if (record.Status != ValidationStatus.Success)
                            {
                                DumpValidationRecord(record, envName, ipString_);
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
                            EopDcName = eopDcName
                        };
                        DumpValidationRecord(noMatchRecord, envName, ipString_);
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

        private void DumpValidationRecord(ValidationRecord record, string envName, string ipString)
        {
            WriteLine($"{record.AddressSpace},,{envName},{record.Forest},{record.EopDcName},{ipString},{record.Prefix},{record.IpamDcName},{record.Region.ToCsvValue()},{record.Status},{record.Summary.ToCsvValue()},{record.Title.ToCsvValue()},{record.Id}");
        }

        private async Task<ValidationRecord> ValidateIpString(
            string addressSpace,
            string forestName,
            string eopDcName,
            string ipString)
        {
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
                    allocation.Tags.TryGetValue("Region", out var allocRegion);
                    allocation.Tags.TryGetValue("PhysicalNetwork", out var network);
                    summaryBuilder.AppendFormat(
                        "Prefix: {0}, Region: {1}, Physical Network: {2}",
                        allocation.Prefix, allocRegion, network);
                    summaryBuilder.AppendLine();
                }
                return new ValidationRecord(
                    ValidationStatus.MultipleMatches,
                    summaryBuilder.ToString())
                {
                    AddressSpace = addressSpace,
                    Forest = forestName,
                    EopDcName = eopDcName,
                };
            }

            var parent = queryResult[0];
            lock (prefixIdList)
            {
                if (prefixIdList.Contains(parent.Id)) return ValidationRecord.Success;
                prefixIdList.Add(parent.Id);
            }

            var prefix = parent.Prefix;
            parent.Tags.TryGetValue("Title", out var title);
            parent.Tags.TryGetValue("Datacenter", out var ipamDcName);
            parent.Tags.TryGetValue("Region", out var region);

            var validationRecord = new ValidationRecord(ValidationStatus.Unknown)
            {
                AddressSpace = addressSpace,
                Forest = forestName,
                EopDcName = eopDcName,
                Id = parent.Id,
                Prefix = prefix,
                IpamDcName = ipamDcName,
                Region = region,
                Title = title,
            };

            var prefixNumberString = prefix.Substring(prefix.LastIndexOf('/') + 1);

            if (int.TryParse(prefixNumberString, out var prefixNumber))
            {
                //
                // Skip large blocks
                //

                if (prefix.Contains(':')) // IPv6
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
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                if (prefixNumber == 32)
                {
                    validationRecord.Status = ValidationStatus.Obsolete;
                    validationRecord.Summary = "Should be deleted";
                    return validationRecord;
                }

                validationRecord.Status = ValidationStatus.EmptyTitle;
                validationRecord.Summary = "Title should not be empty";
                return validationRecord;
            }

            if (string.IsNullOrWhiteSpace(ipamDcName))
            {
                validationRecord.Status = ValidationStatus.EmptyDatacenter;
                validationRecord.Summary = "Datacenter should not be empty";
                return validationRecord;

            }

            var azureDcName = GetAzureDcName(eopDcName);
            var suffix = GetDcSuffix(forestName);
            string normalizedDcName = ipamDcName.EndsWithText(suffix)
                ?
                ipamDcName.Substring(0, ipamDcName.Length - suffix.Length)
                :
                null;

            if (!(
                ipamDcName.IsSameTextAs(eopDcName) ||
                ipamDcName.IsSameTextAs(azureDcName) ||
                normalizedDcName.IsSameTextAs(eopDcName) ||
                normalizedDcName.IsSameTextAs(azureDcName)
                ))
            {
                validationRecord.Status = ValidationStatus.MismatchedDcName;
                validationRecord.Summary = azureDcName == null || azureDcName.IsSameTextAs(eopDcName)
                    ?
                    $"Datacenter {ipamDcName} does not match EOP name {eopDcName}"
                    :
                    $"Datacenter {ipamDcName} does not match EOP name {eopDcName} or mapped Azure name {azureDcName}";
                return validationRecord;
            }

            /*
             * Build a map that contains 4 possible names: IPAM, EOP, Azure and
             * normalized. This will greatly simplify name checking by flattening
             * nested if-else blocks to a simple loop.
             *
             * */

            var dcNameMap = new StringMap();

            dcNameMap["datacenter"] = ipamDcName;
            if (StringMapContainsValueText(dcNameMap, eopDcName) == false)
            {
                dcNameMap["EOP"] = eopDcName;
            }
            if (StringMapContainsValueText(dcNameMap, azureDcName) == false)
            {
                dcNameMap["mapped Azure"] = azureDcName;
            }
            if (StringMapContainsValueText(dcNameMap, normalizedDcName) == false)
            {
                dcNameMap["normalized"] = normalizedDcName;
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

            var azureForestName = GetForestAlias(forestName);

            if (!(title.ContainsText(forestName) || title.ContainsText(azureForestName)))
            {
                validationRecord.Status = ValidationStatus.InvalidTitle;
                validationRecord.Summary = azureForestName == null
                    ?
                    $"Title does not contain forest name ({forestName})"
                    :
                    $"Title does not contain forest name ({forestName} or alias {azureForestName})";
                return validationRecord;
            }

            if (title.ContainsText("unknown"))
            {
                validationRecord.Status = ValidationStatus.InvalidTitle;
                validationRecord.Summary = "Title contains 'UNKNOWN' wording";
                return validationRecord;
            }

            validationRecord.Status = ValidationStatus.Success;
            validationRecord.Summary = "All good";
            return validationRecord;
        }

        /// <summary>
        /// Checks if a string already exists in values collection of a StringMap.
        /// </summary>
        /// <param name="map">A StringMap instance.</param>
        /// <param name="text">String to search for.</param>
        /// <returns>Tri-state boolean value.</returns>
        /// <remarks>
        /// You should compare the return value with true or false using
        /// the equality operator (==). Null return value means the search
        /// text is not valid.
        /// </remarks>
        bool? StringMapContainsValueText(StringMap map, string text)
        {
            if (string.IsNullOrEmpty(text)) { return null; }
            return map.Values.Any((name_) => name_.IsSameTextAs(text));
        }
    }
}
