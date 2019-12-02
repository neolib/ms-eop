using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Console;


namespace EOPWork.Applets
{
    public class IPTagFinder : IApplet
    {
        public static Regex ipv4Regex = new Regex(@"^(?:\d+\.\d+\.\d+\.\d+/\d+)(?:\s*(?:,|\s)\s*(?:\d+\.\d+\.\d+\.\d+/\d+))*$");
        public static Regex ipv6Regex = new Regex(@"^(?:[\da-fA-F]+:)(?:[\da-fA-F]+|:)+/\d+(?:\s*(?:,|\s)\s*(?:[\da-fA-F]+:)(?:[\da-fA-F]+|:)+/\d+)*$");
        List<XAttribute> tagNames = new List<XAttribute>();

        public int Run(string[] args)
        {
            var dir = args[0];
            Process(dir);
            return 0;
        }

        void Process(string dir)
        {
            WriteLine($"<!-- Target dir: {dir} -->");
            WriteLine("<result>");

            foreach (var filename in Directory.GetFiles(dir, "*.xml"))
            {
                ProcessFile(filename);
            }

            WriteLine("  <!-- Tag List -->");
            WriteLine("  <tags>");
            tagNames.ForEach((attr_) => WriteLine($"    <tag name=\"{attr_.Parent.Name}\" attr=\"{attr_.Name}\" />"));
            WriteLine("  </tags>");
            WriteLine("</result>");
        }

        public void ProcessFile(string filename)
        {
            WriteLine($"  <file path=\"{Path.GetFileName(filename)}\">");
            var xd = XDocument.Load(filename);
            WalkNode_(xd.Root);
            WriteLine("  </file>");

            void WalkNode_(XElement node)
            {
                var list = SearchIPTags_(node);
                if (list.Count > 0)
                {
                    WriteLine($"    <{node.Name} path=\"{GetNodePath_(node)}\"");
                    foreach (var attr in list)
                        WriteLine($"      {attr.Name}=\"{attr.Value}\"");
                    WriteLine("    />");
                }
                foreach (var child in node.Elements())
                {
                    WalkNode_(child);
                }
            }

            List<XAttribute> SearchIPTags_(XElement node)
            {
                var list = new List<XAttribute>();
                foreach (var attr in node.Attributes())
                {
                    if (ValidateValue_(attr))
                    {
                        list.Add(attr);
                        AddTagName_(attr);
                    }
                }
                return list;
            }

            void AddTagName_(XAttribute attr)
            {
                if (!tagNames.Any((attr_) => 
                    attr.Parent.Name == attr_.Parent.Name && attr.Name == attr_.Name)
                    )
                {
                    tagNames.Add(attr);
                }
            }

            string GetNodePath_(XElement node)
            {
                var sb = new StringBuilder();
                var list = new List<string>();
                for (var currrent = node.Parent; currrent != null; currrent = currrent.Parent)
                {
                    list.Add(currrent.Name.LocalName);
                }
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    sb.Append(list[i]);
                    sb.Append('.');
                }
                return sb.ToString();
            }

            bool ValidateValue_(XAttribute attr)
            {
                //WriteLine($"***<{attr.Parent.Name} {attr.Name}=\"{attr.Value}\"");
                
                // Skip these special IP ranges and they also make Regex very slow!
                if (attr.Value.StartsWith("0.0.0.0")) return false;
                if (attr.Value.StartsWith("ffff:ffff:")) return false;
                if (ipv4Regex.IsMatch(attr.Value)) return true;
                if (ipv6Regex.IsMatch(attr.Value)) return true;
                return false;
            }
        }
    }
}
