using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Core
{
    public static class DirectoryUtil
    {
        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘをｺﾋﾟｰします。
        /// </summary>
        /// <param name="src">ｺﾋﾟｰ元</param>
        /// <param name="dst">ｺﾋﾟｰ先</param>
        public static void Copy(string src, string dst)
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
            foreach (var finfo in srcdi.GetFiles())
            {
                //同じﾌｧｲﾙが存在していたら、常に上書きする
                finfo.CopyTo(Path.Combine(dstdi.FullName, finfo.Name), true);
            }

            // ﾃﾞｨﾚｸﾄﾘのｺﾋﾟｰ（再帰を使用）
            foreach (var diinfo in srcdi.GetDirectories())
            {
                Copy(diinfo.FullName, Path.Combine(dstdi.FullName, diinfo.Name));
            }
        }

        /// <summary>
        /// 指定したﾃﾞｨﾚｸﾄﾘを削除します。
        /// </summary>
        /// <param name="info">ﾃﾞｨﾚｸﾄﾘ</param>
        public static void Delete(string directory)
        {
            var info = new DirectoryInfo(directory);

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

        public static void DeleteInFiles(string directory, DateTime target)
        {
            foreach (var del in GetFiles(directory))
            {
                var info = new FileInfo(del);
                if (info.CreationTime < target)
                {
                    info.Delete();
                }
            }
        }

        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘが存在するか非同期で確認します。
        /// </summary>
        /// <param name="directory">確認するﾃﾞｨﾚｸﾄﾘﾊﾟｽ</param>
        /// <returns></returns>
        public static Task<bool> Exists(string directory)
        {
            return TaskUtil.WaitAsync(directory, s => Directory.Exists(s));
        }

        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘ内のﾌｧｲﾙﾘｽﾄを取得します。
        /// </summary>
        /// <param name="directory">ﾃﾞｨﾚｸﾄﾘﾊﾟｽ</param>
        /// <param name="pattern">取得するﾌｧｲﾙのﾊﾟﾀｰﾝ</param>
        /// <returns></returns>
        public static string[] GetFiles(string directory, string pattern = "*")
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, pattern)
                : new string[] { };
        }

    }
}