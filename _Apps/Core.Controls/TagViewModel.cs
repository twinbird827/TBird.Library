using System.Windows.Input;
using TBird.Wpf;

namespace Moviewer.Core.Controls
{
	public class TagViewModel : ControlViewModel
	{
		public TagViewModel(string tag) : base(null)
		{
			Tag = tag;
		}

		public string Tag
		{
			get => _Tag;
			set => SetProperty(ref _Tag, value);
		}
		private string _Tag;

		public ICommand OnClickTag =>
			_OnClickTag = _OnClickTag ?? CreateOnClickTag();
		private ICommand _OnClickTag;

		protected virtual ICommand CreateOnClickTag()
		{
			return RelayCommand.Create(_ => { });
		}
	}
}