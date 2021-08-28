using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class TextBlockTextToVisibilityConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language) => value is string stringValue && !string.IsNullOrWhiteSpace(stringValue) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();

    #endregion
  }
}