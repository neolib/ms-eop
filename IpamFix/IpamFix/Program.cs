using System;
using System.Configuration;
using System.Diagnostics;

namespace IpamFix
{
    using Microsoft.Azure.Ipam.Client;
    using static System.Console;

    class Program
    {
        static void Main(string[] args)
        {
            var w = Stopwatch.StartNew();
            Error.WriteLine($"Start time: {DateTime.Now}");

            var resultFile = args[0];
            var cacheFileName = args[1];
            var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);
            new Processor
            {
                IpamClient = new IpamClient(ipamClientSettings),
            }.Process(resultFile, cacheFileName);
            w.Stop();
            Error.WriteLine($"Stop time: {DateTime.Now}");
            var seconds = w.ElapsedMilliseconds / 1000;
            Error.WriteLine($"Total time elapsed: {seconds / 60} minutes {seconds % 60} seconds");

            if (!IsOutputRedirected) ReadLine();
        }
    }
}
