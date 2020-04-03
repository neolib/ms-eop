using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
            public int field;
            public static int SeqNo;

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
            WriteLine("Fields of Dummy:");
            foreach (var fi in typeOfDummy.GetFields())
            {
                WriteLine($"+{fi.FieldType} {fi.Name} {fi.IsStatic}");
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

        [TestMethod]
        public void TestWalkProperties()
        {
            var d = new Dictionary<string, List<Dummy>>
            {
                {
                    "aaa", new List<Dummy>
                    {
                        new Dummy{ }
                    }
                }
            };

            Walk(d, 0);

            void Walk(object obj, int indent)
            {
                var indentation = indent > 0 ? new string('-', indent * 2) : string.Empty;

                if (obj == null)
                {
                    WriteLine($"{indentation}<null>");
                    return;
                }

                var type = obj.GetType();

                Write($"{indentation}{type}");
                if (type.IsPrimitive || obj is string)
                {
                    WriteLine($" {obj}");
                    return;
                }
                WriteLine();

                if (obj is IEnumerable)
                {
                    var it = ((IEnumerable)obj).GetEnumerator();
                    while (it.MoveNext())
                    {
                        Walk(it.Current, indent + 1);
                    }
                }

                foreach (var pi in type.GetPublicProperties())
                {
                    if (pi.Name == "Item") continue;

                    WriteLine($"{indentation}{pi.Name} {pi.PropertyType}");

                    Walk(pi.GetValue(obj), indent + 1);
                }
            }
        }

        [TestMethod]
        public void TestGetPublicInstaceProperties()
        {
            foreach (var pi in IntPtr.Zero.GetType().GetPublicProperties())
            {
                WriteLine($"{pi.Name}");
            }
        }
    }

    static class ExtensionMethods
    {
        public static IEnumerable<PropertyInfo> GetPublicProperties(this Type type)
        {
            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;

            if (!type.IsInterface) { return type.GetProperties(bindingFlags); }

            var props = new List<PropertyInfo>();
            Func<PropertyInfo, bool> addPropertyInfo = (pi_) =>
            {
                var found = false;

                foreach (var prop in props)
                {
                    if (prop.Name == pi_.Name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) { props.Add(pi_); }

                return found;
            };

            foreach (var iType in type.GetInterfaces())
            {
                foreach (var pi in iType.GetProperties(bindingFlags))
                {
                    addPropertyInfo(pi);
                }
            }

            foreach (var pi in type.GetProperties(bindingFlags))
            {
                addPropertyInfo(pi);
            }

            return props;
        }

    }
}
