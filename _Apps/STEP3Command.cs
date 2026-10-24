using System.Threading.Tasks;

namespace Netkeiba
{
	internal class STEP3Command : STEPBase
	{
		public STEP3Command(MainViewModel vm) : base(vm)
		{

		}

		protected override Task ActionAsync(object dummy) => STEP3Trainer.TrainAsync();
	}
}
