using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace Testbed.UnitTests
{
    using static Console;

    [TestClass]
    public class XmlSerializationTests
    {
        #region Inner Classes

        [AttributeUsage(AttributeTargets.Class)]
        public class MyDataItemAttribute : Attribute
        { }

        [DataContract]
        public class DataItem
        {
            [DataMember]
            public string Name { get; set; }

            [DataMember]
            public int Qty { get; set; }
        }

        [DataContract]
        [MyDataItem]
        public class WeightedDataItem : DataItem
        {
            [DataMember]
            public float Weight { get; set; }
        }

        #endregion

        [TestMethod]
        public void TestDataContractInheritance()
        {
            var item = new WeightedDataItem
            {
                Name = "My item",
                Qty = 1,
                Weight = 1.23F
            };

            // GetCustomAttributes only searches the caller Type object.
            var a = item.GetType().GetCustomAttributes(true);
            WriteLine($"{a.Length} custom attributes found:");
            foreach (Attribute attr in a)
            {
                WriteLine(attr.GetType().Name);
            }
            WriteLine();

            var ar = new DataContractSerializer(item.GetType());
            var xmlWriter = new XmlTextWriter(Out);

            xmlWriter.Formatting = Formatting.Indented;
            ar.WriteObject(xmlWriter, item);
        }
    }
}
