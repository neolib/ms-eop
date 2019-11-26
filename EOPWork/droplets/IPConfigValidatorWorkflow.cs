// ---------------------------------------------------------------------------
// <copyright file="IPConfigValidatorWorkflow.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------

namespace Microsoft.Office.Datacenter.Networking.Workflows.Monitors.IpamRanges
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.Office.Datacenter.AssemblyLine.Framework.Validation;
    using Microsoft.Office.Datacenter.CentralAdmin.Interop;
    using Microsoft.Office.Datacenter.CentralAdmin.Workflow.Framework;
    using Microsoft.Office.Datacenter.DropBox.Workflows;
    using Microsoft.Office.Datacenter.Networking.DeviceDriver.Firewall.Fortinet.Serialization;
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
    public sealed class IPConfigValidatorWorkflow : Workflow<ValidationResultList>
    {
        #region Inner Classes

        public struct Result
        {
            public string EnvName;
            public string IPString;
            public string ConfigTagName;
            public string Reason;

            /// <summary>
            /// For string.Join method.
            /// </summary>
            /// <returns>IP string.</returns>
            public override string ToString()
            {
                return $"{IPString} Reason: {Reason}";
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
            private XDocument result;

            /// <summary>
            /// Finds all tags that contain valid IP strings.
            /// </summary>
            /// <param name="xml">F5Automation config XML.</param>
            /// <returns>An XDocument object.</returns>
            /// <remarks>
            /// The returned XDocument has the following structure:
            ///     <result>
            ///         <TAG ATTRIBUTE="IP_STRING" />
            ///     </result>
            /// </remarks>
            internal XDocument Find(string xml)
            {
                var xd = XDocument.Parse(xml);
                result = new XDocument();
                result.Add(new XElement("result"));
                WalkNode(xd.Root);
                return result;
            }

            /// <summary>
            /// Recursively walks an XElement.
            /// </summary>
            /// <param name="node">The XElement to walk.</param>
            private void WalkNode(XElement node)
            {
                var list = ExamineNode(node);
                if (list.Count > 0)
                {
                    var ipNode = new XElement(node.Name);
                    result.Root.Add(ipNode);
                    foreach (var attr in list)
                    {
                        ipNode.Add(new XAttribute(attr.Name, attr.Value));
                    }
                }
                foreach (var child in node.Elements())
                {
                    WalkNode(child);
                }
            }

            /// <summary>
            /// Examines an XElement for possible IP attribute values.
            /// </summary>
            /// <param name="node">The XElement object.</param>
            /// <returns>A list of XAttribute that contain IP strings.</returns>
            private List<XAttribute> ExamineNode(XElement node)
            {
                var list = new List<XAttribute>();
                foreach (var attr in node.Attributes())
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

        /// <summary>
        /// The full path of the assembly file where XML config files resides as embedded resources.
        /// </summary>
        [DataMember(IsRequired = true)]
        public string XmlConfigAssemblyPath { get; set; }

        [DataMember(IsRequired = false)]
        public bool WhatIf { get; set; }

        #endregion

        #region Cached Variables

        IWorkflowRuntime cachedRuntime;

        #endregion

        #region Overrides

        private ValidationResultList resultList;

        public override ValidationResultList Output => resultList;

        protected override Continuation DoWork(IWorkflowRuntime runtime)
        {
            cachedRuntime = runtime;
            DoWork();
            OpenWorkItemIfNecessary();
            return Continuation.Default;
        }

        #endregion

        #region Methods

        private void DoWork()
        {
            var finder = new IPTagFinder();
            var asm = Assembly.LoadFrom(XmlConfigAssemblyPath);
            foreach (var rcName in asm.GetManifestResourceNames())
            {
                if (!rcName.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
                { continue; }

                var rcXml = LoadResourceText(asm, rcName);
                var ipXDoc = finder.Find(rcXml);
                var envName = Path.GetFileNameWithoutExtension(rcName);
                resultList = new ValidationResultList();

                foreach (var node in ipXDoc.Root.Elements())
                {
                    foreach (var attr in node.Attributes())
                    {
                        if (attr.Value.IndexOfAny(FieldSeprators) >= 0)
                        {
                            var a = attr.Value.Split(FieldSeprators, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var ipString in a)
                            {
                                ValidateIPOnKusto(envName, node.Name.LocalName, ipString);
                            }
                        }
                        else
                        {
                            ValidateIPOnKusto(envName, node.Name.LocalName, attr.Value);
                        }
                    }
                }
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

        private const string ProdDataKustoTableName = "Allocations_Default";

        private const string GallatinDataKustoTableName = "Allocations_Galacake";

        private static readonly string[] KustoDataTableNames = new[]
            {
                ProdDataKustoTableName, GallatinDataKustoTableName
            };

        private const string IpamWorkitemTitle = "Some XML Config IP ranges are not present in IPAM.";        
        
        private const string QueryFileName = "KustoIPQuery.txt";

        private string queryTemplate;
        private ICslQueryProvider kustoClient;

        /// <summary>
        /// Queries against Kusto to verify if an IP string is in use. 
        /// </summary>
        /// <param name="envName">Environment name.</param>
        /// <param name="tagName">Source XML tag name.</param>
        /// <param name="ipString">IP string to verify.</param>
        /// <returns>Boolean value.</returns>
        private bool ValidateIPOnKusto(string envName, string tagName, string ipString)
        {
            if (kustoClient == null)
            {
                kustoClient =
                    cachedRuntime.HostServices.GetService(typeof(ICslQueryProvider)) as ICslQueryProvider ??
                    NetworkingKustoClientFactory.GetCslQueryProvider(
                        kustoDatabase: IpamDatabaseName,
                        kustoServer: IpamKustoServerEndpoint);
            }
            if (queryTemplate == null)
            {
                queryTemplate = LoadResourceText(GetType().Assembly, QueryFileName);
            }

            var query = queryTemplate.Replace("{IPString}", ipString);
            foreach (var tableName in KustoDataTableNames)
            {
                if (!ValidateKustoQuery(tableName + query, envName, tagName, ipString))
                {
                    // If validation result is false, we've the final result, and
                    // there is no need to do further validation.
                    return false;
                }
            }
            return true;
        }

        private bool ValidateKustoQuery(string query, string envName, string tagName, string ipString)
        {
            var requestProperties = KustoClientExtensions
                .GetClientRequestPropertiesForApplication(cachedRuntime, "IPConfigValidatorWorkflow");
            var queryResult = kustoClient.ExecuteQueryAsync(query, requestProperties).Result;
            if (queryResult.Read())
            {
                var title = queryResult["Title"] as string;
                if (string.IsNullOrEmpty(title))
                {
                    resultList.Add(new Result
                    {
                        EnvName = envName,
                        IPString = ipString,
                        ConfigTagName = tagName,
                        Reason = "Empty title"
                    });
                    cachedRuntime.Logger.LogInformation($"IP range {ipString} has no title.");
                    return false;
                }
                else if (!title.Contains(envName))
                {
                    resultList.Add(new Result
                    {
                        EnvName = envName,
                        IPString = ipString,
                        ConfigTagName = tagName,
                        Reason = "Title has no environment name"
                    });
                    cachedRuntime.Logger.LogInformation($"Title of IP range {ipString} does not have environment name \"{envName}\"");
                    return false;
                }
                return true;
            }
            else
            {
                resultList.Add(new Result
                {
                    EnvName = envName,
                    IPString = ipString,
                    ConfigTagName = tagName,
                    Reason = "Not found in Kusto"
                });
                cachedRuntime.Logger.LogInformation($"IP range {ipString} not found in Kusto.");
                return false;
            }
        }

        #endregion

        private void OpenWorkItemIfNecessary()
        {
            if (resultList.Any())
            {
                var vstsClient = cachedRuntime.HostServices.GetService(typeof(IVstsClient)) as IVstsClient ??
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
                    if (!WhatIf)
                    {
                        WorkItem newWorkItem = vstsClient.CreateWorkItem(
                                title: IpamWorkitemTitle,
                                areaPath: VstsRecipient.ExoNetworkAutomationEmailRecipient.AreaPath,
                                tags: IpamRangeWorkitemsTag,
                                priority: WorkItemPriority.Priority3,
                                projectGuid: VstsRecipient.ExoNetworkAutomationEmailRecipient.ProjectGuid,
                                reproSteps: string.Join("<br/>", resultList));

                        cachedRuntime.Logger.LogInformation(
                            newWorkItem != null ? 
                                $"A workitem {newWorkItem.Id} with invalid IP ranges in XML configs was created."
                                :
                                "Failed to create a workitem with invalid IP ranges in XML configs.");
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
}
