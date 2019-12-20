using Microsoft.Azure.Ipam.Client;
using Microsoft.Azure.Ipam.Contracts;

using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace first_ipam
{
    using static System.Console;

    class Program
    {
        static void Main(string[] args)
        {
            var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);
            var ipamClient = new IpamClient(ipamClientSettings);
            var addressSpaceId = SpecialAddressSpaces.DefaultAddressSpaceId; //ipamClientSettings.InitialAddressSpaceId;

            try
            {
                foreach (var ipString in args) Dump_(DoQuery_(ipString).Result);
                TestUpdatTags_().Wait();
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex);
            }

            WriteLine("Hit ENTER to exit...");
            ReadLine();

            AllocationQueryModel CreateQueryModel_(string ipString_)
            {
                var queryModel = AllocationQueryModel.Create(addressSpaceId, ipString_);
                queryModel.ReturnParentWhenNotFound = !ipString_.Contains('/');
                queryModel.MaxResults = 1000;
                return queryModel;
            }

            async Task TestUpdatTags_()
            {
                var targetPrefix = "207.46.34.207/32";
                var list = await DoQuery_(targetPrefix); ;
                Dump_(list);

                var alloc = list.Single();
                WriteLine($"Updating target: {targetPrefix}");
                alloc.Tags["Description"] = "***TEST*** modified by v-chunly on " + DateTime.Now;
                await ipamClient.UpdateAllocationTagsV2Async(alloc);

                Dump_(await DoQuery_(targetPrefix));
            }

            async Task<List<AllocationModel>> DoQuery_(string ipString)
            {
                WriteLine($"Querying {ipString}");
                var queryModel = CreateQueryModel_(ipString);
                return await ipamClient.QueryAllocationsAsync(queryModel);
            }

            void Dump_(List<AllocationModel> allocations)
            {
                foreach (var alloc in allocations)
                {
                    WriteLine($"Prefix: {alloc.Prefix}");
                    WriteLine($"ID: {alloc.Id}");
                    WriteLine($"ETag: {alloc.ETag}");
                    WriteLine("Tags:");
                    foreach (var entry in alloc.Tags)
                    {
                        Console.WriteLine($"    {entry.Key}: {entry.Value}");
                    }
                    WriteLine();
                }
            }
        }
    }
}
