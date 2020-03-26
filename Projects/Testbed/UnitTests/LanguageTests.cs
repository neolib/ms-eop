using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

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

        interface IBase
        {
            string Name { get; set; }
        }
        interface IBase2
        {
            string Name { get; set; }
            string Name2 { get; set; }
        }

        interface IDummy : IBase, IBase2
        {
            string Address { get; set; }

            int Add(int a, int b);
        }

        class Dummy : IDummy
        {
            public static int SeqNo { get; set; }

            public string Name { get; set; }
            public string Name2 { get; set; }
            public string Address { get; set; }

            public int Add(int a, int b) => a + b;

            private string DoNotCallMe(object notUseful) => nameof(DoNotCallMe);

        }

        [TestMethod]
        public void TestInvokeMethod()
        {
            var dummy = new Dummy();
            var typeOfDummy = dummy.GetType();
            var typeOfIDummy = typeof(IDummy);

            WriteLine("Public properties of IDummy");
            foreach (var pi in typeOfIDummy.GetPublicProperties())
            {
                WriteLine($"+{pi.PropertyType} {pi.Name}");
            }

            WriteLine();
            WriteLine("Properties of IDummy:");
            foreach (var pi in typeOfIDummy.GetProperties())
            {
                WriteLine($"+{pi.PropertyType} {pi.Name}");
            }

            WriteLine();
            WriteLine("Properties of Dummy:");
            foreach (var pi in typeOfDummy.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                WriteLine($"+{pi.PropertyType} {pi.Name}");
            }

            WriteLine();
            WriteLine("Methods:");
            foreach (var mi in typeOfDummy.GetMethods())
            {
                var @static = mi.IsStatic ? "static " : "";
                var @public = mi.IsPublic ? "public" : "private";
                Write($"-{@static}{@public} {mi.ReturnType} {mi.Name}(");

                var sb = new StringBuilder();

                foreach (var t in mi.GetParameters())
                {
                    sb.Append($"{t.ParameterType} {t.Name}, ");
                }
                if (sb.Length > 2) sb.Length -=2;
                WriteLine($"{sb})");
            }

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var methodInfo = typeOfDummy.GetMethod("DoNotCallMe", flags);
            Assert.IsNotNull(methodInfo);
            Assert.AreEqual(methodInfo.Name, methodInfo.Invoke(dummy, flags,
                null, new object[] { null }, CultureInfo.CurrentCulture));
        }

    }

    static class ExtensionMethods
    {
        public static IEnumerable<PropertyInfo> GetPublicProperties(this Type type)
        {
            if (!type.IsInterface) { return type.GetProperties(); }

            var props = new List<PropertyInfo>();

            foreach (var iType in type.GetInterfaces())
            {
                foreach (var pi in iType.GetProperties())
                {
                    var found = false;

                    foreach (var prop in props)
                    {
                        if (prop.Name == pi.Name)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found) { props.Add(pi); }
                }
            }

            return props;
        }
    }
}
