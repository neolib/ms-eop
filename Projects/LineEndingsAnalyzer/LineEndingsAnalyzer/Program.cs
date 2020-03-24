using System;
using System.IO;
using System.Text;

namespace LineEndingsAnalyzer
{
    using static Console;

    class Program
    {
        static void Main(string[] args)
        {
            foreach (var file in args)
            {
                WriteLine($"Analyzing {file}");
                try
                {
                    Analyze(file);
                }
                catch (Exception ex)
                {
                    Error.WriteLine(ex.Message);
                }
                WriteLine();
            }
        }

        static void Analyze(string file)
        {
            byte[] bytes;

            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                if (fs.Length > 10 * 1024 * 1024)
                {
                    Error.WriteLine($"{file} is too big");
                    return;
                }
                if (fs.Length == 0)
                {
                    Error.WriteLine($"{file} size is zero");
                    return;
                }

                bytes = new byte[fs.Length];
                fs.Read(bytes, 0, bytes.Length);
            }

            var encoding = DetectEncoding(bytes);

            if (encoding == null)
            {
                Error.WriteLine("Cannot detect encoding, using default UTF-8");
                encoding = Encoding.UTF8;
            }

            var lastByte = 0;
            var lastLineIndex = 0;
            var lines = 0;
            var suspects = 0;

            for (var i = 0; i < bytes.Length; i++)
            {
                var @byte = bytes[i];

                if (@byte == '\r')
                {
                    lastLineIndex = i;
                }
                else if (@byte == '\n')
                {
                    lines++;
                    if (lastByte != '\r')
                    {
                        suspects++;

                        var line = encoding.GetString(bytes, lastLineIndex + 1, i - lastLineIndex - 1);
                        WriteLine($"Unix line ending @{lines}:");
                        WriteLine($"{line}\\n");
                    }
                    lastLineIndex = i;
                }
                else if (lastByte == '\r')
                {
                    suspects++;

                    var line = encoding.GetString(bytes, lastLineIndex + 1, i - lastLineIndex - 1);
                    WriteLine($"CR line ending @{lines}:");
                    WriteLine($"{line}\\r");
                }
                lastByte = @byte;
            }

            WriteLine($"{suspects} bad line endings found");
        }

        static Encoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3)
            {
                if (bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf) return Encoding.UTF8;
            }

            if (bytes.Length >= 2)
            {
                if (bytes[0] == 0xff && bytes[1] == 0xfe) return Encoding.Unicode;
                if (bytes[0] == 0xfe && bytes[1] == 0xff) return Encoding.BigEndianUnicode;

                if (bytes[0] != 0 && bytes[1] == 0) return Encoding.Unicode;
                if (bytes[0] == 0 && bytes[1] != 0) return Encoding.BigEndianUnicode;
            }

            if (bytes.Length >= 4)
            {
                if (bytes[0] == 0xff && bytes[1] == 0xfe && bytes[2] == 0 && bytes[3] == 0) return Encoding.UTF32;
                if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xfe && bytes[3] == 0xff) return Encoding.GetEncoding("UTF-32BE");
            }

            return null;
        }
    }
}
