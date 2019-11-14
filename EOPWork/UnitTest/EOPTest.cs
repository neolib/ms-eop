using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;


namespace EOPWork
{
    using static System.Console;

    [TestClass]
    public class EOPTest
    {
        [TestMethod]
        public void Test()
        {
            var xd = new XDocument();
            //xd.Root.Name = "root";
            xd.Add(new XElement("root"));
            xd.Root.Add(new XElement("item1"));
            xd.Root.Add(new XElement("item2"));
            xd.Root.Add(new XElement("item3"));
            WriteLine(xd.ToString());
        }
    }
}
