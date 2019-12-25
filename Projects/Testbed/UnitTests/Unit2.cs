using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class Unit2
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
    }
}
