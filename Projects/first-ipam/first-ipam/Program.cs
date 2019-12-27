using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace first_ipam
{
    using Microsoft.Azure.Ipam.Client;
    using Microsoft.Azure.Ipam.Contracts;
    using static System.Console;
    using StringMap = Dictionary<string, string>;

    class Program
    {
        private StringMap addressSpaceIdMap = new StringMap {
            { "Default", SpecialAddressSpaces.DefaultAddressSpaceId },
            { "GalaCake", SpecialAddressSpaces.GalaCakeAddressSpaceId },
            { "EX", SpecialAddressSpaces.EXAddressSpaceId },
            { "RX", SpecialAddressSpaces.RXAddressSpaceId },
            };

        static void Main(string[] args)
        {
            new Program().Run(args);
        }

        void Run(string[] args)
        {
            var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);
            var ipamClient = new IpamClient(ipamClientSettings);
            //var addressSpaceId = ipamClientSettings.InitialAddressSpaceId;

            try
            {
                //QueryArgs_();

                DumpDatacenterRegionMap_().Wait();
                //DumpDatacenterTag_().Wait();
                //DumpAllSpaceTags_().Wait();
                //TestUpdatTags_().Wait();
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex);
            }

            Error.WriteLine("Hit ENTER to exit...");
            ReadLine();


            void QueryArgs_()
            {
                var addressSpace = "Default";

                foreach (var arg in args)
                {
                    if (!IsIpString(arg))
                    {
                        if (addressSpaceIdMap.ContainsKey(arg))
                        {
                            addressSpace = arg;
                            continue;
                        }
                        else
                        {
                            WriteLine($"Invalid address space name {arg}");
                            break;
                        }
                    }

                    Dump_(DoQuery_(addressSpace, arg).Result);
                }
            }

            bool IsIpString(string s_)
            {
                return s_.Contains(':') || s_.Contains('.');
            }

            AllocationQueryModel CreateQueryModel_(string addressSpace_, string ipString_)
            {
                var addressSpaceId = addressSpaceIdMap[addressSpace_];
                var queryModel = AllocationQueryModel.Create(addressSpaceId, ipString_);
                queryModel.ReturnParentWhenNotFound = !ipString_.Contains('/');
                queryModel.MaxResults = 1000;
                return queryModel;
            }

            async Task TestUpdatTags_()
            {
                var addressSpace = "Default";
                var targetPrefix = "207.46.34.207/32";
                var list = await DoQuery_(addressSpace, targetPrefix); ;
                Dump_(list);

                var alloc = list.Single();
                WriteLine($"Updating target: {targetPrefix}");
                alloc.Tags["Title"] = "ier01.mel01:Lo0 modified by v-chunly on " + DateTime.Now;
                alloc.Tags["Description"] = "***TEST*** modified by v-chunly on " + DateTime.Now;
                await ipamClient.UpdateAllocationTagsV2Async(alloc);

                Dump_(await DoQuery_(addressSpace, targetPrefix));
            }

            async Task<List<AllocationModel>> DoQuery_(string addressSpace_, string ipString_)
            {
                WriteLine($"Querying {ipString_} in {addressSpace_}");
                var queryModel = CreateQueryModel_(addressSpace_, ipString_);
                return await ipamClient.QueryAllocationsAsync(queryModel);
            }

            void Dump_(List<AllocationModel> allocations_)
            {
                foreach (var alloc in allocations_)
                {
                    WriteLine($"Prefix: {alloc.Prefix}");
                    WriteLine($"ID: {alloc.Id}");
                    WriteLine($"ETag: {alloc.ETag}");
                    WriteLine("Tags:");
                    foreach (var entry in alloc.Tags)
                    {
                        Console.WriteLine($"  {entry.Key}: {entry.Value}");
                    }
                    WriteLine();
                }
            }

            async Task DumpAllSpaceTags_()
            {
                WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                WriteLine("<AllTags>");
                foreach (var entry in addressSpaceIdMap)
                {
                    WriteLine($"<Space Name=\"{entry.Key}\">");

                    var tags = await ipamClient.GetTagsAsync(entry.Value);
                    tags.ForEach((tag_) => DumpTag_(tag_));

                    WriteLine("</Space>");
                }
                WriteLine("</AllTags>");
            }

            async Task DumpDatacenterTag_()
            {
                WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                WriteLine("<Map>");
                foreach (var entry in addressSpaceIdMap)
                {
                    WriteLine($"<Space Name=\"{entry.Key}\">");

                    var tag = await ipamClient.GetTagAsync(entry.Value, SpecialTags.Datacenter);
                    DumpTag_(tag);
                    WriteLine("</Space>");
                }
                WriteLine("</Map>");
            }

            async Task DumpDatacenterRegionMap_()
            {
                WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                WriteLine("<Map>");
                foreach (var entry in addressSpaceIdMap)
                {
                    WriteLine($"<Space Name=\"{entry.Key}\">");

                    var tag = await ipamClient.GetTagAsync(entry.Value, SpecialTags.Datacenter);

                    if (tag.ImpliedTags.TryGetValue(SpecialTags.Region, out var regionTag))
                    {
                        tag.KnownValues.ForEach((name_) =>
                        {
                            if (regionTag.TryGetValue(name_, out var region))
                            {
                                WriteLine($"<Item DCName=\"{name_}\" Region=\"{Xmlize_(region)}\" />");
                            }
                            else
                            {
                                Error.WriteLine($"{name_}=???");
                            }
                        });
                    }

                    WriteLine("</Space>");
                }
                WriteLine("</Map>");
            }

            void DumpTag_(TagModel tag_)
            {
                WriteLine($"<Tag Name=\"{tag_.Name}\">");
                WriteLine("<KnownValues>");
                foreach (var value in tag_.KnownValues)
                {
                    WriteLine($"<Value>{Xmlize_(value)}</Value>");
                }
                WriteLine("</KnownValues>");

                WriteLine("<ImpliedTags>");
                foreach (var tagEntry in tag_.ImpliedTags)
                {
                    WriteLine($"<ImpliedTag Name=\"{tagEntry.Key}\">");
                    foreach (var entry in tagEntry.Value)
                    {
                        WriteLine($"<Item Name=\"{entry.Key}\" Value=\"{Xmlize_(entry.Value)}\" />");
                    }
                    WriteLine($"</ImpliedTag>");
                }
                WriteLine("</ImpliedTags>");
                WriteLine("</Tag>");
            }
        }

        static string Xmlize_(string text_)
        {
            return text_?.Replace(">", "&gt;")
                .Replace("<", "&lt;")
                .Replace("\"", "&quot;")
                .Replace("&", "&amp;")
                .Replace("'", "&apos;");
        }
    }
}
