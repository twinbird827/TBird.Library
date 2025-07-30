using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TBird.Core;

namespace TBird.Wpf
{
	public static class WpfDialog
	{
		/// <summary>
		/// ﾌｧｲﾙﾊﾟｽを取得します。
		/// </summary>
		/// <param name="initializePath">初期ﾊﾟｽ</param>
		public static string ShowSaveFile(string initializePath, string filter)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog()
			{
				Title = WpfConst.H_ShowSaveFileDialog,
				// ﾌｫﾙﾀﾞ選択ﾀﾞｲｱﾛｸﾞの場合は true
				// ﾀﾞｲｱﾛｸﾞが表示されたときの初期ﾃﾞｨﾚｸﾄﾘを指定
				InitialDirectory = !string.IsNullOrEmpty(initializePath) ? Path.GetDirectoryName(initializePath) : string.Empty,
				// ﾃﾞﾌｫﾙﾄﾌｧｲﾙ名
				FileName = !string.IsNullOrEmpty(initializePath) ? Path.GetFileName(initializePath) : string.Empty,
				// 複数選択を許可するかどうか
				Multiselect = false,
				// ﾌｨﾙﾀ
				Filter = filter,
				// ﾌｧｲﾙの存在ﾁｪｯｸをするかどうか
				CheckFileExists = false,
				// ﾊﾟｽの存在ﾁｪｯｸをするかどうか
				CheckPathExists = false,
			};

			// ﾀﾞｲｱﾛｸﾞ起動時にﾌｧｲﾙ名項目が見切れてしまうので、起動時にﾌｧｲﾙ名項目でHOMEﾎﾞﾀﾝを押す処理を追加
			new HandlerHelper().AssignHandle();

			if ((bool)dialog.ShowDialog())
			{
				return dialog.FileName;
			}
			else
			{
				return string.Empty;
			}
			//var dialog = new CommonOpenFileDialog()
			//{
			//    //Title = WpfConst.H_ShowSaveFileDialog,
			//    //// ﾌｫﾙﾀﾞ選択ﾀﾞｲｱﾛｸﾞの場合は true
			//    //IsFolderPicker = false,
			//    //// ﾀﾞｲｱﾛｸﾞが表示されたときの初期ﾃﾞｨﾚｸﾄﾘを指定
			//    //InitialDirectory = !string.IsNullOrEmpty(initializePath) ? Path.GetDirectoryName(initializePath) : string.Empty,
			//    //// ﾃﾞﾌｫﾙﾄﾌｧｲﾙ名
			//    //DefaultFileName = !string.IsNullOrEmpty(initializePath) ? Path.GetFileName(initializePath) : string.Empty,
			//    //// ﾕｰｻﾞｰが最近したｱｲﾃﾑの一覧を表示するかどうか
			//    //AddToMostRecentlyUsedList = false,
			//    //// ﾕｰｻﾞｰがﾌｫﾙﾀﾞやﾗｲﾌﾞﾗﾘなどのﾌｧｲﾙｼｽﾃﾑ以外の項目を選択できるようにするかどうか
			//    //AllowNonFileSystemItems = false,
			//    //// 存在するﾌｧｲﾙのみ許可するかどうか
			//    //EnsureFileExists = false,
			//    //// 存在するﾊﾟｽのみ許可するかどうか
			//    //EnsurePathExists = false,
			//    //// 読み取り専用ﾌｧｲﾙを許可するかどうか
			//    //EnsureReadOnly = false,
			//    //// 有効なﾌｧｲﾙ名のみ許可するかどうか（ﾌｧｲﾙ名を検証するかどうか）
			//    //EnsureValidNames = true,
			//    //// 複数選択を許可するかどうか
			//    //Multiselect = false,
			//    //// PC やﾈｯﾄﾜｰｸなどの場所を表示するかどうか
			//    //ShowPlacesList = true,
			//};

			//var source = filter.Split('|');
			//var filters = Enumerable.Range(0, source.Length / 2)
			//    .Select(i => new CommonFileDialogFilter(source[i * 2], source[i * 2 + 1]));

			//dialog.Filters.AddRange(filters);

			//// ﾀﾞｲｱﾛｸﾞ起動時にﾌｧｲﾙ名項目が見切れてしまうので、起動時にﾌｧｲﾙ名項目でHOMEﾎﾞﾀﾝを押す処理を追加
			//new HandlerHelper().AssignHandle();

			//if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
			//{
			//    var index = dialog.SelectedFileTypeIndex - 1;
			//    var filename = dialog.FileName;

			//    if (index == dialog.Filters.Count - 1)
			//    {
			//        // 選択中のﾌｨﾙﾀが全ﾌｧｲﾙなら何もせず返却
			//        return filename;
			//    }
			//    else if (filename.EndsWith(dialog.Filters[index].Extensions[0]))
			//    {
			//        // 拡張子が同じなら
			//        return filename;
			//    }
			//    else
			//    {
			//        // 拡張子が違うなら変更して返却
			//        return Path.ChangeExtension(filename, dialog.Filters[index].Extensions[0]);
			//    }
			//}
			//else
			//{
			//    return string.Empty;
			//}
		}

		private class HandlerHelper : NativeWindow
		{
			private bool initial = true;

			private const uint WS_VISIBLE = 0x10000000;
			private readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
			private readonly IntPtr NULL = IntPtr.Zero;

			public void AssignHandle()
			{

				IntPtr hWnd = Win32Methods.CreateWindowEx(0, "Message", null, WS_VISIBLE, 0, 0, 0, 0, HWND_MESSAGE, NULL, NULL, NULL);

				// 次に起動するｳｨﾝﾄﾞｳをｱｻｲﾝする。
				AssignHandle(hWnd);
			}

			protected override void WndProc(ref Message m)
			{
				if (!initial)
				{
					base.WndProc(ref m);
					return;
				}

				if (initial && m.Msg == Win32Messages.WM_ACTIVATE)
				{
					// 起動したｳｨﾝﾄﾞｳの編集項目のﾊﾝﾄﾞﾙをｱｻｲﾝする。
					GetAllChildWindows(GetWindow(m.LParam))
						.Where(x => x.ClassName == "Edit")
						.ForEach(x => new HandlerHelper().AssignHandle(x.hWnd));
					initial = false;
				}

				if (initial && m.Msg == Win32Messages.EM_SETSEL)
				{
					var length = Win32Methods.SendMessageGetTextLength(m.HWnd, Win32Messages.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
					if (0 < length)
					{
						initial = false;
						// 編集項目で内部処理として全選択したらHOMEﾎﾞﾀﾝの押下処理を行う。
						PostMessage(m.HWnd, Win32Messages.WM_KEYDOWN, Keys.Home);
						PostMessage(m.HWnd, Win32Messages.WM_KEYUP, Keys.Home);
					}
				}
				base.WndProc(ref m);
			}

			/// <summary>指定したKeyCodeでPostMessageを実行する。</summary>
			private void PostMessage(IntPtr hWnd, int msg, Keys keycode)
			{
				bool keydown = msg == Win32Messages.WM_KEYDOWN || msg == Win32Messages.WM_SYSKEYDOWN;
				uint scancode = Win32Methods.MapVirtualKey((uint)keycode, 0);
				uint lParam;

				//KEY DOWN
				lParam = 0x00000001 | (scancode << 16);

				if (!keydown)
				{
					//KEY UP
					lParam |= 0xC0000000;  // set previous key and transition states (bits 30 and 31)
				}

				Win32Methods.PostMessage(hWnd, msg, (uint)keycode, lParam);
			}

			/// <summary>ｳｨﾝﾄﾞｳﾊﾝﾄﾞﾙの孫以降を含めた全ての子ｳｨﾝﾄﾞｳを取得する。</summary>
			private static IEnumerable<WindowInfo> GetAllChildWindows(WindowInfo x)
			{
				yield return x;

				// 子ｳｨﾝﾄﾞｳを対象に再帰処理
				foreach (var y in GetChildWindows(x.hWnd).SelectMany(z => GetAllChildWindows(z)))
				{
					yield return y;
				}
			}

			/// <summary>ｳｨﾝﾄﾞｳﾊﾝﾄﾞﾙの子ｳｨﾝﾄﾞｳを取得する。</summary>
			private static IEnumerable<WindowInfo> GetChildWindows(IntPtr hParentWindow)
			{
				var hWnd = IntPtr.Zero;
				while ((hWnd = Win32Methods.FindWindowEx(hParentWindow, hWnd, null, null)) != IntPtr.Zero)
				{
					yield return GetWindow(hWnd);
				}
			}

			/// <summary>ｳｨﾝﾄﾞｳﾊﾝﾄﾞﾙのｸﾗｽ名を取得して返却する。</summary>
			private static WindowInfo GetWindow(IntPtr hWnd)
			{
				return new WindowInfo()
				{
					hWnd = hWnd,
					ClassName = Win32Methods.GetClassName(hWnd)
				};
			}

			/// <summary>ｳｨﾝﾄﾞｳﾊﾝﾄﾞﾙとｸﾗｽ名の情報ｸﾗｽ</summary>
			private class WindowInfo
			{
				// ｸﾗｽ名
				public string ClassName;

				// ｳｨﾝﾄﾞｳﾊﾝﾄﾞﾙ
				public IntPtr hWnd;
			}
		}

	}
}