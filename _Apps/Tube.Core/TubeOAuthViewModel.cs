using Codeplex.Data;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Controls;

namespace Moviewer.Tube.Core
{
	public class TubeOAuthViewModel : DialogViewModel
	{
		public TubeOAuthViewModel()
		{
			ClientId = TubeSetting.Instance.ClientId;
			ClientSecret = TubeSetting.Instance.ClientSecret;

			AddDisposed((sender, e) =>
			{
				if (DialogResult.Value)
				{
					TubeSetting.Instance.ClientId = ClientId;
					TubeSetting.Instance.ClientSecret = ClientSecret;
					TubeSetting.Instance.Save();
				}
			});
		}

		public string ClientId
		{
			get => _ClientId;
			set => SetProperty(ref _ClientId, value);
		}
		private string _ClientId = null;

		public string ClientSecret
		{
			get => _ClientSecret;
			set => SetProperty(ref _ClientSecret, value);
		}
		private string _ClientSecret = null;

		public ICommand OnDrop => _OnDrop = _OnDrop ?? RelayCommand.Create<DragEventArgs>(e =>
		{
			if (e.Data.GetData(DataFormats.FileDrop) is string[] filepaths && filepaths.Length == 1)
			{
				var filepath = filepaths[0];
				dynamic json = DynamicJson.Parse(File.ReadAllText(filepath));
				ClientId = DynamicUtil.S(json, "installed.client_id");
				ClientSecret = DynamicUtil.S(json, "installed.client_secret");
			}
		});
		private ICommand _OnDrop;

		protected override ICommand GetOKCommand()
		{
			return RelayCommand.Create(
				_ => DialogResult = true,
				_ => !string.IsNullOrEmpty(CoreUtil.Nvl(ClientId, ClientSecret))
			).AddCanExecuteChanged(
				this, nameof(ClientId), nameof(ClientSecret)
			);
		}
	}
}