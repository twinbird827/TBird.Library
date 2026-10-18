using Android.Content;
using Android.Provider;
using TBird.Core;

namespace ComicViewer.Services;

public class StoragePermissionService
{
    private readonly ToastService _toast;

    public StoragePermissionService(ToastService toast) => _toast = toast;

    /// <summary>説明ダイアログはアプリ起動ごとに 1 回だけ出す。</summary>
    public bool HasAsked { get; set; }

    public bool IsGranted => Android.OS.Environment.IsExternalStorageManager;

    public void OpenSettings()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null) return;
        try
        {
            activity.StartActivity(new Intent(Settings.ActionManageAppAllFilesAccessPermission,
                Android.Net.Uri.Parse($"package:{AppInfo.PackageName}")));
        }
        catch (ActivityNotFoundException)
        {
            try
            {
                activity.StartActivity(new Intent(Settings.ActionManageAllFilesAccessPermission));
            }
            catch (ActivityNotFoundException ex)
            {
                MessageService.Exception(ex);
                _toast.Show("設定画面を開けませんでした");
            }
        }
    }
}
