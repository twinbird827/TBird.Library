using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TBird.Core
{
    public static class FileUtil
    {
        /// <summary>
        /// 相対ﾊﾟｽを絶対ﾊﾟｽに変換します。
        /// </summary>
        /// <param name="relative">相対ﾊﾟｽ</param>
        /// <returns></returns>
        public static string RelativePathToAbsolutePath(string relative)
        {
            var work = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            return Path.Combine(work, relative);
        }

        /// <summary>
        /// 対象のﾊﾟｽ名に使用できない文字が含まれていないか確認します。
        /// </summary>
        /// <param name="file">ﾌｧｲﾙ名</param>
        /// <returns></returns>
        public static bool IsSalePathInvalidChars(string path)
        {
            return !Path.GetInvalidPathChars().Any(c => path.Contains(c));
        }

        /// <summary>
        /// 対象のﾌｧｲﾙ名に使用できない文字が含まれていないか確認します。
        /// </summary>
        /// <param name="file">ﾌｧｲﾙ名</param>
        /// <returns></returns>
        public static bool IsSaleFileInvalidChars(string file)
        {
            return !Path.GetInvalidFileNameChars().Any(c => file.Contains(c));
        }

        /// <summary>
        /// 対象のﾌｧｲﾙ名に不正な文字が含まれていないか確認します。
        /// </summary>
        /// <param name="file">ﾌｧｲﾙ名</param>
        /// <returns></returns>
        public static bool IsSaleFileRegex(string file)
        {
            var regex = new Regex("[\\x00-\\x1f<>:\"/\\\\|?*]|^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9]|CLOCK\\$)(\\.|$)|[\\. ]$", RegexOptions.IgnoreCase);

            return !regex.IsMatch(file);
        }

        public static IEnumerable<string[]> CsvLoad(string file)
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
