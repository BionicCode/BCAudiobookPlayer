using System;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer.Player.Playback
{
  public interface IBookmark 
  {
    int AudioPartIndex { get; }
    bool IsNull { get; }
    TimeSpan Position { get; set; }
    TimeSpan RelativePosition { get; set; }
    string Title { get; set; }
    string Album { get; set; }
    string AlbumArtist { get; set; }
    uint TrackNumber { get; set; }
    TimeSpan Duration { get; set; }
    BitmapImage CoverArt { get; set; }

    void MapToAudiobookPart(int partIndex);
  }
}