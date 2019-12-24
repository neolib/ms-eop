using System;
using System.Linq;

namespace Testbed
{
    using Applets;
    using static System.Console;

    class Program
    {
        static void Main(string[] args)
        {
            var name = args.FirstOrDefault();

            if (name == null)
            {
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
                            var ctor = type.GetConstructor(new Type[] { });
                            var applet = ctor.Invoke(null) as IApplet;
                            var newArgs = new string[args.Length - 1];
                            Array.Copy(args, 1, newArgs, 0, newArgs.Length);
                            applet.Run(newArgs);
                            break;
                        }
                    }

                    if (!found)
                    {
                        Error.WriteLine($"Applet by name {name} not found.");
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
            }

            if (!IsOutputRedirected)
            {
                Write("Hit ENTER to exit...");
                ReadLine();
            }
        }
    }

}
