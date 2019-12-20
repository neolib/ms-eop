using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class Unit3
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
        public void TestA()
        {
        }
    }
}
