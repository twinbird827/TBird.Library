using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TBird.Core
{
    public class Lang
    {
        public static Lang Instance
        {
            get => _Instance = _Instance ?? new Lang();
        }
        private static Lang? _Instance;

        /// <summary>
        /// 言語ﾌｧｲﾙの配置ﾃﾞｨﾚｸﾄﾘ
        /// </summary>
        private const string basedir = @".\lang";

        /// <summary>
        /// 言語情報
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> _items = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// 言語ﾌｧｲﾙを読込んでｲﾝｽﾀﾝｽを生成します。
        /// </summary>
        private Lang()
        {
            var directory = Directories.GetAbsolutePath(basedir);

            if (!Directory.Exists(directory)) return;

            DirectoryUtil
                .GetFiles(directory, "*.csv")
                .OrderBy(x => x)
                .SelectMany(path => Expand(path))
                .ForEach(x => _items[x.Key] = x.Value);
        }

        /// <summary>
        /// 指定したｷｰの文字を取得します。
        /// </summary>
        /// <param name="name">ｷｰ</param>
        /// <returns></returns>
        public string Get([CallerMemberName] string? name = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (_items.ContainsKey(name))
            {
                if (_items[name].ContainsKey(CoreSetting.Instance.Language))
                {
                    return _items[name][CoreSetting.Instance.Language];
                }
                else
                {
                    return _items[name].Values.FirstOrDefault();
                }
            }
            else
            {
                return name;
            }
        }

        /// <summary>
        /// 指定した言語ﾌｧｲﾙを展開します。
        /// </summary>
        /// <param name="path">言語ﾌｧｲﾙﾊﾟｽ</param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, string>> Expand(string path)
        {
            // 明細行を全行取得
            var all = CsvUtil.Load(path);
            // ﾍｯﾀﾞ行(存在することが前提)
            var headers = all.First();
            // 明細行(2行目以降)
            var lines = all.Skip(1).ToArray();

            // 各行をDictionary化するための配列(1列目は項目名なので2列目以降をDictionary化する)
            var indexes = Enumerable.Range(1, headers.Length - 1).ToArray();

            // Dictionary化して返却
            return lines.ToDictionary(
                line => line[0],
                line => indexes.ToDictionary(i => headers[i], i => line[i])
            );
        }
    }
}