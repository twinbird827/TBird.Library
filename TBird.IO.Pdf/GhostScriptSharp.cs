using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using TBird.Core;

namespace GhostscriptSharp.API
{
	internal class GhostScript
	{
#if WIN64
		private const string lib_dll = "gsdll64.dll";
#else
		private const string lib_dll = "gsdll32.dll";
#endif

		#region Hooks into Ghostscript DLL

		[DllImport(lib_dll, EntryPoint = "gsapi_new_instance")]
		private static extern int gsapi_new_instance(out IntPtr pinstance, IntPtr caller_handle);

		[DllImport(lib_dll, EntryPoint = "gsapi_init_with_args")]
		private static extern int gsapi_init_with_args(IntPtr instance, int argc, string[] argv);

		[DllImport(lib_dll, EntryPoint = "gsapi_exit")]
		private static extern int gsapi_exit(IntPtr instance);

		[DllImport(lib_dll, EntryPoint = "gsapi_delete_instance")]
		private static extern void gsapi_delete_instance(IntPtr instance);

		[DllImport(lib_dll, EntryPoint = "gsapi_set_stdio", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public static extern int gsapi_set_stdio(IntPtr instance, gs_stdio_handler stdin, gs_stdio_handler stdout, gs_stdio_handler stderr);

		public delegate int gs_stdio_handler(IntPtr caller_handle, IntPtr buffer, int len);

		#endregion

		/// <summary>
		/// Calls the Ghostscript API with a collection of arguments to be passed to it
		/// </summary>
		public static int Call(string[] args, bool error = true)
		{
			// Get a pointer to an instance of the Ghostscript API and run the API with the current arguments
			IntPtr instance;
			int code;

			lock (_lock)
			{
				code = gsapi_new_instance(out instance, IntPtr.Zero);

				ThrowIfErrorOccurred(code, true);

				try
				{
					var sb = new StringBuilder();

					gs_stdio_handler raise_stdin = (caller_handle, buffer, len) =>
					{
						var output = Marshal.PtrToStringAnsi(buffer);
						return len;
					};

					gs_stdio_handler raise_stdout = (caller_handle, buffer, len) =>
					{
						var output = Marshal.PtrToStringAnsi(buffer);
						sb.Append(output.Substring(0, len));
						return len;
					};

					gs_stdio_handler raise_stderr = raise_stdout;

					code = gsapi_set_stdio(instance, raise_stdin, raise_stdout, raise_stderr);

					ThrowIfErrorOccurred(code, true);

					code = gsapi_init_with_args(instance, args.Length, args);

					ThrowIfErrorOccurred(code, error);

					return sb.ToString().GetInt32();
				}
				finally
				{
					gsapi_exit(instance);
					gsapi_delete_instance(instance);
				}
			}
		}

		private static void ThrowIfErrorOccurred(int code, bool error)
		{
			if (error && code < 0)
			{
				throw new ExternalException("Ghostscript conversion error", code);
			}
		}

		/// <summary>
		/// GS can only support a single instance, so we need to bottleneck any multi-threaded systems.
		/// </summary>
		private static object _lock = new object();
	}
}

namespace GhostscriptSharp
{
	/// <summary>
	/// Wraps the Ghostscript API with a C# interface
	/// </summary>
	public class GhostscriptWrapper
	{

		public static int GetPageSize(string path)
		{
			var args = new string[]
			{
				"gs",	// dummy
				"-q",
				"-dNODISPLAY",
				//$"-sFile='{path.Replace('\\', '/')}'",
				//$"--permit-file-read='{path.Replace('\\', '/')}'",	// ←ｺﾒﾝﾄ解除するとｴﾗｰになる
				"-c",
				$"({path.Replace('\\', '/')}) (r) file runpdfbegin pdfpagecount = quit"
			};

			// なぜかｴﾗｰｺｰﾄﾞが返ってくるのでこのｺｰﾙではｴﾗｰを無視する。
			return API.GhostScript.Call(args, false);
		}

		/// <summary>
		/// Rasterises a PDF into selected format
		/// </summary>
		/// <param name="inputPath">PDF file to convert</param>
		/// <param name="outputPath">Destination file</param>
		/// <param name="settings">Conversion settings</param>
		public static void GenerateOutput(string inputPath, string outputPath, GhostscriptSettings settings)
		{
			var args = new List<string>(new[]
			{
				// Keep gs from writing information to standard output
                "-q",
				"-dQUIET",

				"-dPARANOIDSAFER",       // Run this command in safe mode
                "-dBATCH",               // Keep gs from going into interactive mode
                "-dNOPAUSE",             // Do not prompt and pause for each page
                "-dNOPROMPT",            // Disable prompts for user interaction
                "-dMaxBitmap=500000000", // Set high for better performance
				"-dNumRenderingThreads=4", // Multi-core, come-on!

                // Configure the output anti-aliasing, resolution, etc
                "-dAlignToPixels=0",
				"-dGridFitTT=0",
				"-dTextAlphaBits=4",
				"-dGraphicsAlphaBits=4"
			});

			if (settings.Device == Settings.GhostscriptDevices.UNDEFINED)
			{
				throw new ArgumentException("An output device must be defined for Ghostscript", "GhostscriptSettings.Device");
			}

			if (settings.Page.AllPages == false && (settings.Page.Start <= 0 && settings.Page.End < settings.Page.Start))
			{
				throw new ArgumentException("Pages to be printed must be defined.", "GhostscriptSettings.Pages");
			}

			if (settings.Resolution.IsEmpty)
			{
				throw new ArgumentException("An output resolution must be defined", "GhostscriptSettings.Resolution");
			}

			if (settings.Size.Native == Settings.GhostscriptPageSizes.UNDEFINED && settings.Size.Manual.IsEmpty)
			{
				throw new ArgumentException("Page size must be defined", "GhostscriptSettings.Size");
			}

			// Output device
			args.Add(string.Format("-sDEVICE={0}", settings.Device));

			// Pages to output
			if (settings.Page.AllPages)
			{
				args.Add("-dFirstPage=1");
			}
			else
			{
				args.Add(string.Format("-dFirstPage={0}", settings.Page.Start));
				if (settings.Page.End >= settings.Page.Start)
				{
					args.Add(string.Format("-dLastPage={0}", settings.Page.End));
				}
			}

			// Page size
			if (settings.Size.Native == Settings.GhostscriptPageSizes.UNDEFINED)
			{
				args.Add(string.Format("-dDEVICEWIDTHPOINTS={0}", settings.Size.Manual.Width));
				args.Add(string.Format("-dDEVICEHEIGHTPOINTS={0}", settings.Size.Manual.Height));
				args.Add("-dFIXEDMEDIA");
				args.Add("-dPDFFitPage");
			}
			else
			{
				args.Add(string.Format("-sPAPERSIZE={0}", settings.Size.Native.ToString()));
			}

			// Page resolution
			args.Add(string.Format("-dDEVICEXRESOLUTION={0}", settings.Resolution.Width));
			args.Add(string.Format("-dDEVICEYRESOLUTION={0}", settings.Resolution.Height));

			// Files
			args.Add(string.Format("-sOutputFile={0}", outputPath));
			args.Add(inputPath);

			API.GhostScript.Call(args.ToArray());
		}
	}

	/// <summary>
	/// Ghostscript settings
	/// </summary>
	public class GhostscriptSettings
	{
		private Settings.GhostscriptDevices _device;
		private Settings.GhostscriptPages _pages = new Settings.GhostscriptPages();
		private Size _resolution;
		private Settings.GhostscriptPageSize _size = new Settings.GhostscriptPageSize();

		public Settings.GhostscriptDevices Device
		{
			get { return this._device; }
			set { this._device = value; }
		}

		public Settings.GhostscriptPages Page
		{
			get { return this._pages; }
			set { this._pages = value; }
		}

		public Size Resolution
		{
			get { return this._resolution; }
			set { this._resolution = value; }
		}

		public Settings.GhostscriptPageSize Size
		{
			get { return this._size; }
			set { this._size = value; }
		}
	}
}

namespace GhostscriptSharp.Settings
{
	/// <summary>
	/// Which pages to output
	/// </summary>
	public class GhostscriptPages
	{
		private bool _allPages = true;
		private int _start;
		private int _end;

		/// <summary>
		/// Output all pages avaialble in document
		/// </summary>
		public bool AllPages
		{
			set
			{
				this._start = -1;
				this._end = -1;
				this._allPages = true;
			}
			get
			{
				return this._allPages;
			}
		}

		/// <summary>
		/// Start output at this page (1 for page 1)
		/// </summary>
		public int Start
		{
			set
			{
				this._allPages = false;
				this._start = value;
			}
			get
			{
				return this._start;
			}
		}

		/// <summary>
		/// Page to stop output at
		/// </summary>
		public int End
		{
			set
			{
				this._allPages = false;
				this._end = value;
			}
			get
			{
				return this._end;
			}
		}
	}

	/// <summary>
	/// Output devices for GhostScript
	/// </summary>
	public enum GhostscriptDevices
	{
		UNDEFINED,
		png16m,
		pnggray,
		png256,
		png16,
		pngmono,
		pngalpha,
		jpeg,
		jpeggray,
		tiffgray,
		tiff12nc,
		tiff24nc,
		tiff32nc,
		tiffsep,
		tiffcrle,
		tiffg3,
		tiffg32d,
		tiffg4,
		tifflzw,
		tiffpack,
		faxg3,
		faxg32d,
		faxg4,
		bmpmono,
		bmpgray,
		bmpsep1,
		bmpsep8,
		bmp16,
		bmp256,
		bmp16m,
		bmp32b,
		pcxmono,
		pcxgray,
		pcx16,
		pcx256,
		pcx24b,
		pcxcmyk,
		psdcmyk,
		psdrgb,
		pdfwrite,
		pswrite,
		epswrite,
		pxlmono,
		pxlcolor
	}

	/// <summary>
	/// Output document physical dimensions
	/// </summary>
	public class GhostscriptPageSize
	{
		private GhostscriptPageSizes _fixed;
		private Size _manual;

		/// <summary>
		/// Custom document size
		/// </summary>
		public Size Manual
		{
			set
			{
				this._fixed = GhostscriptPageSizes.UNDEFINED;
				this._manual = value;
			}
			get
			{
				return this._manual;
			}
		}

		/// <summary>
		/// Standard paper size
		/// </summary>
		public GhostscriptPageSizes Native
		{
			set
			{
				this._fixed = value;
				this._manual = new Size(0, 0);
			}
			get
			{
				return this._fixed;
			}
		}

	}

	/// <summary>
	/// Native page sizes
	/// </summary>
	/// <remarks>
	/// Missing 11x17 as enums can't start with a number, and I can't be bothered
	/// to add in logic to handle it - if you need it, do it yourself.
	/// </remarks>
	public enum GhostscriptPageSizes
	{
		UNDEFINED,
		ledger,
		legal,
		letter,
		lettersmall,
		archE,
		archD,
		archC,
		archB,
		archA,
		a0,
		a1,
		a2,
		a3,
		a4,
		a4small,
		a5,
		a6,
		a7,
		a8,
		a9,
		a10,
		isob0,
		isob1,
		isob2,
		isob3,
		isob4,
		isob5,
		isob6,
		c0,
		c1,
		c2,
		c3,
		c4,
		c5,
		c6,
		jisb0,
		jisb1,
		jisb2,
		jisb3,
		jisb4,
		jisb5,
		jisb6,
		b0,
		b1,
		b2,
		b3,
		b4,
		b5,
		flsa,
		flse,
		halfletter
	}
}