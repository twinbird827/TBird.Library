namespace Moviewer.Core.Controls
{
	public class CounterModel : ControlModel
	{
		public CounterModel(CounterType type, long count)
		{
			Type = type;
			Count = count;
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