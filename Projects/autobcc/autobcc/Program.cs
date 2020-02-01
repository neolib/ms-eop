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
        Exception
    };

    class Program
    {
        internal static void SetExitCode(ExitCode code) => Environment.ExitCode = (int)code;

        private static string InferInetRoot(string path)
        {
            var folders = new[] { ".git", ".corext" };
            string inetRoot = null;

            path = Path.GetDirectoryName(Path.GetFullPath(path));

            for (var index = path.IndexOf('\\', 3);
                index > 0 && index < path.Length - 1;
                index = path.IndexOf('\\', index + 1))
            {
                var dir = path.Substring(0, index);

                if (!folders.Any((folder_) => Directory.Exists(Path.Combine(dir, folder_))))
                {
                    continue;
                }
                else
                {
                    inetRoot = dir;
                    break;
                }
            }

            return inetRoot;
        }

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
                            Error.WriteLine("No output file specified.");
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
                else
                {
                    Error.WriteLine("Need to specify a C# project file.");
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
                    var csprojFullPath = Path.GetFullPath(csprojPath);
                    var inetRoot = Environment.GetEnvironmentVariable(Processor.InetRootEnvVar);

                    if (string.IsNullOrEmpty(inetRoot))
                    {
                        inetRoot = InferInetRoot(csprojFullPath);
                        if (string.IsNullOrEmpty(inetRoot))
                        {
                            Error.WriteLine($"Error: {Processor.InetRootEnvMacro} is not defined and could not be inferred from project path.");
                            SetExitCode(ExitCode.NoCoreXt);
                            return;
                        }
                        else
                        {
                            Error.WriteLine($"Warning: {Processor.InetRootEnvMacro} is not defined, will use inferred path \"{inetRoot}\".");
                        }
                    }

                    try
                    {
                        new Processor
                        {
                            CacheContent = outputContent,
                            InetRoot = inetRoot,
                            Output = outputStream
                        }.Process(csprojFullPath);
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
