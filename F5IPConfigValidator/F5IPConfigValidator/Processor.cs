using Microsoft.Azure.Ipam.Client;
using Microsoft.Azure.Ipam.Contracts;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace F5IPConfigValidator
{
    using static System.Console;
    using StringMap = Dictionary<string, string>;

    public enum ValidationStatus
    {
        Unknown,
        Success,
        NoMatch,            // No matching record found in Kusto
        Obsolete,           // Should be removed in config
        NoMappingDcName,    // EOP datacenter name has no mapping Azure name
        EmptyTitle,
        EmptyDatacenter,
        MismatchedDcName,   // Azure name does not match EOP name
        InvalidTitle,       // No environment/DC name in title
    }

    public class ValidationRecord
    {
        public string AddressSpace;
        public string Prefix;
        public string Datacenter;
        public string Region;
        public string Title;
        public string Summary;
        public ValidationStatus Status;

        public ValidationRecord(ValidationStatus status, string summary = null)
        {
            this.Status = status;
            this.Summary = summary;
        }

        public static ValidationRecord NoMatch => new ValidationRecord(ValidationStatus.NoMatch);
        public static ValidationRecord Success => new ValidationRecord(ValidationStatus.Success);
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

        private StringMap addressSpaceIds = new StringMap {
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

        private string GetForestName(string envName)
        {
            if (forestNameMap.TryGetValue(envName.ToUpper(), out var name))
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
            WriteLine("Address Space,Comment,Envionment,IP Query,Prefix,Datacenter,Region,Status,Summary,Title");

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
                var index = envName.LastIndexOf('-');
                if (index > 0)
                {
                    var eopDcName = envName.Substring(index + 1);

                    var azureName = GetAzureDcName(eopDcName);
                    if (azureName == null)
                    {
                        var summary = $"EOP datacenter name {eopDcName} has no Azure name";
                        WriteLine($",,{envName},Static name check,,,,,{ValidationStatus.NoMappingDcName},{summary}");
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
                if (forestName == null)
                {
                    Error.WriteLine($"***Config name {configName} has no mapped forest name.");
                    forestName = ExtractForestName_(envName);
                    if (forestName == null)
                    {
                        Error.WriteLine($"***Config name {configName} has no forest name.");
                        return;
                    }
                }
                if (eopDcName == null)
                {
                    Error.WriteLine($"***Config name {configName} has no datacenter name.");
                    return;
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

                    foreach (var entry in addressSpaceIds)
                    {
                        try
                        {
                            var result = await ValidateIpString(forestName, eopDcName, ipString_, entry.Value);
                            result.AddressSpace = entry.Key;
                            if (result.Status != ValidationStatus.Success)
                            {
                                WriteLine($"{result.AddressSpace},,{envName},{ipString_},{result.Prefix},{result.Datacenter},{result.Region.ToCsvValue()},{result.Status},{result.Summary.ToCsvValue()},{result.Title.ToCsvValue()}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Error.WriteLine($"\r\n!!!{envName} {ipString_}:\r\n{ex}");
                        }
                    }
                }
            }

            string ExtractEnvironmentName_(string filename_)
            {
                var index = filename_.LastIndexOf('.');
                if (index > 0) return filename_.Substring(0, index);
                return filename_;
            }

            string ExtractForestName_(string envName)
            {
                var index = envName.LastIndexOf('-');
                if (index > 0) return envName.Substring(0, index);
                return null;
            }

            string ExtractDcName_(string envName)
            {
                var index = envName.LastIndexOf('-');
                if (index > 0) return envName.Substring(index + 1);
                return null;
            }
        }

        async Task<ValidationRecord> ValidateIpString(
            string forestName,
            string eopDcName,
            string ipString,
            string addressSpaceId)
        {
            var queryModel = AllocationQueryModel.Create(addressSpaceId, ipString);
            queryModel.ReturnParentWhenNotFound = !ipString.Contains('/');
            queryModel.MaxResults = 1000;

            var queryResult = await this.IpamClient.QueryAllocationsAsync(queryModel);
            var parent = queryResult.FirstOrDefault();
            if (parent == null) return ValidationRecord.NoMatch;

            lock (prefixIdList)
            {
                if (prefixIdList.Contains(parent.Id)) return ValidationRecord.Success;
                prefixIdList.Add(parent.Id);
            }

            var prefix = parent.Prefix;
            parent.Tags.TryGetValue("Title", out var title);
            parent.Tags.TryGetValue("Datacenter", out var ipamDcName);
            parent.Tags.TryGetValue("Region", out var region);

            if (int.TryParse(
                prefix.Substring(prefix.LastIndexOf('/') + 1),
                out var prefixNumber))
            {
                // Skip large blocks.

                if (prefix.Contains(':')) // IPv6
                {
                    if (prefixNumber < 64)
                    {
                        return ValidationRecord.Success;
                    }
                }
                else // IPv4
                {
                    if (prefixNumber < 23)
                    {
                        return ValidationRecord.Success;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                if (prefixNumber == 32)
                {
                    return new ValidationRecord(
                        ValidationStatus.Obsolete,
                        "Should be deleted")
                    {
                        Prefix = prefix,
                        Datacenter = ipamDcName,
                        Region = region,
                        Title = title,
                    };
                }

                return new ValidationRecord(
                    ValidationStatus.EmptyTitle,
                    "Title should not be empty")
                {
                    Prefix = prefix,
                    Datacenter = ipamDcName,
                    Region = region,
                    Title = title,
                };
            }

            if (string.IsNullOrWhiteSpace(ipamDcName))
            {
                return new ValidationRecord(
                    ValidationStatus.EmptyDatacenter,
                    "Datacenter tag should not be empty")
                {
                    Prefix = prefix,
                    Datacenter = ipamDcName,
                    Region = region,
                    Title = title,
                };
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
                return new ValidationRecord(
                    ValidationStatus.MismatchedDcName,
                    azureDcName == null
                    ?
                    $"Datacenter {ipamDcName} does not match EOP name {eopDcName}"
                    :
                    $"Datacenter {ipamDcName} does not match EOP name {eopDcName} or mapped Azure name {azureDcName}"
                    )
                {
                    Prefix = prefix,
                    Datacenter = ipamDcName,
                    Region = region,
                    Title = title,
                };
            }

            /*
             * Build a map that contains 3 possible names: IPAM, EOP and Azure.
             * This will greatly simplify name checking by flattening nested
             * if-else blocks to a simple loop.
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

                return new ValidationRecord(
                    ValidationStatus.InvalidTitle,
                    summaryBuilder.ToString())
                {
                    Prefix = prefix,
                    Datacenter = ipamDcName,
                    Region = region,
                    Title = title,
                };
            }

            if (!(forestName.StartsWithText("gtm-") || forestName.IsSameTextAs("gtm")))
            {
                var azureForestName = GetForestAlias(forestName);

                if (!(title.ContainsText(forestName) || title.ContainsText(azureForestName)))
                {
                    return new ValidationRecord(
                        ValidationStatus.InvalidTitle,
                        azureForestName == null
                        ?
                        $"Title does not contain forest name ({forestName})"
                        :
                        $"Title does not contain forest name ({forestName} or alias {azureForestName})"
                        )
                    {
                        Prefix = prefix,
                        Datacenter = ipamDcName,
                        Region = region,
                        Title = title,
                    };
                }
            }
            return ValidationRecord.Success;
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
