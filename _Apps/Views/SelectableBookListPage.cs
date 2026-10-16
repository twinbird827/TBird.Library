using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

/// <summary>
/// 一括選択（F-015）対応の一覧ページ共通基底。
/// 選択モード中の戻るボタンは画面を離脱せず選択モードを優先解除する（4ページ共通挙動の集約）。
/// </summary>
public abstract class SelectableBookListPage : ContentPage
{
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is SelectableBookListViewModel { IsSelectionMode: true } vm)
        {
            vm.ExitSelectionModeCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
