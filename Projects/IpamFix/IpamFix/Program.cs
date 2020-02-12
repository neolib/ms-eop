using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace IpamFix
{
    using Microsoft.Azure.Ipam.Client;
    using static System.Console;

    enum ExitCode
    {
        Chaos = -1,
        Success = 0,
        BadArgs,
        Exception,
        FileNotFound,
        NoRecords,
    }

    class Program
    {
        static void Main(string[] args)
        {
            var w = Stopwatch.StartNew();
            WriteLine($"Start time: {DateTime.Now}");

            var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);
            Environment.ExitCode = (int)ExitCode.Chaos;

            try
            {
                IpamHelper.IpamClient = new IpamClient(ipamClientSettings);
                IpamHelper.LoadMaps();

                //new Processor().Run(args);
                new UndoTitles().Run(args);
            }
            catch (Exception ex)
            {
                Environment.ExitCode = (int)ExitCode.Exception;
                Error.WriteLine(ex);
            }

            WriteLine();
            w.Stop();
            WriteLine($"Stop time: {DateTime.Now}");
            var seconds = w.ElapsedMilliseconds / 1000;
            WriteLine($"Total time elapsed: {seconds / 60} minutes {seconds % 60} seconds");

            ReadLine();
        }
    }
}
