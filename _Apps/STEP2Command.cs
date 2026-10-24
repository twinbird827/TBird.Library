using System.Threading.Tasks;

namespace Netkeiba
{
	public class STEP2Command : STEPBase
	{
		public STEP2Command(MainViewModel vm) : base(vm)
		{

		}

		protected override Task ActionAsync(object dummy) => STEP2Generator.GenerateAsync(VM.S2Overwrite.IsChecked);
	}
}
