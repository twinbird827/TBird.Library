using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TBird.Core
{
	[System.Security.SuppressUnmanagedCodeSecurity]
	public static class Win32Methods
	{
		[DllImport("User32.dll", EntryPoint = "SendMessage")]
		public static extern int SendMessageGetTextLength(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("User32.dll")]
		public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, int lParam);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		public static extern uint MapVirtualKey(uint uCode, uint uMapType);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool PostMessage(IntPtr hWnd, int Msg, uint wParam, uint lParam);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

		[DllImport("user32.dll")]
		public static extern IntPtr FindWindowEx(IntPtr hWnd, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

		[DllImport("user32")]
		public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

		public static string GetClassName(IntPtr hWnd)
		{
			var buffer = new StringBuilder(256);
			GetClassName(hWnd, buffer, buffer.Capacity);
			return buffer.ToString();
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("user32.dll")]
		public static extern IntPtr CreateWindowEx(
			uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y,
			int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam
		);

		[DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterPowerSettingNotification", CallingConvention = CallingConvention.StdCall)]
		public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, uint Flags);

		[DllImport("user32.dll", EntryPoint = "UnregisterPowerSettingNotification", CallingConvention = CallingConvention.StdCall)]
		public static extern bool UnregisterPowerSettingNotification(IntPtr RegistrationHandle);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern bool ReleaseCapture();

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern uint SendMessage(IntPtr hWnd, uint wMsg, uint wParam, uint lParam);

		[DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[ComVisible(false)]
		public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[ComVisible(false)]
		public static extern int GetSysColor(int nIndex);

		[DllImport("user32", SetLastError = true)]
		[ComVisible(false)]
		public static extern int ShowScrollBar(IntPtr handle, int wBar, int bShow);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern IntPtr SetActiveWindow(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern int PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("wininet.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern bool InternetGetCookie(string lpszUrl, string lpszCookieName, StringBuilder lpCookieData, ref long lpdwSize);

		[DllImport("wininet.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern bool InternetSetCookie(string lpszUrl, string lpszCookieName, string lpCookieData);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern bool SetSysColors(int cElements, int[] lpaElements, int[] lpaRgbValues);

		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		[ComVisible(false)]
		public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern int RegisterHotKey(IntPtr HWnd, int ID, int MOD_KEY, int KEY);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern int UnregisterHotKey(IntPtr HWnd, int ID);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

		[DllImport("user32.dll", SetLastError = true)]
		[ComVisible(false)]
		public static extern bool GetKeyboardState(byte[] lpKeyState);

		[DllImport("user32.DLL", CharSet = CharSet.Auto)]
		public static extern int ShowWindow(System.IntPtr hWnd, int nCmdShow);

		[DllImport("advapi32.DLL", SetLastError = true)]
		public static extern bool OpenProcessToken(System.IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

		[DllImport("kernel32.DLL", SetLastError = true)]
		public static extern bool CloseHandle(System.IntPtr hHandle);

		[DllImport("kernel32.dll")]
		public static extern int GetPrivateProfileString(
					string lpApplicationName,
					string lpKeyName,
					string lpDefault,
					StringBuilder lpReturnedstring,
					int nSize,
					string lpFileName);

		[StructLayout(LayoutKind.Sequential)]
		public struct STORAGE_DEVICE_NUMBER
		{
			public int DeviceType;
			public int DeviceNumber;
			public int PartitionNumber;
		};

		public enum DriveType : uint
		{
			/// <summary>The drive type cannot be determined.</summary>
			DRIVE_UNKNOWN = 0,      //DRIVE_UNKNOWN

			/// <summary>The root path is invalid, for example, no volume is mounted at the path.</summary>
			DRIVE_NO_ROOT_DIR = 1,  //DRIVE_NO_ROOT_DIR

			/// <summary>The drive is a type that has removable media, for example, a floppy drive or removable hard disk.</summary>
			DRIVE_REMOVABLE = 2,    //DRIVE_REMOVABLE

			/// <summary>The drive is a type that cannot be removed, for example, a fixed hard drive.</summary>
			DRIVE_FIXED = 3,        //DRIVE_FIXED

			/// <summary>The drive is a remote (network) drive.</summary>
			DRIVE_REMOTE = 4,       //DRIVE_REMOTE

			/// <summary>The drive is a CD-ROM drive.</summary>
			DRIVE_CDROM = 5,        //DRIVE_CDROM

			/// <summary>The drive is a RAM disk.</summary>
			DRIVE_RAMDISK = 6       //DRIVE_RAMDISK
		}

		[StructLayout(LayoutKind.Sequential)]
		public class SP_DEVINFO_DATA
		{
			public int cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
			public Guid classGuid = Guid.Empty; // temp
			public int devInst = 0; // dumy
			public int reserved = 0;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		public struct SP_DEVICE_INTERFACE_DETAIL_DATA
		{
			public int cbSize;
			public short devicePath;
		}

		[StructLayout(LayoutKind.Sequential)]
		public class SP_DEVICE_INTERFACE_DATA
		{
			public int cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
			public Guid interfaceClassGuid = Guid.Empty; // temp
			public int flags = 0;
			public int reserved = 0;
		}

		public enum PNP_VETO_TYPE
		{
			Ok,
			TypeUnknown,
			LegacyDevice,
			PendingClose,
			WindowsApp,
			WindowsService,
			OutstandingOpen,
			Device,
			Driver,
			IllegalDeviceRequest,
			InsufficientPower,
			NonDisableable,
			LegacyDriver,
			InsufficientRights
		}

		[DllImport("kernel32.dll")]
		public static extern DriveType GetDriveType([MarshalAs(UnmanagedType.LPStr)] string lpRootPathName);

		[DllImport("kernel32.dll")]
		public static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr CreateFile(
			string lpFileName,
			int dwDesiredAccess,
			int dwShareMode,
			IntPtr lpSecurityAttributes,
			int dwCreationDisposition,
			int dwFlagsAndAttributes,
			IntPtr hTemplateFile);

		[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
		public static extern bool DeviceIoControl(
			IntPtr hDevice,
			int dwIoControlCode,
			IntPtr lpInBuffer,
			int nInBufferSize,
			IntPtr lpOutBuffer,
			int nOutBufferSize,
			out int lpBytesReturned,
			IntPtr lpOverlapped);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiEnumDeviceInterfaces(
			IntPtr deviceInfoSet,
			SP_DEVINFO_DATA deviceInfoData,
			ref Guid interfaceClassGuid,
			int memberIndex,
			SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetupDiGetDeviceInterfaceDetail(
			IntPtr deviceInfoSet,
			SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
			IntPtr deviceInterfaceDetailData,
			int deviceInterfaceDetailDataSize,
			ref int requiredSize,
			SP_DEVINFO_DATA deviceInfoData);

		[DllImport("setupapi.dll")]
		public static extern uint SetupDiDestroyDeviceInfoList(
			IntPtr deviceInfoSet);

		[DllImport("setupapi.dll")]
		public static extern int CM_Get_Parent(
			ref int pdnDevInst,
			int dnDevInst,
			int ulFlags);

		[DllImport("setupapi.dll")]
		public static extern int CM_Request_Device_Eject(
			int dnDevInst,
			out PNP_VETO_TYPE pVetoType,
			StringBuilder pszVetoName,
			int ulNameLength,
			int ulFlags);

		[DllImport("setupapi.dll", EntryPoint = "CM_Request_Device_Eject")]
		public static extern int CM_Request_Device_Eject_NoUi(
			int dnDevInst,
			IntPtr pVetoType,
			StringBuilder pszVetoName,
			int ulNameLength,
			int ulFlags);

		[DllImport("setupapi.dll")]
		public static extern IntPtr SetupDiGetClassDevs(
			ref Guid classGuid,
			int enumerator,
			IntPtr hwndParent,
			int flags);

		[DllImport("kernel32.dll")]
		public static extern int WritePrivateProfileString(
					string lpApplicationName,
					string lpKeyName,
					string lpstring,
					string lpFileName);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		public static extern Boolean DeleteDC(IntPtr hDC);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		public static extern Int32 GetDeviceCaps(DCSafeHandle hDC, Int32 nIndex);

		[DllImport("gdi32.dll", EntryPoint = "CreateDC", CharSet = CharSet.Auto)]
		public static extern DCSafeHandle IntCreateDC(String lpszDriver,
			String lpszDeviceName, String lpszOutput, IntPtr devMode);

		private static void Win32Shutdown(int shutdownFlags)
		{
			Thread thread = new Thread(() =>
			{
				// Win32_OperatingSystemクラスを作成する
				using (ManagementClass managementClass = new ManagementClass("Win32_OperatingSystem"))
				{
					// Win32_OperatingSystemオブジェクトを取得する
					managementClass.Get();
					// 権限を有効化する
					managementClass.Scope.Options.EnablePrivileges = true;

					// WMIのオブジェクトのコレクションを取得する
					ManagementObjectCollection managementObjectCollection = managementClass.GetInstances();
					// WMIのオブジェクトを列挙する
					foreach (ManagementObject managementObject in managementObjectCollection)
					{
						// InvokeMethodでWMIのメソッドを実行する
						managementObject.InvokeMethod(
							// 実行メソッド名
							"Win32Shutdown",
							// メソッドの引数をオブジェクト配列で指定
							new object[] { shutdownFlags, 0 }
							);

						// WMIのオブジェクトのリソースを開放
						managementObject.Dispose();
					}
				}
			});
			// スレッドモデルをSTAに設定する
			thread.SetApartmentState(ApartmentState.STA);
			// スレッドを実行する
			thread.Start();
			// スレッドの終了を待つ
			thread.Join();
		}

		private static void RunWin32Shutdown(Win32ShutdownFlags shutdownFlags)
		{
			Win32Shutdown((int)(shutdownFlags | Win32ShutdownFlags.Forced));
		}

		/// <summary>
		/// Windows ﾛｸﾞｵﾌを実行します。
		/// </summary>
		public static void Win32Logoff()
		{
			RunWin32Shutdown(Win32ShutdownFlags.Logoff);
		}

		/// <summary>
		/// Windows ｼｬｯﾄﾀﾞｳﾝを実行します。
		/// </summary>
		public static void Win32Shutdown()
		{
			RunWin32Shutdown(Win32ShutdownFlags.Shutdown);
		}

		/// <summary>
		/// Windows 再起動を実行します。
		/// </summary>
		public static void Win32Reboot()
		{
			RunWin32Shutdown(Win32ShutdownFlags.Reboot);
		}

		/// <summary>
		/// Windows 電源OFFを実行します。
		/// </summary>
		public static void Win32PowerOff()
		{
			RunWin32Shutdown(Win32ShutdownFlags.PowerOff);
		}
	}

	public sealed class DCSafeHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
	{
		private DCSafeHandle() : base(true)
		{
		}

		protected override Boolean ReleaseHandle()
		{
			return Win32Methods.DeleteDC(base.handle);
		}
	}
}