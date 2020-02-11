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
    using System.Collections.Specialized;
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
        private IpamClient ipamClient;

        static void Main(string[] args)
        {
            new Program().Run(args);
        }

        void Run(string[] args)
        {
            var sectionName = "IpamClientSettings";
            if (args.Length > 0)
            {
                var arg = args[0];
                if (arg.StartsWith(sectionName))
                {
                    sectionName = arg;
                    var newArgs = new string[args.Length - 1];
                    Array.Copy(args, 1, newArgs, 0, newArgs.Length);
                    args = newArgs;
                }
            }

            WriteLine($"Using {sectionName}");
            var settings = ConfigurationManager.GetSection(sectionName) as NameValueCollection;
            try
            {
                var ipamClientSettings = new IpamClientSettings(settings);
                ipamClient = new IpamClient(ipamClientSettings);

                QueryArgs(args);
                //TestUpdateDatacenterTags().Wait();
                //DumpDatacenterRegionMap().Wait();
                //DumpDatacenterTag().Wait();
                //DumpAllSpaceTags().Wait();
                //TestUpdateTitleTags().Wait();
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex);
            }

            Error.WriteLine("Hit ENTER to exit...");
            ReadLine();
        }

        void QueryArgs(string[] args)
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

                Dump(DoQuery(addressSpace, arg).Result);
            }
        }

        bool IsIpString(string s_)
        {
            return s_.Contains(':') || s_.Contains('.');
        }

        AllocationQueryModel CreateQueryModel(string addressSpace_, string ipString_)
        {
            var addressSpaceId = addressSpaceIdMap[addressSpace_];
            var queryModel = AllocationQueryModel.Create(addressSpaceId, ipString_);
            queryModel.ReturnParentWhenNotFound = !ipString_.Contains('/');
            queryModel.MaxResults = 1000;
            // Possible use of RequiredTags:
            //queryModel.RequiredTags[SpecialTags.Datacenter] = "AM1";

            return queryModel;
        }

        async Task TestUpdateTitleTags()
        {
            var addressSpace = "Default";
            var targetPrefix = "207.46.34.207/32";
            var list = await DoQuery(addressSpace, targetPrefix); ;
            Dump(list);

            var alloc = list.Single();
            WriteLine($"Updating target: {targetPrefix}");
            alloc.Tags[SpecialTags.Title] = "ier01.mel01:Lo0 modified by v-chunly on " + DateTime.Now;
            alloc.Tags[SpecialTags.Description] = "***TEST*** modified by v-chunly on " + DateTime.Now;
            await ipamClient.UpdateAllocationTagsV2Async(alloc);

            Dump(await DoQuery(addressSpace, targetPrefix));
        }

        async Task TestUpdateDatacenterTags()
        {
            var addressSpace = "Default";
            var targetPrefix = "10.40.122.0/23";
            var list = await DoQuery(addressSpace, targetPrefix); ;
            Dump(list);

            var alloc = list.Single();
            WriteLine($"Updating target: {targetPrefix}");
            alloc.Tags.Clear();
            alloc.Tags[SpecialTags.Datacenter] = "CYG01";
            await ipamClient.PatchAllocationTagsV2Async(alloc);

            Dump(await DoQuery(addressSpace, targetPrefix));
        }

        async Task<List<AllocationModel>> DoQuery(string addressSpace_, string ipString_)
        {
            WriteLine($"Querying {ipString_} in {addressSpace_}");
            var queryModel = CreateQueryModel(addressSpace_, ipString_);
            return await ipamClient.QueryAllocationsAsync(queryModel);
        }

        void Dump(List<AllocationModel> allocations_)
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

        async Task DumpAllSpaceTags()
        {
            WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            WriteLine("<AllTags>");
            foreach (var entry in addressSpaceIdMap)
            {
                WriteLine($"<Space Name=\"{entry.Key}\">");

                var tags = await ipamClient.GetTagsAsync(entry.Value);
                tags.ForEach((tag_) => DumpTag(tag_));

                WriteLine("</Space>");
            }
            WriteLine("</AllTags>");
        }

        async Task DumpDatacenterTag()
        {
            WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            WriteLine("<Map>");
            foreach (var entry in addressSpaceIdMap)
            {
                WriteLine($"<Space Name=\"{entry.Key}\">");

                var tag = await ipamClient.GetTagAsync(entry.Value, SpecialTags.Datacenter);
                DumpTag(tag);
                WriteLine("</Space>");
            }
            WriteLine("</Map>");
        }

        async Task DumpDatacenterRegionMap()
        {
            WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            WriteLine("<Map>");
            foreach (var entry in addressSpaceIdMap)
            {
                WriteLine($"<Space Name=\"{entry.Key}\">");

                var tag = await ipamClient.GetTagAsync(entry.Value, SpecialTags.Datacenter);

                if (tag.ImpliedTags.TryGetValue(SpecialTags.Region, out var regionMap))
                {
                    tag.KnownValues.ForEach((name_) =>
                    {
                        if (regionMap.TryGetValue(name_, out var region))
                        {
                            WriteLine($"<Item DCName=\"{name_}\" Region=\"{Xmlize(region)}\" />");
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

        void DumpTag(TagModel tag_)
        {
            WriteLine($"<Tag Name=\"{tag_.Name}\">");
            WriteLine("<KnownValues>");
            foreach (var value in tag_.KnownValues)
            {
                WriteLine($"<Value>{Xmlize(value)}</Value>");
            }
            WriteLine("</KnownValues>");

            WriteLine("<ImpliedTags>");
            foreach (var tagEntry in tag_.ImpliedTags)
            {
                WriteLine($"<ImpliedTag Name=\"{tagEntry.Key}\">");
                foreach (var entry in tagEntry.Value)
                {
                    WriteLine($"<Item Name=\"{entry.Key}\" Value=\"{Xmlize(entry.Value)}\" />");
                }
                WriteLine($"</ImpliedTag>");
            }
            WriteLine("</ImpliedTags>");
            WriteLine("</Tag>");
        }

        static string Xmlize(string text_)
        {
            return text_?.Replace(">", "&gt;")
                .Replace("<", "&lt;")
                .Replace("\"", "&quot;")
                .Replace("&", "&amp;")
                .Replace("'", "&apos;");
        }
    }
}
