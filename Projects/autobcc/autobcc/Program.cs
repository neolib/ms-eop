using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace autobcc
{
    using static Console;

    enum ExitCode
    {
        Chaos = -99,
        Success = 0,
        BagBadArgs,
        NoCoreXt,
        FileNotFound,
        Exception
    };

    class Program
    {
        static void Main(string[] args)
        {
            void SetExitCode_(ExitCode code) => Environment.ExitCode = (int)code;

            if (args.Length == 1)
            {
                var csprojPath = args[0];

                try
                {
                    if (File.Exists(csprojPath))
                    {
                        var p = new Processor();

                        p.Process(args[0]);

                        if (p.RefProjects.Count > 0)
                        {
                            foreach (var filepath in p.RefProjects)
                            {
                                var dir = Path.GetDirectoryName(filepath);

                                WriteLine($"cd /d \"{dir}\"");
                                WriteLine("getdeps /build:latest");
                                WriteLine("bcc");
                            }
                            WriteLine();
                        }
                        else
                        {
                            WriteLine("No dependent projects found.");
                        }

                        Environment.ExitCode = 0;
                    }
                    else SetExitCode_(ExitCode.FileNotFound);
                }
                catch (Exception ex)
                {
                    WriteLine(ex.Message);
                    Environment.ExitCode = -1;
                }
            }
            else
            {
                Error.WriteLine("Need project pathname.");
                SetExitCode_(ExitCode.BagBadArgs);
            }
        }
    }
}
