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
    using System.Net.Mail;
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
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared.Email;
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared.Extensions;
    using Microsoft.Office.Datacenter.Networking.Workflows.Shared.ProxyRoles;
    using ValidationRecordList = System.Collections.Generic.List<IPConfigValidatorWorkflow.ValidationRecord>;

    [DataContract]
    [ProxyScript("Invoke-IPConfigValidatorWorkflow.ps1", Roles = new[] { typeof(NetworkingChangeAccessScriptsRole) })]
    public sealed class IPConfigValidatorWorkflow : Workflow<ValidationRecordList>
    {
        #region Inner Classes

        public enum ValidationStatus
        {
            Unknown,
            Success,
            NoMatch,        // No matching record found in Kusto
            EmptyTitle,
            InvalidTitle,   // No environment name in title
        }

        public struct ValidationRecord
        {
            public string EnvName;
            public string IPString;
            public string ConfigTagName;
            public ValidationStatus Status;
        }

        /// <summary>
        /// A helper class that isolates the work of finding all tags that may
        /// contain valid IPv4/IPv6 strings.
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
            /// where capitalized words are placeholders.
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

        [DataMember(IsRequired = false)]
        public string NotificationRecipientEmail { get; set; } = "v-chunly@microsoft.com";

        [DataMember(IsRequired = false)]
        public bool SendNotfication { get; set; } = true;

        #endregion

        #region Cached Variables

        IWorkflowRuntime cachedRuntime;

        #endregion

        #region Overrides

        private ValidationRecordList resultList;

        public override ValidationRecordList Output => resultList;

        protected override Continuation DoWork(IWorkflowRuntime runtime)
        {
            cachedRuntime = runtime;
            DoWork();
            SendEmailNotification();
            return Continuation.Default;
        }

        #endregion

        #region Methods

        private void DoWork()
        {
            var finder = new IPTagFinder();
            var asm = typeof(F5Fake).Assembly;
            foreach (var rcName in asm.GetManifestResourceNames())
            {
                if (!rcName.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
                { continue; }

                var rcXml = LoadResourceText(asm, rcName);
                var ipXDoc = finder.Find(rcXml);
                var envName = Path.GetFileNameWithoutExtension(rcName);
                resultList = new ValidationRecordList();

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

        #region Constatants

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

        #endregion

        #region Cached Variables

        private string queryTemplate;
        private ICslQueryProvider kustoClient;

        #endregion

        /// <summary>
        /// Queries against Kusto to verify if an IP string is in use. 
        /// </summary>
        /// <param name="envName">Environment name.</param>
        /// <param name="tagName">Source XML element tag name.</param>
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
            ValidationStatus validationStatus = ValidationStatus.Unknown;
            foreach (var tableName in KustoDataTableNames)
            {
                validationStatus = ValidateKustoQuery(tableName + query, envName);

                // If found, then all good.
                if (validationStatus == ValidationStatus.Success) { return true; }
                
                // If no match, continue on next Kusto table...
                if (validationStatus == ValidationStatus.NoMatch) { continue; } 
            }

            resultList.Add(new ValidationRecord
            {
                EnvName = envName,
                IPString = ipString,
                ConfigTagName = tagName,
                Status = validationStatus
            });
            if (validationStatus == ValidationStatus.NoMatch)
            {
                cachedRuntime.Logger.LogInformation($"IP range {ipString} not found in Kusto.");
            }
            else if (validationStatus == ValidationStatus.EmptyTitle)
            {
                cachedRuntime.Logger.LogInformation($"IP range {ipString} has no title.");
            }
            else if (validationStatus == ValidationStatus.InvalidTitle)
            {
                cachedRuntime.Logger.LogInformation($"Title of IP range {ipString} does not have environment name \"{envName}\"");
            }
            else
            {
                cachedRuntime.Logger.LogInformation($"Internal logic error: \"{validationStatus}\" not handled");
            }
            return false;
        }

        /// <summary>
        /// Validates an IP string against Kusto via a prepared query.
        /// </summary>
        /// <param name="query">Prepared Kusto query.</param>
        /// <param name="envName">Environment name.</param>
        /// <returns>A ValidationStatus value.</returns>
        private ValidationStatus ValidateKustoQuery(string query, string envName)
        {
            var requestProperties = KustoClientExtensions
                .GetClientRequestPropertiesForApplication(cachedRuntime, "IPConfigValidatorWorkflow");
            var queryResult = kustoClient.ExecuteQueryAsync(query, requestProperties).Result;
            if (queryResult.Read())
            {
                var title = queryResult["Title"] as string;
                if (string.IsNullOrEmpty(title))
                {
                    return ValidationStatus.EmptyTitle;
                }
                else if (!title.Contains(envName))
                {
                    return ValidationStatus.InvalidTitle;
                }
                return ValidationStatus.Success;
            }
            else
            {
                return ValidationStatus.NoMatch;
            }
        }

        #endregion

        #region Notification

        private void SendEmailNotification()
        {
            if (SendNotfication)
            {
                string subject;
                string body;

                if (Output.Any())
                {
                    subject = IpamWorkitemTitle;
                    body = BuildEmailBody();
                }
                else
                {
                    subject = "XML Config IP ranges validation: success";
                    body = "No invalid IP ranges found.";
                }

                var client = cachedRuntime.HostServices
                            .GetService(typeof(IEmailClient)) as IEmailClient ?? new EmailClient();
                try
                {
                    client.SendMail(
                        workflowRuntime: cachedRuntime,
                        toAddresses: new MailAddressCollection { NotificationRecipientEmail },
                        subject: subject,
                        htmlContent: body);
                    /* ccAddresses: new MailAddressCollection { Constants.AutomationNotificationEmailAddress }); */
                }
                catch (Exception exception)
                {
                    cachedRuntime.Logger.LogWarning(
                        "Failed to send email notification. Subject: {0}, Body: {1}. Exception: {2}",
                        subject,
                        body,
                        exception.Message);
                }
            }
        }

        private string BuildEmailBody()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>IP</th><th>Enviroment</th><th>Status</th></tr>");
            foreach (var record in Output)
            {
                sb.AppendLine("<tr>");
                sb.AppendFormat(
                    "<td>{0}</td><td>{1}</td><td>{2}</td>",
                    record.IPString,
                    record.EnvName,
                    record.Status);
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            return sb.ToString();
        }

        #endregion
    }

    class F5Fake
    {
    }
}
