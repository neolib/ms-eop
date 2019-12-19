using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class Sandbox
    {
        [TestMethod]
        public void TestStringConcat()
        {
            var a = 1234;
            var b = "def";
            var c = DateTime.Now;

            var s = "a=" + a + ",b=" + b + ",c=" + c;

            WriteLine(s);
        }

        [TestMethod]
        public void Test()
        {
            var file = @"C:\My\dev\v\result.csv";
            var re = new Regex(@"Prefix: .+/\d+,");
            var input = File.ReadAllText(file);

            var matchIPv6 = re.Match("Prefix: 260f:d200:3:5880::/64,");
            Assert.IsTrue(matchIPv6.Success);

            var list = new List<string>();
            foreach (Match match in re.Matches(input))
            {
                if (!list.Contains(match.Value))
                {
                    list.Add(match.Value);
                }
            }
            list.Sort();
            list.ForEach((item_) => WriteLine(item_));
        }
    }
}
