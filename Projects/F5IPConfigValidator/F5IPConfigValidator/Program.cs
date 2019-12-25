using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace F5IPConfigValidator
{
    using Microsoft.Azure.Ipam.Client;
    using static System.Console;

    class Program
    {
        static void Main(string[] args)
        {
            var w = Stopwatch.StartNew();
            Error.WriteLine($"Start time: {DateTime.Now}");

            var resultFile = args.FirstOrDefault();
            if (resultFile == null)
            {
                Error.WriteLine("Need path to result file.");
                Environment.ExitCode = 1;
            }
            else
            {
                var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);
                new Processor
                {
                    IpamClient = new IpamClient(ipamClientSettings),
                }.Process(resultFile).Wait();

                w.Stop();
                Error.WriteLine($"Stop time: {DateTime.Now}");
                var seconds = w.ElapsedMilliseconds / 1000;
                Error.WriteLine($"Total time elapsed: {seconds / 60} minutes {seconds % 60} seconds");
                Environment.ExitCode = 0;
            }

            ReadLine();
        }

    }
}
