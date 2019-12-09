using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    using F5Automation;

    [TestClass]
    public class IPTagFinderTests
    {
        [TestMethod]
        public void TestIPv4Regex()
        {
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4,1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4 ,1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4 , 1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4, \t1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4 \t, \t1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4 1.2.3.4"));
            Assert.IsTrue(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4 \t 1.2.3.4"));
            Assert.IsFalse(IPTagFinder.ipv4Regex.IsMatch("1.2.3.4 1.2.3.4 abc"));
        }

        [TestMethod]
        public void TestIPv4RangeRegex()
        {
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22,1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 ,1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 , 1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22, \t1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 \t, \t1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 1.2.3.4/22"));
            Assert.IsTrue(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 \t 1.2.3.4/22"));
            Assert.IsFalse(IPTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 1.2.3.4/22 abc"));
        }

        [TestMethod]
        public void TestIPv6RegEx()
        {
            Assert.IsTrue(IPTagFinder.ipv6Regex.IsMatch("2a01:111:f400:fc18:0:0:0:16"));
            Assert.IsTrue(IPTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55::::"));
            Assert.IsTrue(IPTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55::,2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: 2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: \t2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55::, \t2a01:111:e400:3a55::"));
            Assert.IsTrue(IPTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: , \t2a01:111:e400:3a55::"));
            Assert.IsFalse(IPTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: 2a01:111:e400:3a55:: abc"));
        }

        [TestMethod]
        public void TestIPv6RangeRegEx()
        {
            Assert.IsTrue(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:f400:fc18:0:0:0:16/64"));
            Assert.IsTrue(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64,2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 \t2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64, \t2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 , \t2a01:111:e400:3a55::/64"));
            Assert.IsFalse(IPTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 2a01:111:e400:3a55::/64 abc"));
        }
    }
}
