using System.IO.Compression;
using System.Threading.Tasks;

namespace TBird.Core
{
    public static class ZipUtil
    {
        /// <summary>
        /// ﾃﾞｨﾚｸﾄﾘをZIP圧縮します。
        /// </summary>
        /// <param name="src">圧縮するﾃﾞｨﾚｸﾄﾘ</param>
        /// <param name="dst">圧縮後ZIPﾌｧｲﾙ名</param>
        /// <param name="level">圧縮方法</param>
        /// <param name="includeBaseDirectory">圧縮するﾃﾞｨﾚｸﾄﾘをZIPﾌｧｲﾙに含めるかどうか</param>
        public static void CreateFromDirectory(string src, string dst, CompressionLevel level = CompressionLevel.Optimal, bool includeBaseDirectory = true)
        {
            FileUtil.Delete(dst);
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
            await TaskUtil.WaitAsync(() => CreateFromDirectory(src, dst, level, includeBaseDirectory));
        }

		public static void ExtractToDirectory(string src)
		{
			ExtractToDirectory(src, FileUtil.GetFullPathWithoutExtension(src));
		}

		public static void ExtractToDirectory(string src, string dst)
		{
			ZipFile.ExtractToDirectory(src, dst);
		}
	}
}