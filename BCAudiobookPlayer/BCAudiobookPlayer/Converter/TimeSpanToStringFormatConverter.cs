using System;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class TimeSpanToStringFormatConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (!(value is TimeSpan timeSpan))
      {
        return value;
      }
        var sign = parameter as string ?? string.Empty;

      string verboseTimeFormat = "h'h  'm'm  's's'";
      switch (sign)
      {
        case "verbose": return timeSpan.ToString(verboseTimeFormat);
        case "verbose-": return "-" + timeSpan.ToString(verboseTimeFormat);
        case "verbose+": return "+" + timeSpan.ToString(verboseTimeFormat);
        case "h": return timeSpan.Hours.ToString("D2");
        case "m": return timeSpan.Minutes.ToString("D2");
        case "s": return timeSpan.Seconds.ToString("D2");
        default: return sign + timeSpan.ToString(@"hh\:mm\:ss");
      }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      return TimeSpan.TryParse(value as string, out TimeSpan convertedString) ? convertedString : TimeSpan.Zero;
    }

    #endregion
  }
}