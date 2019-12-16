using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;


namespace Testbed.UnitTests
{
    using static System.Console;

    [TestClass]
    public class Unit1
    {
        [TestMethod]
        public void TestXDocumentCreation()
        {
            var xd = new XDocument();
            var xns = (XNamespace)"http://www.abc.com/";
            xd.Add(new XElement(xns + "root"));
            xd.Root.Add(new XElement(xns + "item1", "text1"));
            xd.Root.Add(new XElement(xns + "item2", "text2"));
            xd.Root.Add(new XElement("item3", "text3"));
            WriteLine(xd.ToString());

            var item1 = xd.Root.Element(xns + "item1");
            Assert.IsNotNull(item1);
            WriteLine("item1's namespace is: \"{0}\", is none: {1}",
                item1.Name.Namespace, item1.Name.Namespace == XNamespace.None);
            var item2 = xd.Root.Element("item2");
            Assert.IsNull(item2);
            var item3 = xd.Root.Element("item3");
            Assert.IsNotNull(item3);
            WriteLine("item3's namespace is: \"{0}\", is none: {1}",
                item3.Name.Namespace, item3.Name.Namespace == XNamespace.None);
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
                new Item("item1", "tag1"),
                new Item("item2", "tag2"),
                new Item("item3", "tag3"),
            };

            WriteLine(string.Join(Environment.NewLine, list));
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
            var w = Stopwatch.StartNew();
            var r = new Random();
            StartWork_();
            w.Stop();
            WriteLine($"Total time used in ms: {w.ElapsedMilliseconds}");

            void StartWork_()
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

        internal Item(string name, string tag)
        {
            this.name = name;
            this.tag = tag;
        }

        public override string ToString()
        {
            return $"{name} of {tag}";
        }
    }

    #endregion

}
