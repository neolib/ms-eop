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
    public class Temp
    {
        [TestMethod]
        public void CreateAsciiFile()
        {
            var file = @"C:\TEMP\ASCII.BIN";
            var bytes = new byte[256];

            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)i;
            File.WriteAllBytes(file, bytes);
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

        private string GetResponseString(WebResponse resp)
        {
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                return sr.ReadToEnd();
            }
        }

        [TestMethod]
        public void TestWebClient()
        {
            using (var c = new WebClient())
            {
                try
                {
                    var s = c.DownloadString("https://www.w3.org/xxx");
                    WriteLine(s);
                }
                catch (WebException ex)
                {
                    WriteLine(ex);
                    WriteLine(GetResponseString(ex.Response));
                }
            }
        }

        [TestMethod]
        public void TestWebRequest()
        {
            var request = WebRequest.CreateHttp("https://www.w3.org/xxx");
            try
            {
                using (var resp = request.GetResponse())
                {
                    WriteLine(GetResponseString(resp));
                }
            }
            catch (WebException ex)
            {
                WriteLine(ex);
                WriteLine(GetResponseString(ex.Response));
            }
        }

    }
}
