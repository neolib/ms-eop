using System;

namespace Testbed
{
    using static System.Console;
    using Applets;
    using System.Runtime.CompilerServices;
    using System.ComponentModel;

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var name = args[0];

                try
                {
                    var appletType = typeof(Program).Assembly.GetType("Testbed.Applets." + name);
                    var ctor = appletType.GetConstructor(new Type[] { });
                    var applet = ctor.Invoke(null) as IApplet;
                    if (applet == null)
                    {
                        WriteLine($"Applet not found by name {name}");
                    }
                    else
                    {
                        var newArgs = new string[args.Length - 1];
                        Array.Copy(args, 1, newArgs, 0, newArgs.Length);
                        applet.Run(newArgs);
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
            }

            if (!Console.IsOutputRedirected)
            {
                Write("Hit ENTER to exit...");
                ReadLine();
            }
        }
    }

}
