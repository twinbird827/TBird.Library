using Android.Widget;

namespace ComicViewer.Services;

public class ToastService
{
    public void Show(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => Toast.MakeText(Platform.AppContext, message, ToastLength.Short)?.Show());
    }
}
