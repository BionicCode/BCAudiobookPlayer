using System;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class TrackNumberToStringConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      return $"#{value}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      return (value as string)?.Substring(1);
    }

    #endregion
  }
}