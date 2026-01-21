using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Printing;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TBird.Core;

namespace TBird.Wpf.Reports
{
	public abstract class ReportViewModel : BindableBase
	{
		/// <summary>
		/// 印刷の向き(<see cref="true"/>=横向き, <see cref="false"/>=縦向き)
		/// </summary>
		public bool Landscape { get; protected set; }

		/// <summary>
		/// 画面上の操作を可能とするかどうか
		/// </summary>
		public bool IsGUI { get; protected set; }

		/// <summary>
		/// ﾍﾟｰｼﾞ数
		/// </summary>
		public int Page
		{
			get => _Page;
			set => SetProperty(ref _Page, value);
		}
		private int _Page;

		/// <summary>
		/// 全ﾍﾟｰｼﾞ数
		/// </summary>
		public int Total
		{
			get => _Total;
			set => SetProperty(ref _Total, value);
		}
		private int _Total;

		/// <summary>
		/// 印刷処理を実行します。
		/// </summary>
		/// <returns></returns>
		public async Task PrintAsync()
		{
			try
			{
				using (WpfUtil.GetDispatcherShutdownDisposable())
				{
					// 出力ﾄﾞｷｭﾒﾝﾄ作成
					using var doc = await TaskUtil.WaitAsync(() =>
					{
						var settings = GetPrinterSettings();
						if (settings == null) return null;

						var doc = new PrintDocument();

						doc.PrinterSettings = settings;
						doc.PrintController = new StandardPrintController();

						// 用紙ｻｲｽﾞ指定
						doc.DefaultPageSettings.PaperSize = GetPaperSize(doc);

						return doc;
					});

					if (doc == null) return;

					// 印刷ｻｲｽﾞ
					var size = await TaskUtil.WaitAsync(() =>
					{
						var paper = doc.DefaultPageSettings.PaperSize;
						var area = doc.DefaultPageSettings.PrintableArea;

						// 印刷ｻｲｽﾞ
						var size = doc.PrinterSettings.DefaultPageSettings.Landscape
							// 横向き
							? new Rect((int)area.Top, (int)area.Left, (int)area.Height, (int)area.Width)
							// 縦向き
							: new Rect((int)area.Left, (int)area.Top, (int)area.Width, (int)area.Height);

						return size;
					});

					// 余白無し
					doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

					// 初期化処理(印刷ﾃﾞｰﾀ取得)は非同期で行う
					await WpfUtil.ExecuteOnBACK(Initialize);
					// 描写用Window作成
					var window = GetWindow(GetWindow, size);

					// 印刷ｲﾍﾞﾝﾄ
					doc.PrintPage += (s, e) =>
					{
						// 各ﾍﾟｰｼﾞ描写用設定
						OnPage(Page);

						// 描写内容の確定
						WpfUtil.DoEvents();

						// 画像をﾍﾟｰｼﾞに描き込む
						using (var image = ToDrawingImage(window, size))
						{
							e.Graphics.DrawImage(image, e.MarginBounds);
						}

						Page = Page + 1;

						// 次の画像があるか判定し印刷の続行 / 停止を判断する
						e.HasMorePages = Page <= Total;
					};

					doc.EndPrint += (s, e) =>
					{
						memories.ForEach(Marshal.FreeHGlobal);
						memories.Clear();
					};

					doc.Print();
				}
			}
			catch (Exception ex)
			{
				// 例外ﾛｸﾞ出力
				MessageService.Exception(ex);
			}
		}

		/// <summary>
		/// ﾌﾟﾘﾝﾀ設定を取得します。
		/// </summary>
		/// <returns></returns>
		private PrinterSettings GetPrinterSettings()
		{
			// ﾌﾟﾘﾝﾀ名
			var printername = GetDefaultPrinterName();
			// ﾌﾟﾘﾝﾀ設定
			var settings = new PrinterSettings()
			{
				// ﾌﾟﾘﾝﾀ名
				PrinterName = printername,
				// 印刷の向き
				DefaultPageSettings =
				{
					Landscape = Landscape,
				},
			};

			if (_pdf = printername.ToLower().Contains("pdf"))
			{
				var filename = GetPdfSavePath();
				if (string.IsNullOrEmpty(filename)) return null;

				// PDF時は出力先指定
				settings.PrintToFile = true;
				settings.PrintFileName = filename;
			}

			return settings;
		}

		private bool _pdf = false;

		/// <summary>
		/// 出力先がPDFの場合の出力先ﾊﾟｽを取得します。
		/// </summary>
		/// <returns></returns>
		private string GetPdfSavePath()
		{
			var directory = ReportSetting.Instance.PdfSaveDirectory;

			var initialpath = Path.Combine(directory, GetPdfFilename());

			var targetpath = IsGUI
				? WpfUtil.ExecuteOnUI(() => WpfDialog.ShowSaveFile(initialpath, "PDFファイル|.pdf|全ファイル|.*"))
				: initialpath;

			if (IsGUI)
			{
				ReportSetting.Instance.PdfSaveDirectory = Directory.GetParent(targetpath).FullName;
				ReportSetting.Instance.Save();
			}
			return targetpath;
		}

		/// <summary>
		/// 出力ｲﾒｰｼﾞ作成用のﾃﾞｰﾀを初期化します。
		/// </summary>
		/// <returns></returns>
		protected abstract Task Initialize();

		protected abstract void OnPage(int index);

		/// <summary>
		/// 出力先がPDFの場合の出力ﾌｧｲﾙ名を取得します。
		/// </summary>
		/// <returns></returns>
		protected virtual string GetPdfFilename()
		{
			return DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".pdf";
		}

		/// <summary>
		/// ﾃﾞﾌｫﾙﾄﾌﾟﾘﾝﾀを取得します。
		/// </summary>
		/// <returns></returns>
		private string GetDefaultPrinterName()
		{
			using (var server = new LocalPrintServer())
			{
				var printername = ReportSetting.Instance.PrinterName;

				return server.GetPrintQueues().Any(x => x.FullName == printername)
					? printername
					: server.DefaultPrintQueue.FullName;
			}
		}

		/// <summary>
		/// 画面ﾃﾞｻﾞｲﾝを<see cref="RenderTargetBitmap"/>に変換します。
		/// </summary>
		/// <param name="fe">画面ﾃﾞｻﾞｲﾝ</param>
		/// <param name="size">ﾃﾞｻﾞｲﾝｻｲｽﾞ</param>
		/// <returns></returns>
		private RenderTargetBitmap ToRenderTargetBitmap(UserControl fe, Rect size)
		{
			return WpfUtil.ExecuteOnUI(() =>
			{
				if (_pdf) fe.Background = Brushes.White;

				fe.Measure(fe.RenderSize);
				fe.Arrange(new Rect(0, 0, fe.Width, fe.Height));
				fe.UpdateLayout();

				var target = new RenderTargetBitmap(
					(int)(fe.Width + size.Left * 2),
					(int)(fe.Height + size.Top * 2),
					96, 96, // DPI
					PixelFormats.Pbgra32
				);
				target.Render(fe);
				target.Freeze();

				return target;
			});
		}

		/// <summary>
		/// <see cref="RenderTargetBitmap"/>を画像ﾃﾞｰﾀ(<see cref="System.Drawing.Image"/>)に変換します。
		/// </summary>
		/// <param name="target">元ﾃﾞｰﾀ</param>
		/// <returns></returns>
		private System.Drawing.Image ToDrawingImage(RenderTargetBitmap target)
		{
			var width = target.PixelWidth;
			var height = target.PixelHeight;
			var stride = width * ((target.Format.BitsPerPixel + 7) / 8);
			var memoryBlockPointer = Marshal.AllocHGlobal(height * stride);
			target.CopyPixels(new Int32Rect(0, 0, width, height), memoryBlockPointer, height * stride, stride);
			var bitmap = new System.Drawing.Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, memoryBlockPointer);
			memories.Add(memoryBlockPointer);
			return bitmap;
		}

		private List<IntPtr> memories = new List<IntPtr>();

		/// <summary>
		/// 印刷用の画像ﾃﾞｰﾀを取得します。
		/// </summary>
		/// <param name="fe">印刷ﾃﾞｰﾀ</param>
		/// <param name="size">印刷ｻｲｽﾞ</param>
		/// <returns></returns>
		private System.Drawing.Image ToDrawingImage(UserControl fe, Rect size)
		{
			var target = ToRenderTargetBitmap(fe, size);
			var bitmap = ToDrawingImage(target);

			return bitmap;
		}

		protected virtual PaperSize GetPaperSize(PrintDocument document)
		{
			var papers = document.PrinterSettings
				.PaperSizes
				.OfType<PaperSize>()
				.ToArray();

			var paper1 = papers.FirstOrDefault(x => x.Kind == PaperKind.Legal);
			if (paper1 != null) return paper1;

			var paper2 = papers.FirstOrDefault();
			if (paper2 != null) return paper2;

			throw new NullReferenceException();
		}

		private UserControl GetWindow(Func<UserControl> getwindow, Rect size)
		{
			return WpfUtil.ExecuteOnUI(() =>
			{
				var window = getwindow();

				window.DataContext = this;

				// ｸﾞﾗﾌ表示用の領域を計算する。
				window.Width = size.Width;
				window.Height = size.Height;
				window.Measure(new Size(window.Width, window.Height));
				window.Arrange(new Rect(0, 0, window.Width, window.Height));
				window.UpdateLayout();

				return window;
			});
		}

		protected abstract UserControl GetWindow();
	}
}