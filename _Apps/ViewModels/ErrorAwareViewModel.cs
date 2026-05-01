using CommunityToolkit.Mvvm.ComponentModel;

namespace LanobeReader.ViewModels;

/// <summary>
/// 全画面共通のエラー状態を持つ基底 ViewModel。
/// HasError=true のとき ErrorMessage を赤バナーで表示する規約。
/// 確認ダイアログ（削除確認等）には使わず、エラー通知のみに用いる。
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
