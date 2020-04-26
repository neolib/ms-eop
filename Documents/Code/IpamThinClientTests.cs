// ---------------------------------------------------------------------------
// <copyright file="IpamThinClientTests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//
// THIS IS A TEMPORARY UNIT TEST, ONLY APPLICABLE ON MY OWN VM.
// WILL BE REMOVED WHEN THE PR IS COMMITTED INTO MASTER!!!
//
// ---------------------------------------------------------------------------
using System;

namespace Microsoft.Office.Datacenter.Networking.EopWorkflows.UnitTests.F5Deployment
{
    using Microsoft.Office.Datacenter.Networking.EopWorkflows.F5Deployment.Ipam;
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared.TimeProvider;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using static Console;

    //[TestClass]
    public sealed class IpamThinClientTests
    {
        private const string MyNewAllocationId = "2de807e4-4b8a-462c-8451-8c965022dda1";
        private const string MyNewAutoAllocationId = "84ae8cb9-07cc-4882-8391-83b87f456292";

        private static string Xmlize(string text_)
        {
            return text_?.Replace(">", "&gt;")
                .Replace("<", "&lt;")
                .Replace("\"", "&quot;")
                .Replace("&", "&amp;")
                .Replace("'", "&apos;");
        }

        private static void DumpTag(TagModel tagModel)
        {
            WriteLine($"<Tag Name=\"{tagModel.Name}\">");
            WriteLine("<KnownValues>");
            foreach (var value in tagModel.KnownValues)
            {
                WriteLine($"<Value>{Xmlize(value)}</Value>");
            }
            WriteLine("</KnownValues>");

            WriteLine("<ImpliedTags>");
            foreach (var tagEntry in tagModel.ImpliedTags)
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

        [TestMethod]
        public void TestGetTag()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);

            var tagModel = c.GetTagAsync(SpecialAddressSpaces.DefaultAddressSpaceId, SpecialTags.Datacenter).Result;
            DumpTag(tagModel);
        }

        [TestMethod]
        public void TestCreateAllocation()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            var alloc = new AllocationModel
            {
                AddressSpaceId = SpecialAddressSpaces.DefaultAddressSpaceId,
                Prefix = "10.0.123.0/28",
                CreatedBy = "IpamThinClient",
                ModifiedBy = "IpamThinClient",
                CreatedOn = new TimeProvider().GetUtcNow(),
                Id = MyNewAllocationId
            };
            alloc.Tags[SpecialTags.Title] = "IpamThinClient test";
            alloc.Tags[SpecialTags.RangeType] = "NETWORK";
            alloc.Tags[SpecialTags.NetworkType] = "PRIVATE";
            alloc.Tags[SpecialTags.PropertyGroup] = "FRB";
            alloc.Tags[SpecialTags.Datacenter] = "AM1";
            alloc.Tags[SpecialTags.Region] = "West Europe";

            c.CreateAllocationAsync(alloc).Wait();
            var resultAlloc = c.GetAllocationAsync(SpecialAddressSpaces.DefaultAddressSpaceId, alloc.Id).Result;
            WriteLine($"{resultAlloc.ParentId} {resultAlloc.Tags[SpecialTags.Region]}");
        }

        [TestMethod]
        public void TestCreateAllocationAuto()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            var tags = new Dictionary<string, string>
            {
                [SpecialTags.Title] = "IpamThinClient test auto creation",
                [SpecialTags.RangeType] = "NETWORK",
                [SpecialTags.PropertyGroup] = "FRB",
                [SpecialTags.Datacenter] = "AM1"
            };

            var alloc = c.CreateAllocationAutoAsync(
                new CreateAllocationAutoParams
                {
                    AddressSpaceId = SpecialAddressSpaces.DefaultAddressSpaceId,
                    ParentAllocationId = "90e019fe-9222-4626-a66b-bac21a85b646",  // 1501
                    PrefixLength = 23,
                    Tags = tags,
                    CreatedBy = nameof(IpamThinClient),
                    IsIPv4 = true
                }).Result;

            WriteLine($"{alloc.ParentId} {alloc.Id} {alloc.Prefix} {alloc.Tags[SpecialTags.Region]}");
        }

        [TestMethod]
        public void TestQueryAllocation()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            var queryModel = new AllocationQueryModel
            {
                AddressSpaceId = SpecialAddressSpaces.DefaultAddressSpaceId,
                Prefix = "40.95.58.0/23",
                //IsIPv4 = true,
            };
            //queryModel.RequiredTags[SpecialTags.Datacenter] = "AM1";
            //queryModel.RequiredTags[SpecialTags.PropertyGroup] = "FRB";

            var result = c.QueryAllocationsAsync(queryModel).Result;
            WriteLine($"{result.Count} allocation(s) found");
            foreach (var alloc in result)
            {
                WriteLine($"{alloc.Id} {alloc.Prefix} {alloc.Tags[SpecialTags.Datacenter]} {alloc.ModifiedBy} {alloc.ModifiedOn}");
            }
        }

        [TestMethod]
        public void TestSmartQuery()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            var queryModel = new SmartQueryModel
            {
                AddressSpaceId = SpecialAddressSpaces.DefaultAddressSpaceId,
                ParentId = "ea59f881-49a2-4996-9ecb-73f703b3b78c",
                MaxPrefixLength = 23,
                IsIPv4 = true,
                QueryPolicy = IpamQueryPolicies.FirstLevelOnly
            };
            queryModel.RequiredTags[SpecialTags.PropertyGroup] = "FRB";

            var prefixes = c.QuerySmartAllocationAsync(queryModel).Result;
            WriteLine($"{prefixes.Count} prefix(es) found");
            foreach (var prefix in prefixes)
            {
                WriteLine(prefix);
            }
        }

        [TestMethod]
        public void TestSmartQueryRegex()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            var queryModel = new SmartQueryRegexModel
            {
                AddressSpaceId = SpecialAddressSpaces.DefaultAddressSpaceId,
                ParentId = "90e019fe-9222-4626-a66b-bac21a85b646",
                IsIPv4 = true,
                //IncludeExistedPrefix = true,
                QueryPolicy = IpamQueryPolicies.FirstLevelOnly
            };
            // Tag regex only works with tags whose inheritance is unique.
            //queryModel.TagsRegex[SpecialTags.Description] = "test";

            var prefixes = c.QuerySmartAllocationByRegexAsync(queryModel).Result;
            WriteLine($"{prefixes.Count} prefixes found");
            foreach (var prefix in prefixes)
            {
                WriteLine(prefix);
            }
        }

        [TestMethod]
        public void TestGetAllocation()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            var alloc = c.GetAllocationAsync(SpecialAddressSpaces.DefaultAddressSpaceId, MyNewAllocationId).Result;
            if (alloc != null)
            {
                WriteLine($"{alloc.Id} {alloc.Prefix} {alloc.ModifiedBy} {alloc.ModifiedOn}");
            }
            else
            {
                WriteLine($"Allocation with ID {MyNewAllocationId} not found");
            }
        }

        [TestMethod]
        public void TestDeleteAllocation()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            c.DeleteAllocationAsync(SpecialAddressSpaces.DefaultAddressSpaceId, MyNewAllocationId).Wait();
        }

        [TestMethod]
        public void TestDeleteAllocationAuto()
        {
            var settings = new IpamClientSettings("Test");
            var c = new IpamThinClient(settings);
            c.DeleteAllocationAsync(SpecialAddressSpaces.DefaultAddressSpaceId, MyNewAutoAllocationId).Wait();
        }
    }
}
