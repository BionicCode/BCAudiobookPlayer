using System;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using BCAudiobookPlayer.Player.Playback.Contract;

namespace BCAudiobookPlayer.Converter
{
  public class PageAppBarClipRectangleConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      var audioPart = value as IAudiobookPart;
      if (value is RectangleGeometry clipRectangle)
      {
        Rect clipRectangleRect = clipRectangle.Rect;
        clipRectangleRect.Height = 72;
        clipRectangle.Rect = clipRectangleRect;
        return clipRectangle;
      }

      return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}