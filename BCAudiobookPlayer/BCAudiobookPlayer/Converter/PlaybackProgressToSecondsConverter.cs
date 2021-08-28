using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class PlaybackProgressToSecondsConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      return value is TimeSpan timeSpan ? timeSpan.TotalSeconds : 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      return DependencyProperty.UnsetValue;
    }

    #endregion
  }
}
