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

            [DataMember]
            private string Tag { get; set; } = "tag";
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
            // GetCustomAttributes only searches the caller Type object.
            var a = typeof(WeightedDataItem).GetCustomAttributes(true);
            WriteLine($"{a.Length} custom attributes found:");
            foreach (Attribute attr in a)
            {
                WriteLine(attr.GetType().Name);
            }
        }

        [TestMethod]
        public void TestDataContractSerialization()
        {
            var item = new WeightedDataItem
            {
                Name = "My item",
                Qty = 1,
                Weight = 1.23F
            };

            var ar = new DataContractSerializer(item.GetType());
            var xmlWriter = new XmlTextWriter(Out);

            xmlWriter.Formatting = Formatting.Indented;
            ar.WriteObject(xmlWriter, item);
        }

        [TestMethod]
        public void TestDataXmlSerialization()
        {
            var item = new WeightedDataItem
            {
                Name = "My item",
                Qty = 1,
                Weight = 1.23F
            };

            // XmlSerializer does not serialize private properties/fields.
            var ar = new XmlSerializer(item.GetType());

            ar.Serialize(Out, item);
        }
    }
}
