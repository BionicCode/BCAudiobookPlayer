using System;
using Windows.UI.Xaml.Data;
using BCAudiobookPlayer.Player.Playback.Contract;

namespace BCAudiobookPlayer.Converter
{
    public class AudiobookToAudiobookPartConverter : IValueConverter
    {
      #region Implementation of IValueConverter

      public object Convert(object value, Type targetType, object parameter, string language)
      {
        var audioPart = value as IAudiobookPart;
        if (audioPart is IAudiobook audiobook)
        {
          audioPart = audiobook.CurrentPart;
        }

        if (!(parameter is string propertyName))
        {
          return audioPart;
        }

        switch (propertyName)
        {
          case "Title":
            return audioPart?.Tag?.Title ?? string.Empty;
          case "CoverArt":
            return audioPart?.Tag?.CoverArt;
          case "AlbumArtist":
            return string.IsNullOrWhiteSpace(audioPart?.Tag?.AlbumArtist) ? string.Empty : $"by {audioPart?.Tag?.AlbumArtist}";
          case "Album":
            return audioPart?.Tag?.Album ?? string.Empty;

          default:
            return audioPart;
        }
      }

      public object ConvertBack(object value, Type targetType, object parameter, string language)
      {
        throw new NotImplementedException();
      }

      #endregion
    }
}