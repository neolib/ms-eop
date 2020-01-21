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

            if (Environment.GetEnvironmentVariable("CorextExeVersion") == null)
            {
                WriteLine("Must run autobcc in CoreXT environment.");
                SetExitCode_(ExitCode.NoCoreXt);
                //return;
            }

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
                            var cmdFilePath = GenerateCmd_(p.RefProjects);

                            try
                            {
                                var process = Process.Start(cmdFilePath);

                                process.WaitForExit();
                            }
                            finally
                            {
                                if (cmdFilePath != null) File.Delete(cmdFilePath);
                            }
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

            string GenerateCmd_(IList<string> fileList)
            {
                var cmdFilePath = Path.Combine(Path.GetTempPath(), $"autobcc{DateTime.Now.Ticks}.cmd");
                using (var writer = new StreamWriter(cmdFilePath))
                {
                    writer.WriteLine(GetAutobccResource_());

                    foreach (var filepath in fileList)
                    {
                        var dir = Path.GetDirectoryName(filepath);

                        writer.WriteLine($"call :build {dir}");
                    }
                }
                return cmdFilePath;
            }

            string GetAutobccResource_()
            {
                var type = typeof(Program);
                var rcName = type.Namespace + ".autobcc.cmd";
                using (var reader = new StreamReader(type.Assembly.GetManifestResourceStream(rcName)))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
