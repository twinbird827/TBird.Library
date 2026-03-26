using Moviewer.Nico.Controls;

namespace Moviewer.Nico.Core
{
	public interface INicoSearchHistoryParentViewModel
	{
		void NicoSearchHistoryOnDelete(NicoSearchHistoryViewModel vm);

		void NicoSearchHistoryOnDoubleClick(NicoSearchHistoryViewModel vm);
	}
}