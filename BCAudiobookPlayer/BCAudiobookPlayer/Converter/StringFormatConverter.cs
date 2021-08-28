using System;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class StringFormatConverter : IValueConverter
  {
    public StringFormatConverter()
    {
      this.FormatOverride = null;
    }

    protected virtual object CoerceValue(object value) => value;
    protected string FormatOverride { get; set; }

    
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value != null)
      {
        var formattedString = parameter as string ?? "{0}";
        object coercedValue = CoerceValue(value);
        return string.Format(this.FormatOverride ?? formattedString, coercedValue);
      }

      return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}