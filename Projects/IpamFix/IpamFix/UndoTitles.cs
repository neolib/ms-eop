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
    using StringMap = Dictionary<string, string>;

    class UndoTitles
    {
        internal void Run(string[] args)
        {
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

        async Task<bool> UpdateTitle(
            string addressSpace,
            string prefix,
            string prefixId,
            string newTitle,
            string description = null)
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
