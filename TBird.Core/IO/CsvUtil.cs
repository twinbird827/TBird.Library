using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TBird.Core
{
    public static class CsvUtil
    {
        public static IEnumerable<string[]> Load(string file)
        {
            var sb = new StringBuilder();
            var re = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            using (var fs = new FileStream(file, FileMode.Open))
            using (var ws = new WrappingStream(fs))
            using (var sr = new StreamReader(ws))
            {
                while (!sr.EndOfStream)
                {
                    sb.Append(sr.ReadLine());

                    var line = sb.ToString();
                    if (line.Count(c => c == '"') % 2 == 0)
                    {
                        yield return re.Split(line).Select(x => x.Replace("\"\"", "\"").Trim('"')).ToArray();
                        sb.Clear();
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                }
            }
        }

    }
}