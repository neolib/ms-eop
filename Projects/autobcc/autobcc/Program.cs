using System;
using System.IO;
using System.Linq;

namespace autobcc
{
    using Common;
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
            string csprojPath = null;
            string outputFile = null;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("/") || arg.StartsWith("-"))
                {
                    var option = arg.Substring(1);

                    if (option.IsSameTextAs("out"))
                    {
                        i++;
                        if (i < args.Length)
                        {
                            outputFile = args[i];
                        }
                        else
                        {
                            Error.WriteLine("Output option has no output file value.");
                            SetExitCode(ExitCode.BagBadArgs);
                            return;
                        }
                    }
                    else
                    {
                        Error.WriteLine($"Unknown option {arg}.");
                        SetExitCode(ExitCode.BagBadArgs);
                        return;
                    }
                }
                else
                {
                    if (csprojPath == null)
                    {
                        csprojPath = arg;
                    }
                    else
                    {
                        Error.WriteLine("Program accepts only one input file.");
                        SetExitCode(ExitCode.BagBadArgs);
                        return;
                    }
                }
            }

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

            try
            {
                var outputContent = outputFile != null && File.Exists(outputFile)
                    ? File.ReadAllText(outputFile) : string.Empty;
                var outputStream = outputFile != null ? new StreamWriter(outputFile, true) : null;

                if (File.Exists(csprojPath))
                {
                    try
                    {
                        new Processor
                        {
                            CacheContent = outputContent,
                            Output = outputStream
                        }.Process(csprojPath);
                    }
                    finally
                    {
                        outputStream?.Close();
                    }

                    Environment.ExitCode = 0;
                }
                else SetExitCode(ExitCode.FileNotFound);
            }
            catch (Exception ex)
            {
                WriteLine(ex.Message);
                SetExitCode(ExitCode.Exception);
            }
        }
    }
}
