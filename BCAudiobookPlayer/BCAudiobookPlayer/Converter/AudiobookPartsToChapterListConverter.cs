using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Data;
using BCAudiobookPlayer.Player.Playback.Contract;

namespace BCAudiobookPlayer.Converter
{
  public class AudiobookPartsToChapterListConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      return value is ObservableCollection<IAudiobookPart> audiobookParts && audiobookParts.Count > 1
        ? audiobookParts
        : new ObservableCollection<IAudiobookPart>();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}