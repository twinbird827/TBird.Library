using System;
using System.Threading.Tasks;

namespace Netkeiba
{
	public class STEP1BikouCommand : STEPBase
	{
		public STEP1BikouCommand(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = 1;

			await STEP1Updater.BackfillBikouAsync(new Progress<double>(x => Progress.Value = x));
		}
	}
}
