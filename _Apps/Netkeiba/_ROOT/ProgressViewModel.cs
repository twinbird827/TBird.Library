using TBird.Wpf;

namespace Netkeiba
{
	public class ProgressViewModel : BindableBase
	{
		public ProgressViewModel()
		{
			AddOnPropertyChanged(this, (sender, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(Value):
					case nameof(Maximum):
						OnPropertyChanged(nameof(Ratio));
						return;
				}
			});
		}

		public double Value
		{
			get => _Value;
			set => SetProperty(ref _Value, value);
		}
		private double _Value;

		public double Minimum
		{
			get => _Minimum;
			set => SetProperty(ref _Minimum, value);
		}
		private double _Minimum;

		public double Maximum
		{
			get => _Maximum;
			set => SetProperty(ref _Maximum, value);
		}
		private double _Maximum;

		public double Ratio => 0 < Maximum ? Value / Maximum : 0;
	}
}