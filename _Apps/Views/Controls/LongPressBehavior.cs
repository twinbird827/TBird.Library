using System.Windows.Input;

namespace ComicViewer.Views.Controls;

/// <summary>
/// 行のタップと長押し。MAUI 標準に長押しが無く、TapGestureRecognizer と Android の LongClick を
/// 同じビューに付けるとタッチリスナーを奪い合うため、両方を Android の Click / LongClick で受ける。
/// </summary>
public class LongPressBehavior : Behavior<View>
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LongPressBehavior));

    public static readonly BindableProperty TapCommandProperty =
        BindableProperty.Create(nameof(TapCommand), typeof(ICommand), typeof(LongPressBehavior));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(LongPressBehavior));

    private View? _view;
    private global::Android.Views.View? _platformView;

    /// <summary>長押し</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _view = bindable;
        // DataTemplate 内では Behavior に BindingContext が伝わらないため、親のものを使う。
        BindingContext = bindable.BindingContext;
        bindable.BindingContextChanged += OnBindingContextChanged;
        bindable.HandlerChanged += OnHandlerChanged;
        Attach();
    }

    protected override void OnDetachingFrom(View bindable)
    {
        bindable.BindingContextChanged -= OnBindingContextChanged;
        bindable.HandlerChanged -= OnHandlerChanged;
        Detach();
        _view = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnBindingContextChanged(object? sender, EventArgs e) => BindingContext = _view?.BindingContext;

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        Detach();
        Attach();
    }

    private void Attach()
    {
        if (_view?.Handler?.PlatformView is not global::Android.Views.View platformView) return;
        _platformView = platformView;
        platformView.Clickable = true;
        platformView.LongClickable = true;
        platformView.Click += OnClick;
        platformView.LongClick += OnLongClick;
    }

    private void Detach()
    {
        if (_platformView is null) return;
        _platformView.Click -= OnClick;
        _platformView.LongClick -= OnLongClick;
        _platformView = null;
    }

    private void OnClick(object? sender, EventArgs e) => Execute(TapCommand);

    private void OnLongClick(object? sender, global::Android.Views.View.LongClickEventArgs e)
    {
        e.Handled = true;
        Execute(Command);
    }

    private void Execute(ICommand? command)
    {
        var parameter = CommandParameter ?? _view?.BindingContext;
        if (command?.CanExecute(parameter) == true) command.Execute(parameter);
    }
}
