using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace TBird.Core
{
	public static class MessageService
	{
		/// <summary>ﾒｯｾｰｼﾞ表示用ｻｰﾋﾞｽ</summary>
		private static IMessageService _service = new ConsoleMessageService();

		/// <summary>
		/// ﾒｯｾｰｼﾞ表示用ｻｰﾋﾞｽを切り替えます。
		/// </summary>
		/// <param name="value">ﾒｯｾｰｼﾞ表示用ｻｰﾋﾞｽ</param>
		public static void SetService(IMessageService value)
		{
			_service = value;
		}

		/// <summary>
		/// ｴﾗｰをﾒｯｾｰｼﾞ処理します。
		/// </summary>
		/// <param name="message">ﾒｯｾｰｼﾞ</param>
		public static void Error(string message,
				[CallerMemberName] string callerMemberName = "",
				[CallerFilePath] string callerFilePath = "",
				[CallerLineNumber] int callerLineNumber = 0)
		{
			_service.Error(message, callerMemberName, callerFilePath, callerLineNumber);
		}

		/// <summary>
		/// 情報をﾒｯｾｰｼﾞ処理します。
		/// </summary>
		/// <param name="message">ﾒｯｾｰｼﾞ</param>
		public static void Info(string message,
				[CallerMemberName] string callerMemberName = "",
				[CallerFilePath] string callerFilePath = "",
				[CallerLineNumber] int callerLineNumber = 0)
		{
			_service.Info(message, callerMemberName, callerFilePath, callerLineNumber);
		}

		/// <summary>
		/// 確認をﾒｯｾｰｼﾞ処理します。
		/// </summary>
		/// <param name="message">ﾒｯｾｰｼﾞ</param>
		public static bool Confirm(string message,
				[CallerMemberName] string callerMemberName = "",
				[CallerFilePath] string callerFilePath = "",
				[CallerLineNumber] int callerLineNumber = 0)
		{
			return _service.Confirm(message, callerMemberName, callerFilePath, callerLineNumber);
		}

		/// <summary>
		/// ﾃﾞﾊﾞｯｸﾞﾒｯｾｰｼﾞ処理します。
		/// </summary>
		/// <param name="message">ﾒｯｾｰｼﾞ</param>
		public static void Debug(string message,
				[CallerMemberName] string callerMemberName = "",
				[CallerFilePath] string callerFilePath = "",
				[CallerLineNumber] int callerLineNumber = 0)
		{
			_service.Debug(message, callerMemberName, callerFilePath, callerLineNumber);
		}

		/// <summary>
		/// 例外をﾒｯｾｰｼﾞ処理します。
		/// </summary>
		/// <param name="message">ﾒｯｾｰｼﾞ</param>
		public static void Exception(Exception exception,
				[CallerMemberName] string callerMemberName = "",
				[CallerFilePath] string callerFilePath = "",
				[CallerLineNumber] int callerLineNumber = 0)
		{
			_service.Exception(exception, callerMemberName, callerFilePath, callerLineNumber);
		}

		/// <summary>
		/// 処理を計測します。
		/// </summary>
		public static IDisposable Measure(string? message = null,
				[CallerMemberName] string callerMemberName = "",
				[CallerFilePath] string callerFilePath = "",
				[CallerLineNumber] int callerLineNumber = 0)
		{
			// 時間計測識別用のｷｰを作成する。
			message = message ?? Guid.NewGuid().ToString();
			// ｽﾄｯﾌﾟｳｫｯﾁ開始
			var stopwatch = new Stopwatch(); stopwatch.Start();
			// 開始ﾒｯｾｰｼﾞ
			Debug($"{message} start timing.", callerMemberName, callerFilePath, callerLineNumber);
			// Dispose処理で終了ﾒｯｾｰｼﾞ
			return new Disposer<Stopwatch>(stopwatch, x => Debug($"{message} process took {x.Elapsed:d\\.hh\\:mm\\:ss\\.fff} (...TimeSpan)", callerMemberName, callerFilePath, callerLineNumber));
		}

		public static void AppendLogfile(string message)
		{
			lock (_lock)
			{
				var dir = Directories.GetAbsolutePath("log");
				var tmp = Path.Combine(dir, $"{DateTime.Now.ToString("yyyy-MM-dd")}.log");

				// ﾃﾞｨﾚｸﾄﾘを作成
				Directory.CreateDirectory(dir);

				try
				{
					File.AppendAllText(tmp, $"{message}\n");
				}
				catch (Exception ex)
				{
					Debug(ex.ToString());
				}
			}
		}

		private static object _lock = new object();
	}
}