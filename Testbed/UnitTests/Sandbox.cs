using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;

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
        public void TestIPv4()
        {
            var ips = "20.128.5.92";
            var ip = IPAddress.Parse(ips);
            WriteLine(ip.Address);
        }
    }
}
