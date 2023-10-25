using TBird.Core;
using TBird.Wpf;

namespace Moviewer.Core.Windows
{
	public class DownloadViewModel : DownloadModel
	{
		private DownloadViewModel(DownloadModel m)
		{
			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Title = m.Title;
			}, nameof(Title), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Maximum = m.Maximum;
			}, nameof(Maximum), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Minimum = m.Minimum;
			}, nameof(Minimum), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Value = m.Value;
			}, nameof(Value), true);

			MainViewModel.Instance.DownloadSources.Add(this);

			AddDisposed((sender, e) =>
			{
				MainViewModel.Instance.DownloadSources.Remove(this);
			});
		}

		public static async void Download(DownloadModel m)
		{
			using (new DownloadViewModel(m))
			{
				// 初期化
				await m.Initialize();

				// ﾌｧｲﾙ保存先を取得
				var filepath = m.GetDownloadPath();

				if (string.IsNullOrEmpty(filepath)) return;

				if (await m.Execute().TryCatch())
				{
					WpfToast.ShowMessage(
						AppConst.H_CompleteDownload,
						string.Format(AppConst.M_CompleteDownload, m.Title)
					);
				}
				else
				{
					WpfToast.ShowMessage(
						AppConst.H_FailedDownload,
						string.Format(AppConst.M_FailedDownload, m.Title)
					);
				}
			}
		}
	}
}