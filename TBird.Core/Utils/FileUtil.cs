using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// ﾌｧｲﾙ出力前にﾌｧｲﾙを配置するﾃﾞｨﾚｸﾄﾘがなければ作成し、また同名ﾌｧｲﾙが存在する場合削除します。
        /// </summary>
        /// <param name="path">対象ﾌｧｲﾙ名</param>
        public static void FileOutputPreprocessing(string path)
        {
            // 出力ﾌｧｲﾙを格納するﾌｫﾙﾀﾞが存在しないなら作成する。
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // 出力ﾌｧｲﾙと同名ﾌｧｲﾙが存在するなら削除する。
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘをｺﾋﾟｰします。
        /// </summary>
        /// <param name="src">ｺﾋﾟｰ元</param>
        /// <param name="dst">ｺﾋﾟｰ先</param>
        public static void DirectoryCopy(string src, string dst)
        {
            DirectoryInfo srcdi = new DirectoryInfo(src);
            DirectoryInfo dstdi = new DirectoryInfo(dst);

            //ｺﾋﾟｰ先のﾃﾞｨﾚｸﾄﾘがなければ作成する
            if (dstdi.Exists == false)
            {
                dstdi.Create();
                dstdi.Attributes = srcdi.Attributes;
            }

            //ﾌｧｲﾙのｺﾋﾟｰ
            foreach (FileInfo fileInfo in srcdi.GetFiles())
            {
                //同じﾌｧｲﾙが存在していたら、常に上書きする
                fileInfo.CopyTo(Path.Combine(dstdi.FullName, fileInfo.Name), true);
            }

            // ﾃﾞｨﾚｸﾄﾘのｺﾋﾟｰ（再帰を使用）
            foreach (System.IO.DirectoryInfo directoryInfo in srcdi.GetDirectories())
            {
                DirectoryCopy(directoryInfo.FullName, Path.Combine(dstdi.FullName, directoryInfo.Name));
            }
        }

        /// <summary>
        /// 指定したﾃﾞｨﾚｸﾄﾘを削除します。
        /// </summary>
        /// <param name="info">ﾃﾞｨﾚｸﾄﾘ</param>
        public static void DirectoryDelete(DirectoryInfo info)
        {
            // ﾃﾞｨﾚｸﾄﾘ内のﾌｧｲﾙ、またはﾃﾞｨﾚｸﾄﾘを削除可能な属性にする。
            foreach (var file in info.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if (file.Attributes.HasFlag(FileAttributes.Directory))
                {
                    file.Attributes = FileAttributes.Directory;
                }
                else
                {
                    file.Attributes = FileAttributes.Normal;
                }
            }

            // ﾃﾞｨﾚｸﾄﾘの削除
            info.Delete(true);
        }

        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘをZIP圧縮します。
        /// </summary>
        /// <param name="src">圧縮するﾃﾞｨﾚｸﾄﾘ</param>
        /// <param name="dst">圧縮後ZIPﾌｧｲﾙ名</param>
        /// <param name="level">圧縮方法</param>
        /// <param name="includeBaseDirectory">圧縮するﾃﾞｨﾚｸﾄﾘをZIPﾌｧｲﾙに含めるかどうか</param>
        public static void CreateZipFromDirectory(string src, string dst, CompressionLevel level = CompressionLevel.Optimal, bool includeBaseDirectory = true)
        {
            ZipFile.CreateFromDirectory(src, dst, level, includeBaseDirectory);
        }

        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘを非同期でZIP圧縮します。
        /// </summary>
        /// <param name="src">圧縮するﾃﾞｨﾚｸﾄﾘ</param>
        /// <param name="dst">圧縮後ZIPﾌｧｲﾙ名</param>
        /// <param name="level">圧縮方法</param>
        /// <param name="includeBaseDirectory">圧縮するﾃﾞｨﾚｸﾄﾘをZIPﾌｧｲﾙに含めるかどうか</param>
        public static async Task CreateZipFromDirectoryAsync(string src, string dst, CompressionLevel level = CompressionLevel.Optimal, bool includeBaseDirectory = true)
        {
            // 処理を待機
            await CoreUtil.WaitAsync(() => CreateZipFromDirectory(src, dst, level, includeBaseDirectory));
        }

        /// <summary>
        /// ﾌｧｲﾙを移動します。
        /// </summary>
        /// <param name="src">移動元</param>
        /// <param name="dst">移動先</param>
        /// <param name="overwrite">移動先ﾊﾟｽにﾌｧｲﾙが既に存在していたら上書きするかどうか</param>
        public static void FileMove(string src, string dst, bool overwrite = true)
        {
            if (overwrite)
            {
                FileOutputPreprocessing(dst);
            }
            File.Move(src, dst);
        }

        /// <summary>
        /// 指定したﾌｧｲﾙを非同期でｺﾋﾟｰします。
        /// </summary>
        /// <param name="src">ｺﾋﾟｰ元ﾌｧｲﾙ</param>
        /// <param name="dst">ｺﾋﾟｰ先ﾌｧｲﾙ</param>
        /// <returns></returns>
        public static Task FileCopyAsync(string src, string dst)
        {
            var cts = new CancellationTokenSource();
            // ﾀﾞﾐｰのｷｬﾝｾﾙﾄｰｸﾝを指定してｺﾋﾟｰ
            return FileCopyAsync(src, dst, cts);
        }

        /// <summary>
        /// 指定したﾌｧｲﾙを非同期でｺﾋﾟｰします。
        /// </summary>
        /// <param name="src">ｺﾋﾟｰ元ﾌｧｲﾙ</param>
        /// <param name="dst">ｺﾋﾟｰ先ﾌｧｲﾙ</param>
        /// <param name="token">ｷｬﾝｾﾙﾄｰｸﾝ</param>
        /// <returns></returns>
        public static Task FileCopyAsync(string src, string dst, CancellationTokenSource cts)
        {
            return CoreUtil.WaitAsync(() => File.Copy(src, dst)).Cts(cts);
        }

        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘ内のﾌｧｲﾙﾘｽﾄを取得します。
        /// </summary>
        /// <param name="directory">ﾃﾞｨﾚｸﾄﾘﾊﾟｽ</param>
        /// <param name="pattern">取得するﾌｧｲﾙのﾊﾟﾀｰﾝ</param>
        /// <returns></returns>
        public static string[] GetDirectoryFiles(string directory, string pattern = "*")
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, pattern)
                : new string[] { };
        }
    }
}
