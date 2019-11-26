// ---------------------------------------------------------------------------
// <copyright file="IPConfigValidatorWorkflow.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------

namespace Microsoft.Office.Datacenter.Networking.Workflows.Monitors.IpamRanges
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.Office.Datacenter.AssemblyLine.Framework.Validation;
    using Microsoft.Office.Datacenter.CentralAdmin.Interop;
    using Microsoft.Office.Datacenter.CentralAdmin.Workflow.Framework;
    using Microsoft.Office.Datacenter.Networking.KustoFactory;
    using Microsoft.Office.Datacenter.Networking.VstsClient;
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared;
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared.Extensions;
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared.ProxyRoles;
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared.TimeProvider;
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
    using ValidationResultList = System.Collections.Generic.List<IPConfigValidatorWorkflow.Result>;

    [DataContract]
    [ProxyScript("Invoke-IPConfigValidatorWorkflow.ps1", Roles = new[] { typeof(NetworkingChangeAccessScriptsRole) })]
    public sealed class IPConfigValidatorWorkflow : Workflow
    {
        #region Inner Classes

        internal struct Result
        {
            internal string IPString;
            internal string ConfigTagName;

            /// <summary>
            /// For string.Join method.
            /// </summary>
            /// <returns>IP string.</returns>
            public override string ToString()
            {
                return this.IPString;
            }
        }

        /// <summary>
        /// A helper class that isolates the work of finding all tags that may
        /// contain valid IPv4/IPv6 srings.
        /// </summary>
        /// <remarks>
        /// An instance of this class can be reused.
        /// </remarks>
        private class IPTagFinder
        {
            private readonly StringBuilder sb = new StringBuilder();

            /// <summary>
            /// Finds all tags that contain valid IP strings.
            /// </summary>
            /// <param name="xml">XML text.</param>
            /// <returns>XML string</returns>
            /// <remarks>
            /// A valid XML document that has the following structure:
            ///     <result>
            ///         <TAG ATTRIBUTE="IP_STRING" />
            ///     </result>
            /// </remarks>
            internal string Find(string xml)
            {
                sb.Clear();
                var xd = XDocument.Parse(xml);
                sb.AppendLine("<result>");
                WalkElement(xd.Root);
                sb.AppendLine("</result>");
                return sb.ToString();
            }

            /// <summary>
            /// Recursively walks an XElement.
            /// </summary>
            /// <param name="node">The XElement to walk.</param>
            private void WalkElement(XElement node)
            {
                var list = ExamineElement(node);
                if (list.Count > 0)
                {
                    sb.AppendFormat($"<{node.Name}");
                    foreach (var attr in list)
                    {
                        sb.AppendFormat($"{attr.Name}=\"{attr.Value}\"");
                    }
                    sb.AppendFormat("/>");
                    sb.AppendLine();
                }
                foreach (var elem in node.Elements())
                {
                    WalkElement(elem);
                }
            }
                
            /// <summary>
            /// Examines an XElement for possible IP attribute values.
            /// </summary>
            /// <param name="element">The XElement object.</param>
            /// <returns>A list of XAttribute that contain IP strings.</returns>
            private List<XAttribute> ExamineElement(XElement element)
            {
                var list = new List<XAttribute>();
                foreach (var attr in element.Attributes())
                {
                    if (ValidateName(attr))
                    {
                        if (!attr.Value.StartsWith("0.0.0.0") &&
                            attr.Value != "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")
                        {
                            list.Add(attr);
                        }
                    }
                }
                return list;
            }

            /// <summary>
            /// Validates name and vlaue of an XAttribute's to verify if it contains valid IP strings.
            /// </summary>
            /// <param name="attr">The XAttribute object.</param>
            /// <returns>Boolean value.</returns>
            /// <remarks>
            /// This method does not use Regular Expressions. It verifies by examining
            /// if the attribute value contains certain characters. So far it works.
            /// </remarks>
            private bool ValidateName(XAttribute attr)
            {
                var name = attr.Name.LocalName;
                if (name.Contains("_IPV4") || name.Contains("_IPV6") ||
                    name.EndsWith("_IP") || name.Contains("_IP_"))
                {
                    if (!attr.Value.Contains("-") &&
                        (attr.Value.Contains(".") || attr.Value.Contains(":"))
                        )
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion

        #region Constants

        private static readonly char[] FieldSeprators = new[] { ',', ' ' };

        #endregion

        #region Workflow Parameters

        [DataMember(IsRequired = false)]

        public bool WhatIf { get; set; }

        #endregion

        #region Cached Variables

        IWorkflowRuntime cachedRuntime;

        #endregion

        #region Overrides

        private ValidationResultList output;

        protected override Continuation DoWork(IWorkflowRuntime runtime)
        {
            cachedRuntime = runtime;
            Process();
            OpenWorkitemIfNecessary();
            return Continuation.Default;
        }

        #endregion

        #region Methods

        private void Process()
        {
            var finder = new IPTagFinder();
            var asmF5 = typeof(FakedF5Class).Assembly;
            foreach (var rcName in asmF5.GetManifestResourceNames())
            {
                var rcXml = LoadResourceText(asmF5, rcName);
                var xd = XDocument.Load(finder.Find(rcXml));
                this.output = new ValidationResultList();

                foreach (var elem in xd.Elements())
                {
                    foreach (var attr in elem.Attributes())
                    {
                        if (attr.Name == "path") { continue; }
                        if (attr.Value.IndexOfAny(FieldSeprators) >= 0)
                        {
                            var a = attr.Value.Split(FieldSeprators, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var ipString in a)
                            {
                                ValidateIP(elem.Name.LocalName, ipString);
                            }
                        }
                        else
                        {
                            ValidateIP(elem.Name.LocalName, attr.Value);
                        }
                    }
                }
            }
        }

        void ValidateIP(string tagName, string ipString)
        {
            if (!ValidateIPOnKusto(ipString))
            {
                this.output.Add(new Result
                {
                    IPString = ipString,
                    ConfigTagName = tagName
                });

                cachedRuntime.Logger.LogInformation($"This IP range is not covered: {ipString}");
            }
        }

        /// <summary>
        /// Reads all text of a named resource.
        /// </summary>
        /// <param name="asm">Assembly instance which contains the named resource.</param>
        /// <param name="rcName">Resource name.</param>
        /// <returns>Resource content as string.</returns>
        private string LoadResourceText(Assembly asm, string rcName)
        {
            using (var sr = new StreamReader(asm.GetManifestResourceStream(rcName)))
            {
                return sr.ReadToEnd();
            }
        }

        #endregion

        #region Kusto Query

        private const string IpamDatabaseName = "IpamReport";

        private const string IpamKustoServerEndpoint = "https://ipam.kusto.windows.net";

        private const string IpamRangeWorkitemsTag = "IpamRange";

        private const string IpamWorkitemTitle = "Some XML Config IP ranges are not present in IPAM.";        
        
        private const string QueryFileName = "KustoIPQuery.txt";

        private string queryTemplate;
        private ICslQueryProvider kustoClient;

        /// <summary>
        /// Queries against Kusto to verify if an IP string is in use. 
        /// </summary>
        /// <param name="ipString">IP string to verify.</param>
        /// <returns>Boolean value.</returns>
        public bool ValidateIPOnKusto(string ipString)
        {
            if (this.kustoClient == null)
            {
                this.kustoClient =
                    cachedRuntime.HostServices.GetService(typeof(ICslQueryProvider)) as ICslQueryProvider ??
                    NetworkingKustoClientFactory.GetCslQueryProvider(
                        kustoDatabase: IpamDatabaseName,
                        kustoServer: IpamKustoServerEndpoint);
            }
            if (this.queryTemplate == null)
            {
                using (var sr = new StreamReader(this.GetType().Assembly.GetManifestResourceStream(QueryFileName)))
                {
                    this.queryTemplate = sr.ReadToEnd();
                }
            }

            var query = this.queryTemplate.Replace("{IPString}", ipString);
            var requestProperties = KustoClientExtensions
                .GetClientRequestPropertiesForApplication(cachedRuntime, "IPConfigValidatorWorkflow");
            var queryResult = this.kustoClient.ExecuteQueryAsync(query, requestProperties).Result;
            return queryResult.RecordsAffected > 0;
        }

        #endregion

        private void OpenWorkitemIfNecessary()
        {
            if (this.output.Any())
            {
                var vstsClient =
                    cachedRuntime.HostServices.GetService(typeof(IVstsClient)) as IVstsClient ??
                    CommonHelperMethods.GetExchangeVstsClient(cachedRuntime, Constants.MaxWorkItemsPerQueryFromVsts);

                var timeProvider = cachedRuntime.HostServices.GetService(typeof(ITimeProvider)) as ITimeProvider ?? new TimeProvider();
                DateTime utcNow = timeProvider.GetUtcNow();

                var activeWorkItems =
                    vstsClient.GetAllUnclosedWorkItems(
                        VstsRecipient.ExoNetworkAutomationEmailRecipient.AreaPath,
                        IpamRangeWorkitemsTag,
                        utcNow.AddDays(-14)).ToList();

                var activeWorkItem = activeWorkItems.FirstOrDefault(
                    w => w.Fields[VstsWorkItemFieldNames.TagsFieldName].ToString().Equals(IpamRangeWorkitemsTag));

                if (activeWorkItem == null)
                {
                    if (!this.WhatIf)
                    {
                        WorkItem newWorkItem = vstsClient.CreateWorkItem(
                                title: IpamWorkitemTitle,
                                areaPath: VstsRecipient.ExoNetworkAutomationEmailRecipient.AreaPath,
                                tags: IpamRangeWorkitemsTag,
                                priority: WorkItemPriority.Priority3,
                                projectGuid: VstsRecipient.ExoNetworkAutomationEmailRecipient.ProjectGuid,
                                reproSteps: string.Join($"<br/>", this.output));

                        cachedRuntime.Logger.LogInformation(
                            newWorkItem != null ? $"A workitem {newWorkItem.Id} with invalid IP ranges in XML configs was created." 
                                : "Failed to create a workitem with invalid IP ranges in XML configs.");
                    }
                    else
                    {
                        cachedRuntime.Logger.LogInformation("WhatIf is true, did not attempt to create a ticket.");
                    }
                }
                else
                {
                    cachedRuntime.Logger.LogInformation($"Active workitem for not covered IPAM ranges already exists with ID {activeWorkItem.Id}");
                }
            }
            else
            {
                cachedRuntime.Logger.LogInformation("No missing subnets were found, no workitem was created.");
            }
        }
    }

    class FakedF5Class
    { 
    }
}
