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
        private static readonly string InetRootEnvVar = "INETROOT";
        private static readonly string InetRootEnvMacro = $"%{InetRootEnvVar}%";

        static void Main(string[] args)
        {
            void SetExitCode_(ExitCode code) => Environment.ExitCode = (int)code;

            string InferInetRoot_(string path)
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
                            SetExitCode_(ExitCode.BagBadArgs);
                            return;
                        }
                    }
                    else
                    {
                        Error.WriteLine($"Unknown option {arg}.");
                        SetExitCode_(ExitCode.BagBadArgs);
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
                        SetExitCode_(ExitCode.BagBadArgs);
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
                    SetExitCode_(ExitCode.BagBadArgs);
                    return;
                }
            }

            try
            {
                var outputContent = outputFile != null && File.Exists(outputFile)
                    ? File.ReadAllText(outputFile) : string.Empty;
                var outputStream = outputFile != null ? new StreamWriter(outputFile, true) : null;

                void WriteOutput_(string line = null)
                {
                    if (line == null) line = string.Empty;

                    // Also writes to standard output
                    WriteLine(line);
                    outputStream?.WriteLine(line);
                }

                if (File.Exists(csprojPath))
                {
                    var csprojFulPath = Path.GetFullPath(csprojPath);
                    var inetRoot = Environment.GetEnvironmentVariable(InetRootEnvVar);

                    if (string.IsNullOrEmpty(inetRoot))
                    {
                        inetRoot = InferInetRoot_(csprojFulPath);
                        if (string.IsNullOrEmpty(inetRoot))
                        {
                            Error.WriteLine($"Error: {InetRootEnvMacro} is not defined and could not be inferred from project path.");
                            SetExitCode_(ExitCode.NoCoreXt);
                            return;
                        }
                        else
                        {
                            Error.WriteLine($"Warning: {InetRootEnvMacro} is not defined, will use inferred path \"{inetRoot}\".");
                        }
                    }

                    var p = new Processor();

                    p.Process(csprojPath);

                    if (p.RefProjects.Count > 0)
                    {
                        var newCount = 0;
                        var skippedCount = 0;
                        var seperator = "REM " + new string('-', 80);

                        WriteOutput_(seperator);
                        WriteOutput_($"REM Dependent list of {csprojFulPath.Replace(inetRoot, InetRootEnvMacro)}");
                        WriteOutput_(seperator);

                        foreach (var filepath in p.RefProjects)
                        {
                            var dir = Path.GetDirectoryName(filepath);

                            if (inetRoot != null) dir = dir.Replace(inetRoot, InetRootEnvMacro);

                            if (outputContent.ContainsText(dir))
                            {
                                skippedCount++;
                                Error.WriteLine($"Already in output file: {dir}");
                            }
                            else
                            {
                                newCount++;
                                WriteOutput_($"cd /d \"{dir}\"");
                                WriteOutput_("getdeps /build:latest");
                                WriteOutput_("bcc");
                            }
                        }

                        if (newCount> 0) WriteOutput_();
                        WriteOutput_($"REM {newCount} found, {skippedCount} skipped");

                        outputStream?.Close();
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
                SetExitCode_(ExitCode.Exception);
            }
        }
    }
}
