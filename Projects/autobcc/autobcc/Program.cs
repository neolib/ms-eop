using System;
using System.IO;
using System.Linq;

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
        NoRefProjects,
        Exception
    };

    class Program
    {
        internal static void SetExitCode(ExitCode code) => Environment.ExitCode = (int)code;

        static void Main(string[] args)
        {
            string csprojPath = args.FirstOrDefault();

            if (csprojPath == null)
            {
                var csprojFiles = Directory.GetFiles(".", "*.csproj");

                if (csprojFiles.Length == 1)
                {
                    csprojPath = csprojFiles[0];
                }
                else if (csprojFiles.Length > 1)
                {
                    Error.WriteLine("Multiple C# project files found in current directory.");
                    SetExitCode(ExitCode.BagBadArgs);
                    return;
                }
                else
                {
                    Error.WriteLine("No C# project file specified.");
                    SetExitCode(ExitCode.BagBadArgs);
                    return;
                }
            }
            else
            {
                if (!File.Exists(csprojPath))
                {
                    Error.WriteLine($"File not found: {csprojPath}");
                    SetExitCode(ExitCode.FileNotFound);
                    return;
                }
            }

            try
            {
                new Processor(Out).Process(csprojPath);
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                WriteLine(ex.Message);
                SetExitCode(ExitCode.Exception);
            }
        }
    }
}
