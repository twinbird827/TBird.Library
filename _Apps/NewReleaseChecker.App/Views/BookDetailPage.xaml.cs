using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class BookDetailPage : ContentPage
{
    public BookDetailPage(BookDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
