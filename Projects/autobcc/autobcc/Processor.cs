using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace autobcc
{
    class Processor
    {
        public static readonly string DefaultNs = "http://schemas.microsoft.com/developer/msbuild/2003";

        public IList<string> RefProjects { get; set; } = new List<string>();

        public void Process(string csprojPath)
        {
            ProcessCsproj(csprojPath);
        }

        private void ProcessCsproj(string csprojPath)
        {
            csprojPath = Path.GetFullPath(csprojPath);

            if (RefProjects.Contains(csprojPath, StringComparer.CurrentCultureIgnoreCase)) return;

            var xd = XDocument.Load(csprojPath);
            var csprojDir = Path.GetDirectoryName(csprojPath);

            var nsm = new XmlNamespaceManager(new NameTable());
            nsm.AddNamespace("ns", DefaultNs);
            var projRefNodes = xd.XPathSelectElements("/ns:Project/ns:ItemGroup/ns:ProjectReference", nsm);

            foreach (var projRefNode in projRefNodes)
            {
                var refCsrojPath = projRefNode.Attribute("Include").Value;

                ProcessCsproj(Path.Combine(csprojDir, refCsrojPath));
            }

            RefProjects.Add(csprojPath);
        }
    }
}
