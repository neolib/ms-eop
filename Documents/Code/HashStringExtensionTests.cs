// ---------------------------------------------------------------------------
// <copyright file="HashStringExtensionTests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Microsoft.Office.Datacenter.Networking.EopWorkflows.UnitTests.F5Deployment
{
    using Microsoft.Office.Datacenter.Networking.EopWorkflows.F5Deployment.Ipam;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class HashStringExtensionTests
    {
        [TestMethod]
        public void TestEqualHashes()
        {
            var obj1 = new SmartQueryModel
            {
                AddressSpaceId = SpecialAddressSpaces.DefaultAddressSpaceId,
                ParentId = "ea59f881-49a2-4996-9ecb-73f703b3b78c",
                ParentPrefix = "1.2.3.0/22",
                MaxPrefixLength = 28,
                IsIPv4 = true,
                QueryPolicy = IpamQueryPolicies.FirstLevelOnly,
            };
            var obj2 = new SmartQueryModel
            {
                AddressSpaceId = obj1.AddressSpaceId,
                ParentId = obj1.ParentId,
                ParentPrefix = obj1.ParentPrefix,
                MaxPrefixLength = obj1.MaxPrefixLength,
                IsIPv4 = obj1.IsIPv4,
                QueryPolicy = obj1.QueryPolicy
            };

            Action<Dictionary<string, string>> fillMap = (map_) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    map_["item" + i] = $"item{i}_value";
                }
            };

            fillMap(obj1.RequiredTags);
            fillMap(obj1.AdditionalTags);
            fillMap(obj1.ExcludedTags);

            fillMap(obj2.RequiredTags);
            fillMap(obj2.AdditionalTags);
            fillMap(obj2.ExcludedTags);

            Assert.AreEqual(obj1.GetDeepHashString(), obj2.GetDeepHashString());

            obj1.ParentPrefix = "1.2.2.0/23";
            Assert.AreNotEqual(obj1.GetDeepHashString(), obj2.GetDeepHashString());
        }
    }
}
