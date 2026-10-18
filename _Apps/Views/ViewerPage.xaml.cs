using ComicViewer.Services;
using ComicViewer.ViewModels;
using TBird.Core;

namespace ComicViewer.Views;

public partial class ViewerPage : ContentPage, ISlideHost
{
    private const double CommitRatio = 0.2;
    private const uint SlideMs = 200;
    private const uint CancelMs = 150;

    private readonly SystemBarsService _systemBars;
    private bool _panning;
    private double _panX;

    public ViewerPage(ViewerViewModel viewModel, SystemBarsService systemBars)
    {
        ViewModel = viewModel;
        _systemBars = systemBars;
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.SlideHost = this;
        viewModel.OverlayChanged += OnOverlayChanged;
        Root.SizeChanged += (_, _) => ResetSlide();
    }

    public ViewerViewModel ViewModel { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _systemBars.Hide();
        DeviceDisplay.Current.KeepScreenOn = true;
        DeviceDisplay.Current.MainDisplayInfoChanged += OnDisplayChanged;
    }

    protected override void OnDisappearing()
    {
        DeviceDisplay.Current.MainDisplayInfoChanged -= OnDisplayChanged;
        DeviceDisplay.Current.KeepScreenOn = false;
        _systemBars.Show();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        Run(ViewModel.BackAsync);
        return true;
    }

    private void OnDisplayChanged(object? sender, DisplayInfoChangedEventArgs e) => ViewModel.OnDisplayChanged();

    private void OnOverlayChanged(bool visible)
    {
        if (visible)
        {
            _systemBars.Show();
            // 3 ボタンナビゲーションに下部パネルが隠れないよう余白を取る。
            BottomPanel.Padding = new Thickness(12, 8, 12, 8 + _systemBars.GetNavigationBarHeight());
        }
        else
        {
            _systemBars.Hide();
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (e.GetPosition(Root) is not { } point) return;
        Run(() => ViewModel.OnTapAsync(point, new Size(Root.Width, Root.Height)));
    }

    private void OnSliderDragCompleted(object? sender, EventArgs e) => ViewModel.SeekCommand.Execute(null);

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panning = ViewModel.CanSwipe;
                _panX = 0;
                break;
            case GestureStatus.Running when _panning:
                _panX = e.TotalX;
                SetOffset(_panX);
                break;
            case GestureStatus.Completed when _panning:
            case GestureStatus.Canceled when _panning:
                _panning = false;
                if (Math.Abs(_panX) < Root.Width * CommitRatio) Run(CancelSlideAsync);
                // 右開き: 右へスワイプ（TotalX > 0）で次の単位。
                else Run(() => ViewModel.CommitSwipeAsync(_panX > 0 ? +1 : -1));
                break;
        }
    }

    private void SetOffset(double x)
    {
        CurrentView.TranslationX = x;
        NextView.TranslationX = x - Root.Width;
        PrevView.TranslationX = x + Root.Width;
    }

    public Task SlideAsync(int dir) => AnimateToAsync(dir * Root.Width, SlideMs);

    public Task CancelSlideAsync() => AnimateToAsync(0, CancelMs);

    public void ResetSlide() => SetOffset(0);

    private Task AnimateToAsync(double x, uint length) => Task.WhenAll(
        CurrentView.TranslateToAsync(x, 0, length),
        NextView.TranslateToAsync(x - Root.Width, 0, length),
        PrevView.TranslateToAsync(x + Root.Width, 0, length));

    private static async void Run(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }
}
