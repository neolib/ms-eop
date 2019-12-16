using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class Unit2
    {
        private string ExtractEnvironmentName(string rcName)
        {
            var dotIndex = rcName.LastIndexOf('.');
            if (dotIndex > 0)
            {
                var index = rcName.LastIndexOf('.', dotIndex - 1) + 1;
                return rcName.Substring(index, dotIndex - index);
            }
            return rcName;
        }

        [TestMethod]
        public void TestExtractEnvironmentName()
        {
            Assert.AreEqual("test", ExtractEnvironmentName("test"));
            Assert.AreEqual("test", ExtractEnvironmentName("test."));
            Assert.AreEqual("test", ExtractEnvironmentName("my.ns.files.test."));
            Assert.AreEqual("test", ExtractEnvironmentName("test.xml"));
            Assert.AreEqual("test", ExtractEnvironmentName("my.ns.files.test.xml"));
        }
    }
}
