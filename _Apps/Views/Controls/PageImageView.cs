namespace ComicViewer.Views.Controls;

/// <summary>Android.Graphics.Bitmap を再エンコードせずにそのまま表示する（FitCenter、背景黒）。</summary>
public class PageImageView : View
{
    // Platforms 側の型を共有コードの BindableProperty に出さないため object で持ち、ハンドラでキャストする。
    public static readonly BindableProperty BitmapProperty =
        BindableProperty.Create(nameof(Bitmap), typeof(object), typeof(PageImageView));

    public object? Bitmap
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }
}
