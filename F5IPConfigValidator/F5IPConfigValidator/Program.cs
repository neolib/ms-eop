using Microsoft.Azure.Ipam.Client;

using System;
using System.Configuration;


namespace F5IPConfigValidator
{
    using static System.Console;

    class Program
    {
        static void Main(string[] args)
        {
            var resultFile = args[0];
            var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);
            new Processor
            {
                IpamClient = new IpamClient(ipamClientSettings),
            }.Process(resultFile).Wait();

            WriteLine("Hit ENTER to exit...");
            Console.ReadLine();
        }

    }
}
