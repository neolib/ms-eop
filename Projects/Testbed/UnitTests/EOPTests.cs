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
        public void TestSearch()
        {
            var prefixes = File.ReadAllLines(@"C:\My\dev\v\BGPL.txt");
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
