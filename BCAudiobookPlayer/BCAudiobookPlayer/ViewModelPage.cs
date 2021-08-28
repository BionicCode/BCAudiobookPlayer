using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BCAudiobookPlayer.ViewModel;

namespace BCAudiobookPlayer
{
  public abstract class ViewModelPage<TViewModel> : Page where TViewModel : IViewModel
  {
    public abstract TViewModel ViewModel { get; set; }

    //public static readonly DependencyProperty DataContextProperty = DependencyProperty.Register(
    //  "DataContext",
    //  typeof(object),
    //  typeof(ViewModelPage),
    //  new PropertyMetadata(default(object)));

    //public new object DataContext { get { return (object) GetValue(ViewModelPage.DataContextProperty); } set { SetValue(ViewModelPage.DataContextProperty, value); } }

    //public ViewModelPage()
    //{
    //  this.DataContextChanged += (s, e) => this.DataContext = base.DataContext;
    //}
  }
}