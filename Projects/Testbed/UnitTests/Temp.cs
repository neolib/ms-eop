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

        [TestMethod]
        public void TestResourceStream()
        {
            var t = this.GetType();
            var name = t.Namespace + ".Files.test.txt";
            WriteLine($"Reading resource file \"{name}\"");
            using (var rcs = t.Assembly.GetManifestResourceStream(name))
            using (var sr = new StreamReader(rcs))
            {
                var text = sr.ReadToEnd();
                WriteLine($"{text}");
            }
        }
    }
}
