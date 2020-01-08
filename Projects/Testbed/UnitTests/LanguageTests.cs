using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class LanguageTests
    {
        [TestMethod]
        public void TestArrayCopy()
        {
            var a = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            // Copy forward
            Array.Copy(a, 0, a, 3, 7);
            for (int i = 0; i < a.Length; i++)
            {
                WriteLine($"{i}:{a[i]}");
            }
        }

        [TestMethod]
        public void TestArrayCopy2()
        {
            var a = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            // Copy backward
            Array.Copy(a, 3, a, 0, 7);
            for (int i = 0; i < a.Length; i++)
            {
                WriteLine($"{i}:{a[i]}");
            }
        }
    }
}
