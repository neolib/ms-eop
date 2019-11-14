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
        Regex ipv6Regex = new Regex(@"^(([\da-z]+)?:){7}([\da-z]+)?");
        List<string> tagNames = new List<string>();

        public int Run(string[] args)
        {
            var dir = args[0];
            Process(dir);

            WriteLine("==============================");
            tagNames.Sort();
            tagNames.ForEach((name_) => WriteLine($"{name_}"));
            return 0;
        }

        void AddTagName(string name)
        {
            if (!tagNames.Contains(name)) tagNames.Add(name);
        }

        void Process(string dir)
        {
            WriteLine($"Target dir: {dir}");
            foreach (var filename in Directory.GetFiles(dir, "*.xml"))
            {
                ProcessFile(filename);
            }
        }

        public void ProcessFile(string filename)
        {
            WriteLine($"**{filename}");
            var xd = XDocument.Load(filename);
            WalkNode_(xd.Root);

            void WalkNode_(XElement node)
            {
                var list = SearchIPTags_(node);
                if (list.Count > 0)
                {
                    WriteLine($"<{node.Name}");
                    foreach (var attr in list)
                        WriteLine($"    {attr.Name}=\"{attr.Value}\"");
                    WriteLine(">");
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
                            AddTagName(attr.Name.LocalName);
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

            bool ValidateValue_(XAttribute attr)
            {
                if (ipv4Regex.IsMatch(attr.Value)) return true;
                if (ipv6Regex.IsMatch(attr.Value)) return true;
                return false;
            }
        }
    }
}
