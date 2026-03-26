using GhostscriptSharp.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
		private static string GetPath(string x) => x.Replace('\\', '/');

		public static void PutPageNumber(string path)
		{
			var tmpout = FileUtil.GetTempFilePath(".pdf");
			var script = @"globaldict /MyPageCount 1 put /concatstrings { exch dup length 2 index length add string dup dup 4 2 roll copy length 4 -1 roll putinterval } bind def << /EndPage {exch pop 0 eq dup {/Helvetica 12 selectfont MyPageCount =string cvs ( / $npages) concatstrings dup stringwidth pop currentpagedevice /PageSize get 0 get exch sub 20 sub 20 moveto show globaldict /MyPageCount MyPageCount 1 add put } if } bind >> setpagedevice";

			script = script.Replace("$npages", GetPageSize(path).ToString());

			var args = new string[]
			{
				"gs",	// dummy
				"-dBATCH",
				"-dNOPAUSE",
				"-sDEVICE=pdfwrite",
				"-dPDFSETTINGS=/prepress",
				"-o",
				GetPath(tmpout),
				"-c",
				script,
				"-f",
				GetPath(path)
			};

			API.GhostScript.Call(args);

			FileUtil.Move(tmpout, path);
		}

		public static int GetPageSize(string path)
		{
			var args = new string[]
			{
				"gs",	// dummy
				"-q",
				"-dNODISPLAY",
				//$"-sFile='{GetPath(path)}'",
				//$"--permit-file-read='{GetPath(path)}'",	// ←ｺﾒﾝﾄ解除するとｴﾗｰになる
				"-c",
				$"({GetPath(path)}) (r) file runpdfbegin pdfpagecount = quit"
			};

			// なぜかｴﾗｰｺｰﾄﾞが返ってくるのでこのｺｰﾙではｴﾗｰを無視する。
			var pagesize = API.GhostScript.Call(args, false);

			return 0 < pagesize ? pagesize : GetPageSizeFromPdfText(path);
		}

		/// <summary>
		/// PDFﾌｧｲﾙのﾍﾟｰｼﾞ数をﾃｷｽﾄ形式で読み込んで取得します。
		/// </summary>
		/// <param name="path">PDFﾌｧｲﾙﾊﾟｽ</param>
		/// <returns></returns>
		/// <remarks>
		/// "/Count [Total number of pages]"という形でﾃｷｽﾄ埋め込みがされているので、
		/// 左記形式の行をすべて取得して最も大きい数をﾍﾟｰｼﾞ数として返却
		/// (ｻﾝﾌﾟﾙPDFに"/Count xxx"が複数存在していたため)
		/// </remarks>
		private static int GetPageSizeFromPdfText(string path)
		{
			var regex = new Regex(@"/Count (?<pagesize>[\d]+)");

			// ﾌｧｲﾙ読取
			return File.ReadAllLines(path, Encoding.UTF8)
				.Select(line => regex.Match(line))
				.Where(m => m.Success)
				.Select(m => m.Groups["pagesize"].Value.GetInt32())
				.MaxOrDefault(i => i, 0);
		}

		public static void Pdf2Image(string src, string dst, GhostscriptDevices devices, Size resolution, GhostscriptPageSizes pagesize, int min = 0, int max = 0)
		{
			Pdf2Image(src, dst, devices, resolution, pagesize, Size.Empty, min, max);
		}

		public static void Pdf2Image(string src, string dst, GhostscriptDevices devices, Size resolution, Size size, int min = 0, int max = 0)
		{
			Pdf2Image(src, dst, devices, resolution, GhostscriptPageSizes.UNDEFINED, size, min, max);
		}

		/// <summary>
		/// Rasterises a PDF into selected format
		/// </summary>
		/// <param name="src">PDF file to convert</param>
		/// <param name="dst">Destination file</param>
		/// <param name="settings">Conversion settings</param>
		private static void Pdf2Image(string src, string dst, GhostscriptDevices devices, Size resolution, GhostscriptPageSizes pagesize, Size size, int min = 0, int max = 0)
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

			if (devices == GhostscriptDevices.UNDEFINED)
			{
				throw new ArgumentException("An output device must be defined for Ghostscript", "GhostscriptDevices");
			}

			if (resolution.IsEmpty)
			{
				throw new ArgumentException("An output resolution must be defined", "GhostscriptSettings.Resolution");
			}

			// Output device
			args.Add(string.Format("-sDEVICE={0}", devices));

			// Pages to output
			if (min == 0 && max == 0)
			{
				args.Add("-dFirstPage=1");
			}
			else
			{
				args.Add(string.Format("-dFirstPage={0}", min));
				if (min <= max)
				{
					args.Add(string.Format("-dLastPage={0}", max));
				}
			}

			// Page size
			if (pagesize == GhostscriptPageSizes.UNDEFINED)
			{
				args.Add(string.Format("-dDEVICEWIDTHPOINTS={0}", size.Width));
				args.Add(string.Format("-dDEVICEHEIGHTPOINTS={0}", size.Height));
				args.Add("-dFIXEDMEDIA");
				args.Add("-dPDFFitPage");
			}
			else
			{
				args.Add(string.Format("-sPAPERSIZE={0}", pagesize.ToString()));
			}

			// Page resolution
			args.Add(string.Format("-dDEVICEXRESOLUTION={0}", resolution.Width));
			args.Add(string.Format("-dDEVICEYRESOLUTION={0}", resolution.Height));

			// Files
			args.Add(string.Format("-sOutputFile={0}", dst));
			args.Add(src);

			API.GhostScript.Call(args.ToArray());
		}
	}
}

namespace GhostscriptSharp.Settings
{
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