using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    using F5Automation;
    using static Console;

    [TestClass]
    public class IPTagFinderTests
    {
        [TestMethod]
        public void TestIPv4Regex()
        {
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4,1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4 ,1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4 , 1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4, \t1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4 \t, \t1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4 1.2.3.4"));
            Assert.IsTrue(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4 \t 1.2.3.4"));
            Assert.IsFalse(IPTagFinder.IPv4Regex.IsMatch("1.2.3.4 1.2.3.4 abc"));
        }

        [TestMethod]
        public void TestIPv4RangeRegex()
        {
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22,1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22 ,1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22 , 1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22, \t1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22 \t, \t1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22 1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22 \t 1.2.3.4/22"));
            Assert.IsFalse(IPTagFinder.IPv4RangeRegex.IsMatch("1.2.3.4/22 1.2.3.4/22 abc"));
        }

        [TestMethod]
        public void TestIPv6RegEx()
        {
            Assert.IsTrue(IPTagFinder.IPv6Regex.IsMatch("2a01:111:f400:fc18:0:0:0:16"));
            Assert.IsTrue(IPTagFinder.IPv6Regex.IsMatch("2a01:111:e400:3a55::::"));
            Assert.IsTrue(IPTagFinder.IPv6Regex.IsMatch("2a01:111:e400:3a55::,2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.IPv6Regex.IsMatch("2a01:111:e400:3a55:: 2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.IPv6Regex.IsMatch("2a01:111:e400:3a55:: \t2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.IPv6Regex.IsMatch("2a01:111:e400:3a55::, \t2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.IPv6Regex.IsMatch("2a01:111:e400:3a55:: , \t2a01:111:e400:3a55::"));
            Assert.IsFalse(IPTagFinder.IPv6Regex.IsMatch("2a01:111:e400:3a55:: 2a01:111:e400:3a55:: abc"));
        }

        [TestMethod]
        public void TestIPv6RangeRegEx()
        {
            Assert.IsTrue(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:f400:fc18:0:0:0:16/64"));
            Assert.IsTrue(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64,2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 \t2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64, \t2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 , \t2a01:111:e400:3a55::/64"));
            Assert.IsFalse(IPTagFinder.IPv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 2a01:111:e400:3a55::/64 abc"));
        }

        [TestMethod]
        public void TestVlanRegEx()
        {
            var filenames = new[] {
                "fra01-mr2.xml",
                "son01-sn1.xml",
                "zaf01-ct2.xml"
            };
            var myType = this.GetType();

            foreach (var filename in filenames)
            {
                Dump_($"{myType.Namespace}.Files.{filename}");
                WriteLine();
            }

            void Dump_(string rcName)
            {
                WriteLine($"{rcName}");
                using (var rcs = myType.Assembly.GetManifestResourceStream(rcName))
                {
                    var xd = XDocument.Load(rcs);
                    var comment = xd.Root.FirstNode as XComment;
                    foreach (Match match in IPTagFinder.VlanRegex.Matches(comment.Value))
                    {
                        WriteLine($"no={match.Groups["no"]} name={match.Groups["name"]} ipv4={match.Groups["v4"]} ipv6={match.Groups["v6"]}");
                    }

                    foreach (Match match in IPTagFinder.VlanRegex2.Matches(comment.Value))
                    {
                        Write($"name={match.Groups["name"]}");
                        if (match.Groups["v4"].Success)
                            WriteLine($" ipv4={match.Groups["v4"]}");
                        else
                            WriteLine($" ipv6={match.Groups["v6"]}");
                    }
                }
            }
        }

    }
}
