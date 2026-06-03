using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class ExcludeKeywordsPage : ContentPage
{
    public ExcludeKeywordsPage(ExcludeKeywordsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
