using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;


namespace EOPWork.UnitTest
{
    using static System.Console;
    using Applets;
    using System.Threading.Tasks;

    [TestClass]
    public class EOPTest
    {
        [TestMethod]
        public void TestXDocumentCreation()
        {
            var xd = new XDocument();
            var xns = (XNamespace)"http://www.abc.com/";
            xd.Add(new XElement(xns + "root"));
            xd.Root.Add(new XElement(xns + "item1", "text1"));
            xd.Root.Add(new XElement(xns + "item2", "text2"));
            xd.Root.Add(new XElement(xns + "item3", "text3"));
            var item1 = xd.Root.Element(xns + "item1");
            if (item1 != null)
            {
                WriteLine("item1's namespace is: \"{0}\", is none: {1}",
                    item1.Name.Namespace, item1.Name.Namespace == XNamespace.None);
            }
            WriteLine(xd.ToString());
        }

        [TestMethod]
        public void TestXAttribute()
        {
            var node = new XElement("test");
            node.Add(new XAttribute("attr", 1234));
            WriteLine("node1:");
            WriteLine(node.ToString());

            var node2 = new XElement("test2");
            node2.Add(new XAttribute(node.Attributes().First()));
            WriteLine("node2:");
            WriteLine(node2.ToString());
        }

        [TestMethod]
        public void TestResourceStream()
        {
            var t = this.GetType();
            var name = t.Namespace + ".Files.test.txt";
            WriteLine($"Reading resource file \"{name}\"");
            var rcs = t.Assembly.GetManifestResourceStream(name);
            using (var sr = new StreamReader(rcs))
            {
                var text = sr.ReadToEnd();
                WriteLine($"{text}");
            }
        }

        [TestMethod]
        public void TestStringJoin()
        {
            var list = new List<Item>
            {
                new Item { name = "item1", tag = "tag1" },
                new Item { name = "item2", tag = "tag2" },
                new Item { name = "item3", tag = "tag3" },
            };

            WriteLine(string.Join(Environment.NewLine, list));
        }

        [TestMethod]
        public void TestIPv4Regex()
        {
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4,1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4 ,1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4 , 1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4, \t1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4 \t, \t1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4 1.2.3.4"));
            Assert.IsTrue(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4 \t 1.2.3.4"));
            Assert.IsFalse(IpTagFinder.ipv4Regex.IsMatch("1.2.3.4 1.2.3.4 abc"));
        }

        [TestMethod]
        public void TestIPv4RangeRegex()
        {
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22,1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 ,1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 , 1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22, \t1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 \t, \t1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 1.2.3.4/22"));
            Assert.IsTrue(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 \t 1.2.3.4/22"));
            Assert.IsFalse(IpTagFinder.ipv4RangeRegex.IsMatch("1.2.3.4/22 1.2.3.4/22 abc"));
        }

        [TestMethod]
        public void TestIPv6RegEx()
        {
            Assert.IsTrue(IpTagFinder.ipv6Regex.IsMatch("2a01:111:f400:fc18:0:0:0:16"));
            Assert.IsTrue(IpTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55::::"));
            Assert.IsTrue(IpTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55::,2a01:111:e400:3a55::"));
            Assert.IsTrue(IpTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: 2a01:111:e400:3a55::"));
            Assert.IsTrue(IpTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: \t2a01:111:e400:3a55::"));
            Assert.IsTrue(IpTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55::, \t2a01:111:e400:3a55::"));
            Assert.IsTrue(IpTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: , \t2a01:111:e400:3a55::"));
            Assert.IsFalse(IpTagFinder.ipv6Regex.IsMatch("2a01:111:e400:3a55:: 2a01:111:e400:3a55:: abc"));
        }

        [TestMethod]
        public void TestIPv6RangeRegEx()
        {
            Assert.IsTrue(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:f400:fc18:0:0:0:16/64"));
            Assert.IsTrue(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64,2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 \t2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64, \t2a01:111:e400:3a55::/64"));
            Assert.IsTrue(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 , \t2a01:111:e400:3a55::/64"));
            Assert.IsFalse(IpTagFinder.ipv6RangeRegex.IsMatch("2a01:111:e400:3a55::/64 2a01:111:e400:3a55::/64 abc"));
        }

        [TestMethod]
        public void TestXDocumentXPathAttribute()
        {
            var myType = this.GetType();
            var rcName = myType.Namespace + ".Files.env.xml";
            using (var rcs = myType.Assembly.GetManifestResourceStream(rcName))
            {
                var xd = XDocument.Load(rcs);
                var name = "am5_eur03_01";
                var node = xd.XPathSelectElement($"//add[@key='{name}']");
                WriteLine(node.Attribute("value").Value);
            }
        }

        [TestMethod]
        public void TestXDocumentXPathStartsWith()
        {
            var myType = this.GetType();
            var rcName = myType.Namespace + ".Files.env.xml";
            using (var rcs = myType.Assembly.GetManifestResourceStream(rcName))
            {
                var xd = XDocument.Load(rcs);
                var name = "am5";   // starts-with is case-sensitive
                var node = xd.XPathSelectElement($"//add[starts-with(@key, '{name}')]");
                WriteLine(node.Attribute("value").Value);
            }
        }

        [TestMethod]
        public void TestTaskWaitAll()
        {
            var r = new Random();
            StartWork_().Wait();

            async Task StartWork_()
            {
                WriteLine("Starting 10 workers...");
                var tasks = new List<Task>();
                for (int i = 1; i < 10; i++)
                {
                    tasks.Add(DoWork_($"Worker_{i}"));
                }
                Task.WaitAll(tasks.ToArray());
                WriteLine("All workers are done!");
            }

            async Task DoWork_(string name_)
            {
                var delay = r.Next(100, 2000);
                WriteLine($"Worker {name_} will complete in {delay} ms...");
                await Task.Delay(delay);
                WriteLine($"Worker {name_} completed");
            }
        }

    }

    #region Help Classes

    internal struct Item
    {
        internal string name;
        internal string tag;

        public override string ToString()
        {
            return $"{name} of {tag}";
        }
    }

    #endregion

}
