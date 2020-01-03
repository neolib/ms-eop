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
    public class LocalFuncTests
    {

        [TestMethod]
        public void TestLocalFunc()
        {
            for (int i = 0; i < 10; i++)
            {
                var i2 = 1234;
                Func<int, int> func = (i_) =>
                {
                    return i + i_;
                };

                WriteLine(func(i2));
            }
        }

        public void TestLocalFunc2()
        {
            var list = new List<int>();
            Func<int, bool> containsFunc = (i_) => list.Contains(i_);

            list.Add(1);
            list.Add(2);
            list.Add(3);

            Assert.IsTrue(containsFunc(1));
        }

        [TestMethod]
        public void TestYield()
        {
            foreach (var i in Get_(10))
            {
                WriteLine(i);
            }

            IEnumerable<int> Get_(int c)
            {
                while (c-- > 0)
                {
                    yield return c;
                }
            }
        }
    }
}
