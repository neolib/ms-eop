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
            var pattern = @"(?<h>EOP:\s+)(?<r>\w+)\s*-\s*(?<df>\w+?)(?<t>(FSPROD)?\s+(-\s+)?IPv\d.+)";
            var titleRegex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var titles = new[] {
                "EOP: EUR-DB3FSPROD - IPv4_Anchor - IPV4_HRI",
                "EOP: NAM-BN1NAM07 - IPv4_1022/1023 - IPv4_1022/1023"
                };

            foreach (var title in titles)
            {
                var match = titleRegex.Match(title);

                Assert.IsTrue(match.Success);

                var headGroup = match.Groups["h"];
                var regionGroup = match.Groups["r"];
                var dcForestGroup = match.Groups["df"];
                var tailGroup = match.Groups["t"];

                WriteLine($"Header: {headGroup.Value}");
                WriteLine($"Region: {regionGroup.Value}");
                WriteLine($"DC/Forest: {dcForestGroup.Value}");
                WriteLine($"Tail: {tailGroup.Value}");
                WriteLine();

                // Use StringBuilder to do replacement
                var sb = new StringBuilder(title);
                sb.Replace(dcForestGroup.Value, "DCFOREST", dcForestGroup.Index, dcForestGroup.Length);
                Assert.AreEqual(sb.ToString(), $"EOP: {regionGroup.Value}-DCFOREST{tailGroup.Value}");

                // Use MatchEvaluator to do replacement
                var replaced = titleRegex.Replace(title, (match_) => $"{headGroup.Value}{regionGroup.Value}-DCFOREST{tailGroup.Value}");
                Assert.AreEqual(replaced, $"EOP: {regionGroup.Value}-DCFOREST{tailGroup.Value}");
            }

            var bad = "EOP: ITAR USG01 Capacity/MGMT Block";
            Assert.IsFalse(titleRegex.IsMatch(bad));
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
        public void TestSearch()
        {
            var prefixes = File.ReadAllLines(@"C:\My\dev\v\BGPLCheck.txt");
            WriteLine($"total {prefixes.Length} prefixes to check");
            var text = File.ReadAllText(@"C:\My\dev\v\result.xml");

            foreach (var prefix in prefixes)
            {
                var s = '"' + prefix + '"';
                var found = text.Contains(s);
                WriteLine($"{prefix},{found}");
            }
        }
    }
}
