using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Console;


namespace EOPWork
{
    class FindIPTags : IApplet
    {
        Regex ipv4Regex = new Regex(@"^\d+\.\d+\.\d+\.\d+\/\d+");
        /*
         * I'm not confident about this regular expression, so ValidateValue_ function is not used.
         * But so far the simple checks in ValidateName_ works.
         */
        Regex ipv6Regex = new Regex(@"^(([\da-z]+)?:){7}([\da-z]+)?");
        List<string> tagNames = new List<string>();

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
            tagNames.Sort();
            tagNames.ForEach((name_) => WriteLine($"    <tag name=\"{name_}\" />"));
            WriteLine("  </tags>");
            WriteLine("</result>");
        }

        public void ProcessFile(string filename)
        {
            WriteLine($"  <file path=\"{filename}\">");
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
                foreach (var child in node.Nodes())
                {
                    if (child is XElement elem) WalkNode_(elem);
                }
            }

            List<XAttribute> SearchIPTags_(XElement element)
            {
                var list = new List<XAttribute>();
                foreach (var attr in element.Attributes())
                {
                    if (ValidateName_(attr))
                    {
                        if (!attr.Value.StartsWith("0.0.0.0") &&
                            attr.Value != "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff"
                            )
                        {
                            list.Add(attr);
                            AddTagName_(attr.Name.LocalName);
                        }
                    }
                }
                return list;
            }

            bool ValidateName_(XAttribute attr)
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

            void AddTagName_(string name)
            {
                if (!tagNames.Contains(name)) tagNames.Add(name);
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
                if (ipv4Regex.IsMatch(attr.Value)) return true;
                if (ipv6Regex.IsMatch(attr.Value)) return true;
                return false;
            }
        }
    }
}
