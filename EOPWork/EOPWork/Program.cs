﻿using System;

namespace EOPWork
{
    using static System.Console;
    using Applets;

    class Program
    {
        static void Main(string[] args)
        {
            new IPTagFinder().Run(args);
            //new Sandbox().Run(args);

            if (!Console.IsOutputRedirected)
            {
                Write("Hit ENTER to exit...");
                ReadLine();
            }
        }
    }

}
