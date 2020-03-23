using System.IO;
using System.Text.RegularExpressions;

namespace Testbed.Applets
{
    using Common;
    using static System.Console;

    class F5DeviceMetadata : IApplet
    {
        public int Run(string[] args)
        {
            if (args.Length != 1)
            {
                Error.WriteLine("Need folder of F5 XML config files.");
                return 1;
            }

            var dir = args[0];
            var files = Directory.GetFiles(dir, "*.xml");
            var regex = new Regex(@"TRUNK_NAME\s*=\s*(?<q>[""'])(?<name>\S+?)\<q>");

            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);

                if (filename.StartsWith("_")) continue;
                if (filename.StartsWithText("gtm")) continue;

                var text = File.ReadAllText(file);
                var match = regex.Match(text);

                if (match.Success)
                {
                    WriteLine($"{filename},{match.Groups["name"].Value}");
                }
                else
                {
                    WriteLine($"{filename},No match");
                }
            }

            return 0;
        }
    }
}
