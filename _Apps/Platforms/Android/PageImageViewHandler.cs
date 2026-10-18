using Android.Widget;
using ComicViewer.Views.Controls;
using Microsoft.Maui.Handlers;

namespace ComicViewer.Platforms.Android;

public class PageImageViewHandler : ViewHandler<PageImageView, ImageView>
{
    public static readonly IPropertyMapper<PageImageView, PageImageViewHandler> Mapper =
        new PropertyMapper<PageImageView, PageImageViewHandler>(ViewMapper)
        {
            [nameof(PageImageView.Bitmap)] = MapBitmap,
        };

    public PageImageViewHandler() : base(Mapper)
    {
    }

    protected override ImageView CreatePlatformView() => new(Context);

    protected override void ConnectHandler(ImageView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SetScaleType(ImageView.ScaleType.FitCenter!);
        platformView.SetBackgroundColor(global::Android.Graphics.Color.Black);
    }

    protected override void DisconnectHandler(ImageView platformView)
    {
        platformView.SetImageBitmap(null);
        base.DisconnectHandler(platformView);
    }

    // 呼び出し側は新しい Bitmap をここで差し替えた後に古い Bitmap を Recycle する。
    private static void MapBitmap(PageImageViewHandler handler, PageImageView view)
        => handler.PlatformView.SetImageBitmap(view.Bitmap as global::Android.Graphics.Bitmap);
}
