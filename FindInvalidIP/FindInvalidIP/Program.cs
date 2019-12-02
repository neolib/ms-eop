using Microsoft.Azure.Ipam.Client;

using System;
using System.Configuration;


namespace FindInvalidIP
{
    using static System.Console;

    class Program
    {
        static void Main(string[] args)
        {
            var resultFile = args[0];
            var ipamClientSettings = new IpamClientSettings(ConfigurationManager.AppSettings);

            WriteLine($"Using AS {ipamClientSettings.InitialAddressSpaceId}");
            new Processor
            {
                IpamClient = new IpamClient(ipamClientSettings),
            }.Process(resultFile).Wait();

            WriteLine("Hit ENTER to exit...");
            Console.ReadLine();
        }

    }
}
