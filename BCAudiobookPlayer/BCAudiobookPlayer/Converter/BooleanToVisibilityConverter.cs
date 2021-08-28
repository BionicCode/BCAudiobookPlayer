using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class BooleanToVisibilityConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value is bool isVisible)
      {
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
      }
      return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      if (value is Visibility visibility)
      {
        return visibility == Visibility.Visible;
      }

      return value;
    }

    #endregion
  }
}