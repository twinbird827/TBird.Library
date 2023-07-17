﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
    public static class FileUtil
    {
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

        public static void BeforeCreate(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// ﾌｧｲﾙを移動します。
        /// </summary>
        /// <param name="src">移動元</param>
        /// <param name="dst">移動先</param>
        /// <param name="overwrite">移動先ﾊﾟｽにﾌｧｲﾙが既に存在していたら上書きするかどうか</param>
        public static void Move(string src, string dst, bool overwrite = true)
        {
            if (overwrite) BeforeCreate(dst);

            File.Move(src, dst);
        }

        /// <summary>
        /// ﾌｧｲﾙが存在するか非同期で確認します。
        /// </summary>
        /// <param name="file">確認するﾌｧｲﾙﾊﾟｽ</param>
        /// <returns></returns>
        public static Task<bool> Exists(string file)
        {
            return TaskUtil.WaitAsync(file, s => File.Exists(s));
        }

        /// <summary>
        /// 指定したﾌｧｲﾙを非同期でｺﾋﾟｰします。
        /// </summary>
        /// <param name="src">ｺﾋﾟｰ元ﾌｧｲﾙ</param>
        /// <param name="dst">ｺﾋﾟｰ先ﾌｧｲﾙ</param>
        /// <returns></returns>
        public static Task CopyAsync(string src, string dst)
        {
            // ﾀﾞﾐｰのｷｬﾝｾﾙﾄｰｸﾝを指定してｺﾋﾟｰ
            return CopyAsync(src, dst, new CancellationTokenSource());
        }

        /// <summary>
        /// 指定したﾌｧｲﾙを非同期でｺﾋﾟｰします。
        /// </summary>
        /// <param name="src">ｺﾋﾟｰ元ﾌｧｲﾙ</param>
        /// <param name="dst">ｺﾋﾟｰ先ﾌｧｲﾙ</param>
        /// <param name="token">ｷｬﾝｾﾙﾄｰｸﾝ</param>
        /// <returns></returns>
        public static async Task CopyAsync(string src, string dst, CancellationTokenSource cts)
        {
            var buffersize = 1 * 1024 * 1024;

            using (var ss = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, buffersize, true))
            using (var ds = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, buffersize, true))
            {
                await ss.CopyToAsync(ds, buffersize, cts.Token);
            }
        }

    }
}