using System;
using System.Linq;
using System.Security.Cryptography;

namespace TBird.Core
{
	public class Encrypter
	{
		/// <summary>
		/// 文字列を暗号化します。
		/// </summary>
		/// <param name="data">暗号化する文字列</param>
		/// <param name="password">ﾊﾟｽﾜｰﾄﾞ</param>
		/// <returns>暗号化した文字列(Base64形式)</returns>
		public static string EncryptString(string data, string password)
		{
			// 暗号に用いるICryptoTransformを生成するﾌﾞﾛｯｸ
			using (AesManaged aes = new AesManaged())
			{
				aes.BlockSize = 128;                // BlockSize = 16bytes
				aes.KeySize = 128;                  // KeySize = 16bytes
				aes.Mode = CipherMode.CBC;          // CBC mode
				aes.Padding = PaddingMode.PKCS7;    // Padding mode is "PKCS7".

				//入力されたﾊﾟｽﾜｰﾄﾞをﾍﾞｰｽに擬似乱数を新たに生成
				Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(password, 16);

				// Rfc2898DeriveBytesが内部生成したなｿﾙﾄを取得
				byte[] salt = deriveBytes.Salt;

				// 生成した擬似乱数から16ﾊﾞｲﾄ切り出したﾃﾞｰﾀをﾊﾟｽﾜｰﾄﾞにする
				byte[] bufferKey = deriveBytes.GetBytes(16);

				aes.Key = bufferKey;
				// IV ( Initilization Vector ) は、AesManagedにつくらせる
				aes.GenerateIV();

				// 暗号処理用ｲﾝｽﾀﾝｽを生成し、文字列を暗号化するﾌﾞﾛｯｸ
				using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
				{
					// 先頭にsaltとIVを追加した文字を作成する。
					var bytes = System.Text.Encoding.UTF8.GetBytes(String.Concat(
						System.Convert.ToBase64String(salt),
						System.Convert.ToBase64String(aes.IV),
						data
					));

					// 文字を暗号化する。
					bytes = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

					// 暗号化する文字列を作成する。
					// →1) 元の文字列の先頭にsaltとIVを追加する。
					// 　2) その後に暗号化する文字列をByteに変換した文字を追加する。
					// 　3) byte配列を暗号化→文字列に変換して返却
					return System.Convert.ToBase64String(salt.Concat(aes.IV).Concat(bytes).ToArray());
				}
			}
		}

		/// <summary>
		/// 文字列を複合化します。
		/// </summary>
		/// <param name="data">複合化する文字列</param>
		/// <param name="password">ﾊﾟｽﾜｰﾄﾞ</param>
		/// <returns>複合化した文字列</returns>
		/// <exception cref="ArgumentException">複合化する文字が空 or 先頭にsaltとIVが含まれていない or 複合化に失敗</exception>
		public static string DecryptString(string data, string password)
		{
			// 入力ﾁｪｯｸ (空ﾁｪｯｸ)
			if (String.IsNullOrWhiteSpace(data))
				throw new ArgumentException("複合化する文字が空です。");

			// 複合化する文字をbyte配列に変換する。
			var bytes = System.Convert.FromBase64String(data);

			// 入力ﾁｪｯｸ (ﾚﾝｸﾞｽﾁｪｯｸ)
			if (bytes.Length < 16 * 2)
				throw new ArgumentException("複合化する文字はｱﾙｺﾞﾘｽﾞﾑの条件を満たしていません。");

			// 引数のﾃﾞｰﾀからsaltとIV、複合化する文字列を取得する。
			var salt = bytes.Take(16).ToArray();
			var IV = bytes.Skip(16).Take(16).ToArray();
			var byteData = bytes.Skip(16 * 2).ToArray();

			// 複合に用いるICryptoTransformを生成するﾌﾞﾛｯｸ
			using (AesManaged aes = new AesManaged())
			{
				aes.BlockSize = 128;                // BlockSize = 16bytes
				aes.KeySize = 128;                  // KeySize = 16bytes
				aes.Mode = CipherMode.CBC;          // CBC mode
				aes.Padding = PaddingMode.PKCS7;    // Padding mode is "PKCS7".

				//入力されたﾊﾟｽﾜｰﾄﾞをﾍﾞｰｽに擬似乱数を新たに生成
				Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(password, salt);

				// 生成した擬似乱数から16ﾊﾞｲﾄ切り出したﾃﾞｰﾀをﾊﾟｽﾜｰﾄﾞにする
				byte[] bufferKey = deriveBytes.GetBytes(16);

				// AESにｷｰとIVを設定する。
				aes.Key = bufferKey;
				aes.IV = IV;

				// 複合処理用ｲﾝｽﾀﾝｽを生成し、文字列を複合化する
				using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
				{
					// byte配列を暗号化→文字列に変換して返却
					string decryptString = null;

					try
					{
						decryptString = System.Text.Encoding.UTF8.GetString(decryptor.TransformFinalBlock(byteData, 0, byteData.Length));
					}
					catch (Exception ex)
					{
						// 複合化で例外が発生する→ﾊﾟｽﾜｰﾄﾞが異なる。
						throw new ArgumentException("ﾊﾟｽﾜｰﾄﾞが違います。", ex);
					}

					// saltとIV、複合化した文字を取得する。
					var decryptSalt = String.Concat(decryptString.Take(24));
					var decryptIV = String.Concat(decryptString.Skip(24).Take(24));
					var decryptData = String.Concat(decryptString.Skip(24 * 2));

					// 入力ﾁｪｯｸ (複合化した文字内に含まれるsaltとIVが生のsaltとIVと一致しない)
					if (!salt.SequenceEqual(System.Convert.FromBase64String(decryptSalt)) ||
							!IV.SequenceEqual(System.Convert.FromBase64String(decryptIV)))
					{
						throw new ArgumentException("ﾊﾟｽﾜｰﾄﾞが違います。");
					}

					// 結果を返却
					return decryptData;
				}
			}
		}
	}
}