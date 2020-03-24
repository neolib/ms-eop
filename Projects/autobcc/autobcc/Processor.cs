using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace autobcc
{
    using static Console;

    class Processor
    {
        public static readonly string DefaultNs = "http://schemas.microsoft.com/developer/msbuild/2003";
        public static readonly string InetRootEnvVar = "INETROOT";
        public static readonly string InetRootEnvMacro = $"%{InetRootEnvVar}%";

        public string InetRoot { get; private set; }

        public TextWriter Output { get; private set; }

        public Processor(TextWriter output)
        {
            this.Output = output;
        }

        public int Process(string slnOrCsprojFile)
        {
            var slnOrCsprojFilePath = Path.GetFullPath(slnOrCsprojFile);

            InetRoot = Environment.GetEnvironmentVariable(InetRootEnvVar);

            if (string.IsNullOrEmpty(InetRoot))
            {
                InetRoot = InferInetRoot(slnOrCsprojFilePath);
                if (string.IsNullOrEmpty(InetRoot))
                {
                    Error.WriteLine($"Error: {InetRootEnvMacro} is not defined and could not be inferred from project path.");
                    return (int)ExitCode.NoCoreXt;
                }
                else
                {
                    Error.WriteLine($"Warning: {InetRootEnvMacro} is not defined, will use inferred path \"{InetRoot}\".");
                }
            }

            var projList = new List<string>();

            if (slnOrCsprojFile.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                var slnTag = "Microsoft Visual Studio Solution File, Format Version";
                var regex = new Regex(@"Project\(""{\S+}""\)\s*=\s*""\S+"",\s*""(?<prj>\S+)""");
                var text = File.ReadAllText(slnOrCsprojFilePath);

                if (!text.StartsWith(slnTag))
                {
                    throw new Exception($"Not a valid sln file: {slnOrCsprojFilePath}");
                }

                foreach (Match match in regex.Matches(text))
                {
                    ParseCsproj(match.Groups["prj"].Value, projList);
                }
            }
            else
            {
                ParseCsproj(slnOrCsprojFilePath, projList);
            }

            if (projList.Count > 0)
            {
                var cmdText = GetAutobccCmd();

                Out.WriteLine(cmdText.Replace("{project}", slnOrCsprojFilePath.Replace(InetRoot, InetRootEnvMacro)));

                foreach (var filepath in projList)
                {
                    var dir = Path.GetDirectoryName(filepath).Replace(InetRoot, InetRootEnvMacro);

                    Output.WriteLine($"call :build \"{dir}\"");
                }

                Output.WriteLine("endlocal");
                Output.Flush();

                return 0;
            }
            else
            {
                WriteLine("No dependent projects found.");
                return (int)ExitCode.NoRefProjects;
            }
        }

        private string GetAutobccCmd()
        {
            var myType = this.GetType();
            var rcs = myType.Assembly.GetManifestResourceStream(myType.Namespace + ".Files.autobcc.cmd");
            using (var sr = new StreamReader(rcs)) return sr.ReadToEnd();
        }

        private void ParseCsproj(string csprojPath, IList<string> projList)
        {
            var csprojFullPath = Path.GetFullPath(csprojPath);

            if (projList.Contains(csprojFullPath, StringComparer.CurrentCultureIgnoreCase))
            {
                // Skip duplicates...
                return;
            }

            if (File.Exists(csprojFullPath))
            {
                var xd = XDocument.Load(csprojFullPath);
                var nsm = new XmlNamespaceManager(new NameTable());

                nsm.AddNamespace("ns", DefaultNs);

                // Do a very simple test if the file is valid.
                if (xd.XPathSelectElement("/ns:Project", nsm) == null)
                {
                    throw new Exception($"Not a valid csproj file: {csprojFullPath}");
                }

                var xpath = "/ns:Project/ns:ItemGroup/ns:ProjectReference";
                var projRefNodes = xd.XPathSelectElements(xpath, nsm);
                var csprojDir = Path.GetDirectoryName(csprojFullPath);

                foreach (var projRefNode in projRefNodes)
                {
                    var refCsrojPath = projRefNode.Attribute("Include").Value;

                    ParseCsproj(Path.Combine(csprojDir, refCsrojPath), projList);
                }

                projList.Add(csprojFullPath);
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

                if (folders.Any((folder_) => !Directory.Exists(Path.Combine(dir, folder_))))
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
