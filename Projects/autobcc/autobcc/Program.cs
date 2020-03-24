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
            string slnOrCsprojFile = args.FirstOrDefault();

            if (slnOrCsprojFile == null)
            {
                var files = Directory.GetFiles(".", "*.csproj");

                if (files.Length == 0) files = Directory.GetFiles(".", "*.sln");

                if (files.Length == 1)
                {
                    slnOrCsprojFile = files[0];
                }
                else if (files.Length > 1)
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
                if (!File.Exists(slnOrCsprojFile))
                {
                    Error.WriteLine($"File not found: {slnOrCsprojFile}");
                    SetExitCode(ExitCode.FileNotFound);
                    return;
                }
            }

            try
            {
                new Processor(Out).Process(slnOrCsprojFile);
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
