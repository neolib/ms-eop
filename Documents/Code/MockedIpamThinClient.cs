// ---------------------------------------------------------------------------
// <copyright file="MockedIpamThinClient.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Office.Datacenter.Networking.EopWorkflows.UnitTests.F5Deployment
{
    using Microsoft.Office.Datacenter.Networking.EopWorkflows.F5Deployment;
    using Microsoft.Office.Datacenter.Networking.EopWorkflows.F5Deployment.Ipam;
    using StringList = List<string>;
    using StringListMap = Dictionary<string, List<string>>;
    using AllocationList = List<EopWorkflows.F5Deployment.Ipam.AllocationModel>;
    using AllocationMap = Dictionary<string, EopWorkflows.F5Deployment.Ipam.AllocationModel>;
    using AllocationListMap = Dictionary<string, List<EopWorkflows.F5Deployment.Ipam.AllocationModel>>;
    using TagList = List<EopWorkflows.F5Deployment.Ipam.TagModel>;

    class MockedIpamThinClient : IIpamThinClient
    {
        #region Mocked Data

        public TagList Tags { get; set; } = new TagList();

        public AllocationList Allocations { get; set; } = new AllocationList();

        public StringListMap SmartQueryResult { get; set; } = new StringListMap();

        public AllocationListMap AllocationsForQuery { get; set; } = new AllocationListMap();

        public AllocationMap AllocationsForAutoCreation { get; set; } = new AllocationMap();

        public AllocationList CreateAllocationSaveList { get; } = new AllocationList();

        #endregion

        #region IIpamThinClient

        public Task CreateAllocationAsync(AllocationModel allocation)
        {
            this.CreateAllocationSaveList.Add(allocation);
            return Task.CompletedTask;
        }

        public Task<AllocationModel> CreateAllocationAutoAsync(CreateAllocationAutoParams values)
        {
            return Task.FromResult(AllocationsForAutoCreation[values.GetDeepHashString()]);
        }

        public Task DeleteAllocationAsync(string addressSpaceId, string allocationId)
        {
            throw new NotImplementedException();
        }

        public Task<AllocationModel> GetAllocationAsync(string addressSpaceId, string allocationId)
        {
            return Task.FromResult(this.Allocations.First((alloc_) =>
                alloc_.AddressSpaceId == addressSpaceId && alloc_.Id == allocationId));
        }

        public Task<TagModel> GetTagAsync(string addressSpaceId, string tagName)
        {
            return Task.FromResult(this.Tags.First((tag_) =>
                tag_.AddressSpaceId == addressSpaceId && tag_.Name == tagName));
        }

        public Task<TagList> GetTagsAsync(string addressSpaceId)
        {
            return Task.FromResult(this.Tags);
        }

        public Task<AllocationList> QueryAllocationsAsync(AllocationQueryModel queryModel)
        {
            var hashString = queryModel.GetDeepHashString();
            return Task.FromResult(this.AllocationsForQuery[hashString]);
        }

        public Task<StringList> QuerySmartAllocationAsync(SmartQueryModel queryModel)
        {
            var hashString = queryModel.GetDeepHashString();
            return Task.FromResult(this.SmartQueryResult[hashString]);
        }

        #endregion
    }

    #region Private Extension Class

    /// <summary>
    /// This class is only intended to be used for F5 unit tests.
    /// </summary>
    static class ExtensionMethods
    {
        /// <summary>
        /// Convenient method to return textual representation of the object's deep hash.
        /// </summary>
        /// <param name="self">The source object.</param>
        /// <returns>Hash string.</returns>
        public static string GetDeepHashString(this object self)
        {
            return FormatBytes(self.ComputeDeepHash());
        }

        /// <summary>
        /// Computes deep hash of an object.
        /// </summary>
        /// <param name="instance">The source object.</param>
        /// <returns>Deep hash as a byte array.</returns>
        /// <remarks>
        /// This method traverses recursively into all properties of the source object, builds a
        /// textual representation of property names and values, then computes SHA-256 hash of the
        /// string and returns it.
        /// </remarks>
        public static byte[] ComputeDeepHash(this object self)
        {
            var builder = new StringBuilder();

            Traverse_(self);

            var textBytes = Encoding.UTF8.GetBytes(builder.ToString());
            var hasher = new SHA256Cng();
            return hasher.ComputeHash(textBytes);

            void Traverse_(object instance)
            {
                if (instance == null)
                {
                    // If it is not the root object, then output a blank new line to indicate null value.
                    if (builder.Length > 0) { builder.AppendLine(); }
                    return;
                }

                var type = instance.GetType();

                builder.AppendLine(type.FullName);
                if (type.IsPrimitive || instance is string)
                {
                    builder.AppendLine(instance.ToString());
                }

                var isEnumerable = instance is System.Collections.IEnumerable;

                if (isEnumerable)
                {
                    var it = ((System.Collections.IEnumerable)instance).GetEnumerator();
                    while (it.MoveNext())
                    {
                        Traverse_(it.Current);
                    }
                }

                foreach (var propInfo in type.GetPublicProperties())
                {
                    // This is empirical. Skip index properties with parameters.
                    // This works with known types (IDictionary/IList/IEnumerable) used by IPAM models.
                    if (propInfo.GetMethod.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    var value = propInfo.GetValue(instance);

                    builder.Append($"{propInfo.Name}=");
                    Traverse_(value);
                }
            }
        }

        public static string FormatBytes(byte[] bytes)
        {
            var builder = new StringBuilder();

            foreach (var @byte in bytes)
            {
                builder.Append(@byte.ToString("x2"));
            }

            return builder.ToString();
        }
    }

    #endregion
}
