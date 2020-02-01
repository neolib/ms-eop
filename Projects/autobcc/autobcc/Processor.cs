using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace autobcc
{
    using Common;
    using static Console;

    class Processor
    {
        public static readonly string DefaultNs = "http://schemas.microsoft.com/developer/msbuild/2003";
        public static readonly string InetRootEnvVar = "INETROOT";
        public static readonly string InetRootEnvMacro = $"%{InetRootEnvVar}%";

        public string InetRoot { get; set; }

        public string CacheContent { get; set; }

        public TextWriter Output { get; set; }

        public void Process(string csprojFullPath)
        {
            var refProjList = new List<string>();
            ProcessCsproj(csprojFullPath, refProjList);

            if (refProjList.Count > 0)
            {
                var newCount = 0;
                var skippedCount = 0;
                var seperator = "REM " + new string('-', 80);

                WriteOutput(seperator);
                WriteOutput($"REM Dependent list of {csprojFullPath.Replace(InetRoot, InetRootEnvMacro)}");
                WriteOutput(seperator);

                foreach (var filepath in refProjList)
                {
                    var dir = Path.GetDirectoryName(filepath);

                    dir = dir.Replace(InetRoot, InetRootEnvMacro);

                    if (CacheContent.ContainsText(dir))
                    {
                        skippedCount++;
                        Error.WriteLine($"Already in output file: {dir}");
                    }
                    else
                    {
                        newCount++;
                        WriteOutput($"cd /d \"{dir}\"");
                        WriteOutput("getdeps /build:latest");
                        WriteOutput("bcc");
                    }
                }

                if (newCount > 0) WriteOutput();
                WriteOutput($"REM {newCount} found, {skippedCount} skipped");

                Output.Flush();
            }
            else
            {
                WriteLine("No dependent projects found.");
            }
        }

        private void WriteOutput(string line = null)
        {
            if (line == null) line = string.Empty;

            // Also writes to standard output
            if (Output != Out) WriteLine(line);
            Output?.WriteLine(line);
        }

        private void ProcessCsproj(string csprojPath, IList<string> refProjList)
        {
            csprojPath = Path.GetFullPath(csprojPath);

            if (refProjList.Contains(csprojPath, StringComparer.CurrentCultureIgnoreCase)) return;

            var xd = XDocument.Load(csprojPath);
            var csprojDir = Path.GetDirectoryName(csprojPath);

            var nsm = new XmlNamespaceManager(new NameTable());
            nsm.AddNamespace("ns", DefaultNs);
            var projRefNodes = xd.XPathSelectElements("/ns:Project/ns:ItemGroup/ns:ProjectReference", nsm);

            foreach (var projRefNode in projRefNodes)
            {
                var refCsrojPath = projRefNode.Attribute("Include").Value;

                ProcessCsproj(Path.Combine(csprojDir, refCsrojPath), refProjList);
            }

            refProjList.Add(csprojPath);
        }


    }
}
