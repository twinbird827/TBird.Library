using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Wpf;
using TBird.Core;

namespace Netkeiba
{
	public class STEP4UpdateListCommand : STEPBase
	{
		public STEP4UpdateListCommand(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			var dates = await Enumerable.Range(-1, 2)
				.Select(i => DateTime.Now.AddMonths(i))
				.Select(x => NetkeibaGetter.GetKaisaiDate(x.Year, x.Month))
				.WhenAll()
				.RunAsync(x => x.SelectMany(y => y));

			WpfUtil.ExecuteOnUI(() =>
			{
				VM.S4Dates.Items.Clear();
				VM.S4Dates.Items.AddRange(dates.Select(x => new ComboboxItemModel(x, x)));
			});
		}
	}
}