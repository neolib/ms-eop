using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Console;


namespace F5Automation
{
    public class IPTagFinder
    {
        public const string IPv4Pattern = @"(?:\d+\.\d+\.\d+\.\d+)";
        public const string IPv4RangePattern = @"(?:\d+\.\d+\.\d+\.\d+/\d+)";
        public const string IPV6Pattern = @"(?:[1-9a-f][\da-f]*)(?::(?:[\da-f]+)*)+";
        public const string IPV6RangePattern = @"(?:[1-9a-f][\da-f]*)(?::(?:[\da-f]+)*)+/\d+";
        public const string SeparatorPattern = @"?:\s*(?:,|\s)\s*";

        public static Regex ipv4Regex = new Regex(
            $@"^{IPv4Pattern}({SeparatorPattern}{IPv4Pattern})*$");

        public static Regex ipv4RangeRegex = new Regex(
            $@"^{IPv4RangePattern}({SeparatorPattern}{IPv4RangePattern})*$");

        public static Regex ipv6Regex = new Regex(
            $@"^{IPV6Pattern}({SeparatorPattern}{IPV6Pattern})*$",
            RegexOptions.IgnoreCase);

        public static Regex ipv6RangeRegex = new Regex(
            $@"^{IPV6RangePattern}({SeparatorPattern}{IPV6RangePattern})*$",
            RegexOptions.IgnoreCase);

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
            WriteLine($"  <file name=\"{Path.GetFileName(filename)}\">");
            var xd = XDocument.Load(filename);
            WalkNode_(xd.Root);
            WriteLine("  </file>");

            void WalkNode_(XElement node)
            {
                var list = SearchIPTags_(node);
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
                    // Skip this huge node!
                    if (attr.Name.LocalName == "f5_class" &&
                        attr.Value == "GTM_AAAA_RECORD_CREATE_BULK")
                    {
                        break;
                    }

                    if (ValidateValue_(attr))
                    {
                        list.Add(attr);
                        AddTagName_(attr);
                    }
                }
                if (list.Count > 0)
                {
                    WriteLine($"    <{node.Name} path=\"{GetNodePath_(node)}\"");
                    foreach (var attr in list)
                        WriteLine($"      {attr.Name}=\"{attr.Value}\"");
                    WriteLine("    />");
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
                var s = attr.Value;

                // Skip these special IP ranges
                if (s.StartsWith("0.0.0.0")) return false;
                if (s.StartsWith("255.255.255.255")) return false;
                if (s.StartsWith("ffff:ffff:")) return false;
                if (s.StartsWith("0:")) return false;

                if (ipv4Regex.IsMatch(s)) return true;
                if (ipv6Regex.IsMatch(s)) return true;
                if (ipv4RangeRegex.IsMatch(s)) return true;
                if (ipv6RangeRegex.IsMatch(s)) return true;

                return false;
            }
        }
    }
}
