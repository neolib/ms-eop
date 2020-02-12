using System;
using System.Collections.Generic;
using System.IO;

namespace IpamFix
{
    using ExcelDataReader;
    using Common;
    using static Console;
    using ExcelRecord = Dictionary<string, string>;
    using StringList = List<string>;

    public sealed class ExcelHelper
    {
        public static Dictionary<string, List<StringList>> ReadSheets(string excelFileName)
        {
            using (var stream = File.Open(excelFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var sheets = new Dictionary<string, List<StringList>>();

                    do
                    {
                        WriteLine($"***Reading sheet {reader.Name}...");

                        var records = new List<StringList>();

                        while (reader.Read())
                        {
                            var record = new StringList();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                record.Add(reader.GetString(i));
                            }

                            records.Add(record);
                        }

                        sheets[reader.Name] = records;
                    } while (reader.NextResult());

                    return sheets;
                }
            }
        }

        public static List<ExcelRecord> ReadSheet(string excelFileName, string sheetName, bool hasHeader)
        {
            using (var stream = File.Open(excelFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var records = new List<ExcelRecord>();

                    do
                    {
                        WriteLine($"***Got sheet {reader.Name}...");
                        if (sheetName == null || reader.Name.IsSameTextAs(sheetName))
                        {
                            var fieldNames = new StringList();

                            if (hasHeader)
                            {
                                if (reader.Read())
                                {
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        fieldNames.Add(reader.GetString(i));
                                    }
                                }
                            }

                            while (reader.Read())
                            {
                                var record = new ExcelRecord();
                                var fieldCount = hasHeader ? fieldNames.Count : reader.FieldCount;

                                for (int i = 0; i < fieldCount; i++)
                                {
                                    if (hasHeader)
                                        record[fieldNames[i]] = reader.GetString(i);
                                    else
                                        record[i.ToString()] = reader.GetString(i);
                                }

                                records.Add(record);
                            }

                            break;
                        }
                    } while (reader.NextResult());

                    return records;
                }
            }
        }
    }
}
