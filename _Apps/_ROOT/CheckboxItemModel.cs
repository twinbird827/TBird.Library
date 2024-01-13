using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Wpf;

namespace Netkeiba
{
	public class CheckboxItemModel : ComboboxItemModel
	{
		public CheckboxItemModel(string value, string display) : base(value, display)
		{

		}

		public bool IsChecked
		{
			get => _IsChecked;
			set => SetProperty(ref _IsChecked, value);
		}
		private bool _IsChecked;
	}
}