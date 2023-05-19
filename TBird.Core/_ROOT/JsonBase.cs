using Codeplex.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace TBird.Core
{
    public abstract class JsonBase
    {
        // 読み込みﾌﾗｸﾞ
        internal static bool _load = false;

        // 読み込み処理を一意に実行するためのﾛｯｸｵﾌﾞｼﾞｪｸﾄ
        internal static object _lock = new object();

        // 設定ﾌｧｲﾙ
        internal string _basepath;

        // 暗号化ﾌﾗｸﾞ
        internal bool _encrypt;

        /// <summary>
        /// 設定ﾌｧｲﾙにﾌﾟﾛﾊﾟﾃｨの値を保存します。
        /// </summary>
        public void Save()
        {
            lock (_lock)
            {
                _encrypt = false;
                Serialize();
                _encrypt = true;
            }
        }

        /// <summary>
        /// ｲﾝｽﾀﾝｽの内容をｼﾘｱﾗｲｽﾞ化します。
        /// </summary>
        private void Serialize()
        {
            // 出力ﾌｧｲﾙを格納するﾌｫﾙﾀﾞが存在しないなら作成する。
            Directory.CreateDirectory(Path.GetDirectoryName(_basepath));

            // 出力ﾌｧｲﾙと同名ﾌｧｲﾙが存在するなら削除する。
            if (File.Exists(_basepath))
            {
                File.Delete(_basepath);
            }

            var json = DynamicJson.Serialize(this);

            File.WriteAllText(_basepath, ToReadable(json));
        }

        /// <summary>
        /// Json文字列にｲﾝﾃﾞﾝﾄを追加して可読性を高めます。
        /// </summary>
        /// <param name="json">Json文字列</param>
        /// <returns></returns>
        private string ToReadable(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;
            int i = 0;
            int indent = 0;
            int quoteCount = 0;
            int position = -1;
            var sb = new StringBuilder();
            int lastindex = 0;
            while (true)
            {
                if (i > 0 && json[i] == '"' && json[i - 1] != '\\') quoteCount++;

                if (quoteCount % 2 == 0) //is not value(quoted)
                {
                    if (json[i] == '{' || json[i] == '[')
                    {
                        indent++;
                        position = 1;
                    }
                    else if (json[i] == '}' || json[i] == ']')
                    {
                        indent--;
                        position = 0;
                    }
                    else if (json.Length > i && json[i] == ',' && json[i + 1] == '"')
                    {
                        position = 1;
                    }
                    if (position >= 0)
                    {
                        sb.AppendLine(json.Substring(lastindex, i + position - lastindex));
                        sb.Append(new string(' ', indent * 2));
                        lastindex = i + position;
                        position = -1;
                    }
                }

                i++;
                if (json.Length <= i)
                {
                    sb.Append(json.Substring(lastindex));
                    break;
                }

            }
            return sb.ToString();
        }

    }

    public abstract class JsonBase<TType> : JsonBase where TType : JsonBase
    {
        protected JsonBase(string path)
        {
            _basepath = FileUtil.RelativePathToAbsolutePath(path);
            _encrypt = false;
        }

        /// <summary>
        /// 指定したﾌﾟﾛﾊﾟﾃｨの値を取得します。
        /// </summary>
        /// <typeparam name="T">ﾌﾟﾛﾊﾟﾃｨの型</typeparam>
        /// <param name="storage">ﾌﾟﾛﾊﾟﾃｨの値を保持する変数</param>
        /// <returns></returns>
        protected T GetProperty<T>(T storage)
        {
            return storage;
        }

        /// <summary>
        /// 指定したﾌﾟﾛﾊﾟﾃｨの値を設定します。
        /// </summary>
        /// <typeparam name="T">ﾌﾟﾛﾊﾟﾃｨの型</typeparam>
        /// <param name="storage">ﾌﾟﾛﾊﾟﾃｨの値を保持する変数</param>
        /// <param name="value">変更後の値</param>
        /// <param name="propertyName">ﾌﾟﾛﾊﾟﾃｨ名</param>
        /// <returns></returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            // ﾌﾟﾛﾊﾟﾃｨ値変更
            storage = value;
            return true;
        }

        /// <summary>
        /// 指定したﾌﾟﾛﾊﾟﾃｨの値を複合化して取得します。
        /// </summary>
        /// <typeparam name="T">ﾌﾟﾛﾊﾟﾃｨの型</typeparam>
        /// <param name="storage">ﾌﾟﾛﾊﾟﾃｨの値を保持する変数</param>
        /// <returns></returns>
        protected string GetEncryptProperty(string storage)
        {
            if (_encrypt)
            {
                return Encrypter.DecryptString(storage, CoreSetting.Instance.ApplicationKey);
            }
            else
            {
                return storage;
            }
        }

        /// <summary>
        /// 指定したﾌﾟﾛﾊﾟﾃｨの値を暗号化して設定します。
        /// </summary>
        /// <typeparam name="T">ﾌﾟﾛﾊﾟﾃｨの型</typeparam>
        /// <param name="storage">ﾌﾟﾛﾊﾟﾃｨの値を保持する変数</param>
        /// <param name="value">変更後の値</param>
        /// <param name="propertyName">ﾌﾟﾛﾊﾟﾃｨ名</param>
        /// <returns></returns>
        protected bool SetEncryptProperty(ref string storage, string value, [CallerMemberName] string propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            // ﾌﾟﾛﾊﾟﾃｨ値変更
            if (_encrypt)
            {
                storage = GetEncryptString(value);
            }
            else
            {
                storage = value;
            }
            return true;
        }

        protected string GetEncryptString(string value)
        {
            return Encrypter.EncryptString(value, CoreSetting.Instance.ApplicationKey);
        }

        /// <summary>
        /// 設定ﾌｧｲﾙからﾌﾟﾛﾊﾟﾃｨの値を読み込みます。
        /// </summary>
        /// <returns></returns>
        protected bool Load()
        {
            if (_load)
            {
                return false;
            }
            TType src;
            lock (_lock)
            {
                // 既存ﾌｧｲﾙ読込
                _load = true;
                src = Deserialize();
                if (src != null) src._basepath = _basepath;
                _load = false;

                if (src != null)
                {
                    // 既存ﾌｧｲﾙが存在するならﾌｧｲﾙの設定値で上書き
                    var props = typeof(TType).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    _encrypt = false;
                    props.AsParallel().ForAll(prop =>
                    {
                        prop.SetValue(this, prop.GetValue(src));
                    });
                    _encrypt = true;

                    return true;
                }
                else
                {
                    // 既存ﾌｧｲﾙが存在しないなら規定値で作成
                    _encrypt = true;
                    return false;
                }
            }
        }

        /// <summary>
        /// ｼﾘｱﾗｲｽﾞ化された設定ﾌｧｲﾙの内容を復元します。
        /// </summary>
        /// <returns></returns>
        private TType Deserialize()
        {
            try
            {
                if (File.Exists(_basepath))
                {
                    var json = DynamicJson.Parse(File.ReadAllText(_basepath));
                    return json.Deserialize<TType>();
                }
                else
                {
                    return default(TType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

    }
}
