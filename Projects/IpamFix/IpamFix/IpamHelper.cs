using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IpamFix
{
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using static Console;
    using StringMap = Dictionary<string, string>;
    using TagMap = Dictionary<string, Microsoft.Azure.Ipam.Contracts.TagModel>;

    public class IpamHelper
    {
        public static IpamClient IpamClient { get; set; }

        public static StringMap AddressSpaceIdMap = new StringMap {
            { "Default", SpecialAddressSpaces.DefaultAddressSpaceId },
            { "GalaCake", SpecialAddressSpaces.GalaCakeAddressSpaceId },
            { "EX", SpecialAddressSpaces.EXAddressSpaceId },
            { "RX", SpecialAddressSpaces.RXAddressSpaceId },
            };

        public static StringMap DatacenterNameMap { get; private set; }
        public static TagMap TagMap { get; private set; }

        public static void LoadMaps()
        {
            DatacenterNameMap = LoadNameMap();
            //TagMap = LoadIpamMaps().Result;
        }

        public static async Task<TagMap> LoadIpamMaps()
        {
            var map = new TagMap();

            foreach (var entry in AddressSpaceIdMap)
            {
                var dcTag = await IpamClient.GetTagAsync(entry.Value, SpecialTags.Datacenter);
                map[entry.Key] = dcTag;
            }
            return map;
        }

        public static StringMap LoadNameMap()
        {
            var myType = typeof(IpamHelper);
            var rcName = myType.Namespace + ".Files.NameMap.xml";
            var nameMap = new StringMap();

            using (var rcs = myType.Assembly.GetManifestResourceStream(rcName))
            {
                var mapDoc = XDocument.Load(rcs);

                foreach (var node in mapDoc.Root.Element("DCNames").Elements())
                {
                    var eopName = node.Attribute("EOPName").Value;
                    var azureName = node.Attribute("AzureName").Value;
                    nameMap[eopName] = azureName;
                }
            }
            return nameMap;
        }

        public static async Task<AllocationModel> QueryIpam(string addressSpace, string prefix, string prefixId)
        {
            var queryModel = AllocationQueryModel.Create(AddressSpaceIdMap[addressSpace], prefix);
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

        public static async Task<bool> UpdateTitle(string addressSpace, string prefix, string prefixId,
            string newTitle, string description = null)
        {
            var allocation = await QueryIpam(addressSpace, prefix, prefixId);
            if (allocation != null)
            {
                allocation.ModifiedOn = DateTime.Now;
                allocation.Tags[SpecialTags.Title] = newTitle;
                if (description != null) allocation.Tags[SpecialTags.Description] = description;
                await IpamClient.UpdateAllocationTagsV2Async(allocation);
                return true;
            }
            return false;
        }

        public static async Task<string> UpdateDatacenter(string addressSpace, string prefix, string prefixId, string eopDcName)
        {
            if (!DatacenterNameMap.TryGetValue(eopDcName, out var newDcName))
            {
                newDcName = eopDcName;
                WriteLine($"EOP datacenter {eopDcName} has no azure mapping");
            }

            var allocation = await QueryIpam(addressSpace, prefix, prefixId);
            if (allocation != null)
            {
                allocation.ModifiedOn = DateTime.Now;
                allocation.Tags.Clear();
                allocation.Tags[SpecialTags.Datacenter] = newDcName;
                await IpamClient.PatchAllocationTagsV2Async(allocation);
                return newDcName;
            }
            return null;
        }

        public static async Task<string> UpdateRegion(string addressSpace, string prefix, string prefixId, string ipamDcName)
        {
            var regionMap = TagMap[addressSpace].ImpliedTags[SpecialTags.Region];
            if (!regionMap.TryGetValue(ipamDcName, out var region))
            {
                WriteLine($"Datacenter {ipamDcName} has no region mapping");
                if (ipamDcName == "PUS01") region = "Korea South 2";
                else return null;
            }

            var allocation = await QueryIpam(addressSpace, prefix, prefixId);
            if (allocation != null)
            {
                allocation.Tags.Clear();
                allocation.Tags[SpecialTags.Region] = region;
                await IpamClient.PatchAllocationTagsV2Async(allocation);
                return region;
            }
            return null;
        }
    }
}
