using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Netkeiba
{
	public class UniformViewModel<T> : BindableBase
	{
		public UniformViewModel(IEnumerable<T> columns)
		{
			ColumnsSource = new BindableCollection<T>(columns);
			Columns = ColumnsSource.ToBindableContextCollection();

			AddCollectionChanged(ColumnsSource, (sender, e) =>
			{
				OnPropertyChanged(nameof(ColumnCount));
			});
		}

		public int ColumnCount => ColumnsSource.Count;

		public BindableCollection<T> ColumnsSource { get; } = new BindableCollection<T>();

		public BindableContextCollection<T> Columns { get; }
	}

	public class UniformViewModel : UniformViewModel<ComboboxItemModel>
	{
		public UniformViewModel(IEnumerable<ComboboxItemModel> columns) : base(columns)
		{

		}
	}
}