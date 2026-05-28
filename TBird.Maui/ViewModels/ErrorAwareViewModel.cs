using CommunityToolkit.Mvvm.ComponentModel;

namespace TBird.Maui.ViewModels;

/// <summary>
/// HasError / ErrorMessage を保持するエラー状態付き ViewModel 基底。
/// View 側で HasError をトリガーにエラー UI（バナー等）を表示する想定。
/// 確認ダイアログのような双方向対話用途には設計されていない（エラー通知の片方向状態のみ）。
/// </summary>
public abstract partial class ErrorAwareViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    protected virtual void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    protected virtual void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
