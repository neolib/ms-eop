using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class EOPTests
    {
        private string ExtractEnvironmentName(string rcName)
        {
            var dotIndex = rcName.LastIndexOf('.');
            if (dotIndex > 0)
            {
                var index = rcName.LastIndexOf('.', dotIndex - 1) + 1;
                return rcName.Substring(index, dotIndex - index);
            }
            return rcName;
        }

        [TestMethod]
        public void TestExtractEnvironmentName()
        {
            Assert.AreEqual("test", ExtractEnvironmentName("test"));
            Assert.AreEqual("test", ExtractEnvironmentName("test."));
            Assert.AreEqual("test", ExtractEnvironmentName("my.ns.files.test."));
            Assert.AreEqual("test", ExtractEnvironmentName("test.xml"));
            Assert.AreEqual("test", ExtractEnvironmentName("my.ns.files.test.xml"));
        }

        [TestMethod]
        public void TestTitlePattern()
        {
            var input = "EOP: EUR-DB3FSPROD - IPv4_Anchor - IPV4_HRI";
            var titlePattern = new Regex(@"(?<h>EOP:\s+)(?<f>\w+)-(?<d>\w+?)(?<t>(FSPROD)?\s+-\s+IPv.+)",
                 RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var match = titlePattern.Match(input);

            Assert.IsTrue(match.Success);

            var headGroup = match.Groups["h"];
            var forestGroup = match.Groups["f"];
            var dcGroup = match.Groups["d"];
            var tailGroup = match.Groups["t"];

            Assert.AreEqual("EUR", forestGroup.Value);
            Assert.AreEqual("DB3", dcGroup.Value);

            // Use StringBuilder to do replacement
            var sb = new StringBuilder(input);
            sb.Replace(forestGroup.Value, "FOREST", forestGroup.Index, forestGroup.Length);
            Assert.AreEqual(sb.ToString(), "EOP: FOREST-DB3FSPROD - IPv4_Anchor - IPV4_HRI");

            // Use MatchEvaluator to do replacement
            var replaced = titlePattern.Replace(input, (match_) => $"{headGroup.Value}FOREST-DCNAME{tailGroup.Value}");
            Assert.AreEqual(replaced, "EOP: FOREST-DCNAMEFSPROD - IPv4_Anchor - IPV4_HRI");

            var bad = "EOP: ITAR USG01 Capacity/MGMT Block";
            match = titlePattern.Match(bad);
            Assert.IsFalse(match.Success);
        }

        [TestMethod]
        public void TestExtractPrefix()
        {
            var file = @"C:\My\dev\v\result.csv";
            var re = new Regex(@"Prefix: (.+/\d+),");
            var input = File.ReadAllText(file);

            var matchIPv6 = re.Match("Prefix: 260f:d200:3:5880::/64,");
            Assert.IsTrue(matchIPv6.Success);
            Assert.AreEqual(matchIPv6.Groups[1].Value, "260f:d200:3:5880::/64");

            var list = new List<string>();
            foreach (Match match in re.Matches(input))
            {
                var prefix = match.Groups[1].Value;
                if (!list.Contains(prefix))
                {
                    list.Add(prefix);
                }
            }
            list.Sort();
            list.ForEach((item_) => WriteLine(item_));
        }

        [TestMethod]
        public void TestCountIPs()
        {
            var a = new[] {
                "10.13.14.0/27",
                "10.13.14.32/27",
                "10.13.14.64/27",
                "10.13.150.224/27",
                "10.13.151.0/27",
                "10.233.232.192/27",
                "10.97.81.0/24",
                "20.128.10.0/23",
                "21.3.100.0/24",
                "21.3.101.0/24",
                "21.3.12.0/23",
                "21.3.129.0/24",
                "21.3.136.0/23",
                "21.3.148.0/24",
                "21.3.149.0/24",
                "21.3.17.0/24",
                "21.3.172.0/23",
                "21.3.177.0/24",
                "23.103.138.144/28",
                "23.103.156.128/28",
                "23.103.156.160/28",
                "23.103.157.144/28",
                "23.103.157.224/27",
                "25.153.54.0/23",
                "25.155.30.0/23",
                "25.155.62.0/23",
                "40.107.208.0/24",
                };

            WriteLine($"total {a.Length} IPs");
            var text = File.ReadAllText(@"C:\My\dev\v\result.csv");
            var c = 0;

            foreach (var s in a)
            {
                if (text.Contains(s))
                {
                    WriteLine($"found {s}");
                }
                else
                {
                    c++;
                    WriteLine($"not found {s}");

                }
            }
            WriteLine($"not found {c}");
        }
    }
}
