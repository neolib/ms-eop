using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Testbed.Applets
{
    using Common;
    using System;
    using System.Linq;
    using System.Xml.XPath;
    using static System.Console;

    class F5DeviceMetadata
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Error.WriteLine("Need folder of F5 XML config files.");
                Environment.ExitCode = 1;
                return;
            }

            var dir = args[0];
            var files = Directory.GetFiles(dir, "*.xml");

            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);

                if (filename.StartsWith("_")) continue;
                if (filename.StartsWithText("gtm")) continue;

                Error.WriteLine();
                Error.WriteLine($"Processing {filename}");

                var xd = XDocument.Load(file);
                var trunkNode = xd.XPathSelectElement("//object[@TRUNK_NAME]");

                if (trunkNode == null)
                {
                    Error.WriteLine("No TRUNK_NAME");
                    continue;
                }

                var trunkName = trunkNode.Attribute("TRUNK_NAME").Value;

                if (trunkName.EndsWithText("ab"))
                {
                    trunkName = trunkName.Substring(0, trunkName.Length - 2);
                }
                else if (trunkName.EndsWithText("a"))
                {
                    trunkName = trunkName.Substring(0, trunkName.Length - 1);
                }
                else
                {
                    Error.WriteLine($"TRUNK_NAME {trunkName} does not ends with ab");
                    continue;
                }

                var deviceNodes = xd.XPathSelectElements("//object[@MY_DEVICE_NAME]");

                if (deviceNodes.Count() != 2)
                {
                    Error.WriteLine($"No MY_DEVICE_NAME found");
                    continue;
                }

                for (var index = 0; index < 2; index++)
                {
                    var node = deviceNodes.ElementAt(index);
                    var deviceName = node.Attribute("MY_DEVICE_NAME").Value;

                    deviceName = deviceName.Substring(0, deviceName.IndexOf('.'));

                    // Assume the order of device nodes in XML is correct!
                    if (index == 0)
                    {
                        WriteLine($"{deviceName},3.0,{trunkName + "a"},Ethernet19/1,Data");
                        WriteLine($"{deviceName},4.0,{trunkName + "a"},Ethernet20/1,Data");
                        WriteLine($"{deviceName},5.0,{trunkName + "b"},Ethernet19/1,Data");
                        WriteLine($"{deviceName},6.0,{trunkName + "b"},Ethernet20/1,Data");
                    }
                    else
                    {
                        WriteLine($"{deviceName},3.0,{trunkName + "a"},Ethernet49/1,Data");
                        WriteLine($"{deviceName},4.0,{trunkName + "a"},Ethernet50/1,Data");
                        WriteLine($"{deviceName},5.0,{trunkName + "b"},Ethernet49/1,Data");
                        WriteLine($"{deviceName},6.0,{trunkName + "b"},Ethernet50/1,Data");
                    }
                }
            }

            Environment.ExitCode = 0;

            ReadLine();
        }
    }
}
