using System;
using static System.Console;


namespace EOPWork
{
    class Program
    {
        static void Main(string[] args)
        {
            //new IPTagFinder().Run(args);
            new Sandbox().Run(args);

            if (!Console.IsOutputRedirected)
            {
                Write("Hit ENTER to exit...");
                ReadLine();
            }
        }
    }

}
