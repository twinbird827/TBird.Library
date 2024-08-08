using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Netkeiba
{
	public class TreeCheckboxViewModel : BindableBase
	{
		public TreeCheckboxViewModel(CheckboxItemModel value) : this(value, Enumerable.Empty<TreeCheckboxViewModel>())
		{

		}

		public TreeCheckboxViewModel(CheckboxItemModel value, IEnumerable<TreeCheckboxViewModel> children)
		{
			Children = ChildrenSource.ToBindableContextCollection();

			ChildrenSource.AddRange(children);

			_Value = value;
			_Value.AddOnPropertyChanged(this, (sender, e) =>
			{
				ChildrenSource.ForEach(x => x.Value.IsChecked = _Value.IsChecked);
			});
		}

		public CheckboxItemModel Value
		{
			get => _Value;
			set => SetProperty(ref _Value, value);
		}
		private CheckboxItemModel _Value;

		public BindableCollection<TreeCheckboxViewModel> ChildrenSource { get; } = new BindableCollection<TreeCheckboxViewModel>();

		public BindableContextCollection<TreeCheckboxViewModel> Children { get; }

	}
}