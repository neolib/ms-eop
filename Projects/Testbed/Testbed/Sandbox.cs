using System.Diagnostics;
using System.IO;

namespace Testbed.Applets
{
    using static System.Console;

    class Sandbox : IApplet
    {
        public int Run(string[] args)
        {
            var process = Process.Start(@"c:\temp\test.cmd");
            process.WaitForExit();

            return 0;
        }
    }
}
