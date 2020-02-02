﻿using System;
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

        public string InetRoot { get; private set; }

        public string CacheContent { get; set; }

        public TextWriter Output { get; set; }

        public int Process(string csprojPath)
        {
            var csprojFullPath = Path.GetFullPath(csprojPath);

            InetRoot = Environment.GetEnvironmentVariable(Processor.InetRootEnvVar);

            if (string.IsNullOrEmpty(InetRoot))
            {
                InetRoot = InferInetRoot(csprojFullPath);
                if (string.IsNullOrEmpty(InetRoot))
                {
                    Error.WriteLine($"Error: {Processor.InetRootEnvMacro} is not defined and could not be inferred from project path.");
                    return (int)ExitCode.NoCoreXt;
                }
                else
                {
                    Error.WriteLine($"Warning: {Processor.InetRootEnvMacro} is not defined, will use inferred path \"{InetRoot}\".");
                }
            }

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
                    var dir = Path.GetDirectoryName(filepath).Replace(InetRoot, InetRootEnvMacro);

                    if (CacheContent.ContainsText(dir))
                    {
                        skippedCount++;
                        Error.WriteLine($"Hit cache: {dir}");
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

                Output?.Flush();

                return 0;
            }
            else
            {
                WriteLine("No dependent projects found.");
                return (int)ExitCode.NoRefProjects;
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
            var csprojFullPath = Path.GetFullPath(csprojPath);

            if (refProjList.Contains(csprojFullPath, StringComparer.CurrentCultureIgnoreCase))
            {
                // Skip duplicates...
                return;
            }

            if (File.Exists(csprojFullPath))
            {
                XDocument xd;

                try
                {
                    xd = XDocument.Load(csprojFullPath);
                }
                catch (Exception ex)
                {
                    Error.WriteLine($"Error loading {csprojFullPath}:\n{ex.Message}");
                    return;
                }

                var csprojDir = Path.GetDirectoryName(csprojFullPath);
                var nsm = new XmlNamespaceManager(new NameTable());

                nsm.AddNamespace("ns", DefaultNs);

                var xpath = "/ns:Project/ns:ItemGroup/ns:ProjectReference";
                var projRefNodes = xd.XPathSelectElements(xpath, nsm);

                foreach (var projRefNode in projRefNodes)
                {
                    var refCsrojPath = projRefNode.Attribute("Include").Value;

                    ProcessCsproj(Path.Combine(csprojDir, refCsrojPath), refProjList);
                }

                refProjList.Add(csprojFullPath);
            }
            else
            {
                Error.WriteLine($"{csprojFullPath} does not exist");
            }
        }

        private static string InferInetRoot(string path)
        {
            var folders = new[] { ".git", ".corext" };
            string InetRoot = null;

            path = Path.GetDirectoryName(Path.GetFullPath(path));

            for (var index = path.IndexOf('\\', 3);
                index > 0 && index < path.Length - 1;
                index = path.IndexOf('\\', index + 1))
            {
                var dir = path.Substring(0, index);

                if (!folders.Any((folder_) => Directory.Exists(Path.Combine(dir, folder_))))
                {
                    continue;
                }
                else
                {
                    InetRoot = dir;
                    break;
                }
            }

            return InetRoot;
        }
    }
}
