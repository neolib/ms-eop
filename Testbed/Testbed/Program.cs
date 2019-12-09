using System;

namespace Testbed
{
    using static System.Console;
    using Applets;

    class Program
    {
        static void Main(string[] args)
        {
            new Sandbox().Run(args);

            if (!Console.IsOutputRedirected)
            {
                Write("Hit ENTER to exit...");
                ReadLine();
            }
        }
    }

}
