namespace Moviewer.Core.Controls
{
	public class CounterViewModel : ControlViewModel
	{
		public CounterViewModel(CounterModel m) : base(m)
		{
			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Type = m.Type;
			}, nameof(m.Type), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Count = m.Count;
			}, nameof(m.Count), true);
		}

		public CounterType Type
		{
			get => _Type;
			set => SetProperty(ref _Type, value);
		}
		private CounterType _Type;

		public long Count
		{
			get => _Count;
			set => SetProperty(ref _Count, value);
		}
		private long _Count;

	}
}