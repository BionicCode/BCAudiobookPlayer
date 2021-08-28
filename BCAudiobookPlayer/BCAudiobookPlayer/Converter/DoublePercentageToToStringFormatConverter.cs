using System;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class DoublePercentageToToStringFormatConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      return $"{(int) (100 * (double) value):D} %";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      return int.TryParse((value as string)?.Split('%')[0], out int intValue) ? intValue / 100d : 0d;
    }

    #endregion
  }
}