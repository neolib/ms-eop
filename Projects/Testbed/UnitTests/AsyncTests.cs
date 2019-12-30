using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.DesignerServices;
using System.Threading.Tasks;

namespace Testbed.UnitTests
{
    using static System.Console;

    [TestClass]
    public class AsyncTests
    {
        [TestMethod]
        public void TestTaskWaitAll()
        {
            var w = Stopwatch.StartNew();
            var r = new Random();
            StartWork_();
            w.Stop();
            WriteLine($"Total time used in ms: {w.ElapsedMilliseconds}");

            void StartWork_()
            {
                WriteLine("Starting 10 workers...");
                var tasks = new List<Task>();
                for (int i = 1; i < 10; i++)
                {
                    tasks.Add(DoWork_($"Worker_{i}"));
                }
                Task.WaitAll(tasks.ToArray());
                WriteLine("All workers are done!");
            }

            async Task DoWork_(string name_)
            {
                var delay = r.Next(100, 2000);
                WriteLine($"Worker {name_} will complete in {delay} ms...");
                await Task.Delay(delay);
                WriteLine($"Worker {name_} completed");
            }
        }

        [TestMethod]
        public void TestLoopCaptureForEach()
        {
            var list = new List<Item>();

            for (int i = 0; i < 10; i++)
            {
                list.Add(new Item("item" + i, "tag" + i));
            }

            var tasks = new List<Task>();
            foreach (var item in list)
            {
                tasks.Add(Test_());

                async Task Test_()
                {
                    await Task.Delay(100);
                    WriteLine($"{item.name},{item.tag}");
                    item.tag = "updated!";
                }
            }

            Task.WaitAll(tasks.ToArray());
        }

        [TestMethod]
        public void TestLoopCaptureFor()
        {
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var copyI = i;
                tasks.Add(Test_());

                async Task Test_()
                {
                    await Task.Delay(100);
                    WriteLine($"i={i}, copy={copyI}");
                }
            }

            Task.WaitAll(tasks.ToArray());
        }

        #region Private Classes

        private class Item
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

        #endregion

    }

}
