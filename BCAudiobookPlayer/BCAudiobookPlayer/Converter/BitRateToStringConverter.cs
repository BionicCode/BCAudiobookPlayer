using System;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class BitRateToStringConverter : IValueConverter
  {
    private const int UnitRatio = 1000;
    private const string Unit = "kbps";
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      return $"{((uint) value) / BitRateToStringConverter.UnitRatio} {BitRateToStringConverter.Unit}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      return uint.TryParse((value as string)?.Split(BitRateToStringConverter.Unit)[0], out uint bitRate) ? bitRate * BitRateToStringConverter.UnitRatio : 0;
    }

    #endregion
  }
}