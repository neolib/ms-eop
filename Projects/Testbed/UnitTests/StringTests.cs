using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class StringTests
    {
        private struct Item
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
        public void TestReadChar()
        {
            using (var r = new StringReader("abc"))
            {
                for (int i = 0; i < 3; i++)
                {
                    int c = r.Read();
                    if (c == -1) break;
                    WriteLine((char)c);
                }
            }
        }

        [TestMethod]
        public void TestStringReplace()
        {
            var s = "test";
            var t = s.Replace("xxx", "");
            Assert.IsTrue(ReferenceEquals(s, t));

            var t2 = s.Replace("e", "!");
            Assert.AreEqual(t2, "t!st");
            Assert.IsFalse(ReferenceEquals(s, t2));
        }

        [TestMethod]
        public void TestStringEmpty()
        {
            var a = string.Empty;
            var b = "";
            Assert.AreEqual(a, b);
            Assert.IsTrue(ReferenceEquals(a, b));
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

    }

}
