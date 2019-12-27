﻿using System;
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
            WriteLine($"Start time: {DateTime.Now}");

            var resultFile = args[0];
            var cacheFileName = args[1];
            var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);
            new Processor
            {
                IpamClient = new IpamClient(ipamClientSettings),
            }.Process(resultFile, cacheFileName);

            WriteLine();
            w.Stop();
            WriteLine($"Stop time: {DateTime.Now}");
            var seconds = w.ElapsedMilliseconds / 1000;
            WriteLine($"Total time elapsed: {seconds / 60} minutes {seconds % 60} seconds");

            ReadLine();
        }
    }
}