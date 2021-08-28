using System;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class PlaybacksProgressToDurationConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      return value is TimeSpan timeSpan 
        ? timeSpan.ToString(@"hh\:mm\:ss")
        : value is double doubleValue ? TimeSpan.FromSeconds(doubleValue).ToString(@"hh\:mm\:ss") : "00:00:00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}