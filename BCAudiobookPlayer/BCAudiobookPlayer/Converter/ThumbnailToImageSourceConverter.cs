using System;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace BCAudiobookPlayer.Converter
{
  public class ThumbnailToImageSourceConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value is StorageItemThumbnail thumbnail)
      {
        var imageSource = new BitmapImage();
        imageSource.SetSource(thumbnail);
        return imageSource;
      }

      return value?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}