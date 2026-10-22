using System;
using System.IO;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public class STEP1Command : STEPBase
	{
		public STEP1Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			var create = VM.S1Overwrite.IsChecked || !File.Exists(AppUtil.Sqlitepath);

			if (create && !MessageService.Confirm("netkeibaから全データを取得し直します。よろしいでしょうか。"))
			{
				return;
			}

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = 1;

			await STEP1Updater.UpdateAsync(create, VM.SYear, new Progress<double>(x => Progress.Value = x));
		}
	}
}
