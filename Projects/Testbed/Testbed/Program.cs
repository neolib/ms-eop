using System;
using System.Linq;

namespace Testbed
{
    using Applets;
    using static System.Console;

    enum ExitCode
    {
        Chaos = -1,
        Success = 0,
        BadArgs,
        Exception
    }

    class Program
    {
        static void Main(string[] args)
        {
            Environment.ExitCode = (int)ExitCode.Chaos;
            var name = args.FirstOrDefault();

            if (name == null)
            {
                Environment.ExitCode = (int)ExitCode.BadArgs;
                Error.WriteLine("No applet name specified.");
            }
            else
            {
                var found = false;
                try
                {
                    var myTypes = typeof(Program).Assembly.GetTypes();
                    foreach (var type in myTypes)
                    {
                        if (type.Name == name &&
                            type.GetInterfaces().Any((type_) => type_ == typeof(IApplet)))
                        {
                            found = true;
                            var ctor = type.GetConstructor(Type.EmptyTypes);
                            var applet = ctor.Invoke(null) as IApplet;
                            var newArgs = new string[args.Length - 1];
                            Array.Copy(args, 1, newArgs, 0, newArgs.Length);
                            Environment.ExitCode = applet.Run(newArgs);
                            break;
                        }
                    }

                    if (!found)
                    {
                        Environment.ExitCode = (int)ExitCode.BadArgs;
                        Error.WriteLine($"Applet by name {name} not found.");
                    }
                }
                catch (Exception ex)
                {
                    Environment.ExitCode = (int)ExitCode.Exception;
                    Error.WriteLine(ex);
                }
            }

            if (!IsOutputRedirected)
            {
                Write("Hit ENTER to exit...");
                ReadLine();
            }

            Environment.ExitCode = (int)ExitCode.Success;
        }
    }

}
