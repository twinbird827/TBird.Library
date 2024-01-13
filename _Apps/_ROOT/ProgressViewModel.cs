using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Wpf;

namespace Netkeiba._ROOT
{
	public class ProgressViewModel : BindableBase
	{
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

	}
}
