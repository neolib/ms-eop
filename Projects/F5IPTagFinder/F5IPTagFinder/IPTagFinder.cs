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
        public const string IPv4Pattern = @"(\d+\.\d+\.\d+\.\d+)";
        public const string IPv4RangePattern = @"(\d+\.\d+\.\d+\.\d+/\d+)";
        public const string IPv6Pattern = @"([1-9a-f][\da-f]*)(:([\da-f]+)*)+";
        public const string IPv6RangePattern = @"([1-9a-f][\da-f]*)(:([\da-f]+)*)+/\d+";
        public const string SeparatorPattern = @"\s*(,|\s)\s*";

        public static Regex IPv4Regex = new Regex(
            $@"^{IPv4Pattern}({SeparatorPattern}{IPv4Pattern})*$",
            RegexOptions.ExplicitCapture);

        public static Regex IPv4RangeRegex = new Regex(
            $@"^{IPv4RangePattern}({SeparatorPattern}{IPv4RangePattern})*$",
            RegexOptions.ExplicitCapture);

        public static Regex IPVv6Regex = new Regex(
            $@"^{IPv6Pattern}({SeparatorPattern}{IPv6Pattern})*$",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public static Regex IPv6RangeRegex = new Regex(
            $@"^{IPv6RangePattern}({SeparatorPattern}{IPv6RangePattern})*$",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public static Regex VlanRegex = new Regex(
            $@"^\s*(?<no>\d+)\s+(?<name>\w+)\s+(?<v4>{IPv4RangePattern})(\s+(?<v6>{IPv6RangePattern}))?\s*$",
            RegexOptions.Multiline | RegexOptions.ExplicitCapture);

        public static Regex VlanRegex2 = new Regex(
            $@"^\s*(?<name>([a-z]+(\s+[a-z]+)?)|(\d+(/\d+)?))\s+(\-\s+)?((?<v4>({IPv4Pattern})|({IPv4RangePattern}))|(?<v6>({IPv6Pattern})|({IPv6RangePattern})))\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        List<XAttribute> tagNames = new List<XAttribute>();

        public int Run(string[] args)
        {
            var dir = args[0];
            Process(dir);
            return 0;
        }

        private void Process(string dir)
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

        private void ProcessFile(string filename)
        {
            var xd = XDocument.Load(filename);
            var barename = Path.GetFileName(filename);

            WriteLine($"  <file name=\"{barename}\">");
            ExtractVlanInfo_();
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

                if (IPv4Regex.IsMatch(s)) return true;
                if (IPVv6Regex.IsMatch(s)) return true;
                if (IPv4RangeRegex.IsMatch(s)) return true;
                if (IPv6RangeRegex.IsMatch(s)) return true;

                return false;
            }

            void ExtractVlanInfo_()
            {
                if (xd.Root.FirstNode is XComment comment)
                {
                    var matches1 = VlanRegex.Matches(comment.Value);
                    var matches2 = VlanRegex2.Matches(comment.Value);

                    if (matches1.Count > 0 || matches2.Count > 0)
                    {
                        WriteLine("    <VLANs>");
                        foreach (Match match in matches1)
                        {
                            Write($"      <VLAN name=\"{match.Groups["name"]}\" no=\"{match.Groups["no"]}\" IPv4=\"{match.Groups["v4"]}\"");
                            if (match.Groups["v6"].Success)
                            {
                                Write($" IPv6=\"{match.Groups["v6"]}\"");
                            }
                            WriteLine(" />");
                        }

                        foreach (Match match in matches2)
                        {
                            Write($"      <VLAN name=\"{match.Groups["name"]}\"");
                            if (match.Groups["v4"].Success)
                                Write($" IPv4=\"{match.Groups["v4"]}\"");
                            else
                                Write($" IPv6=\"{match.Groups["v6"]}\"");
                            WriteLine(" />");
                        }
                        WriteLine("    </VLANs>");
                    }
                }
            }
        }

    }
}
