using System;
using System.ComponentModel;
using System.Drawing;
using System.Dynamic;
using System.Runtime.Serialization;
using System.Text;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer.Player.Playback
{
  [DataContract]
  public class Bookmark : IBookmark
  {
    public static Bookmark NullObject => new Bookmark(true);

    public Bookmark(IAudiobookPart audiobookPart) : this()
    {
      int audioPartIndex = -1;
      this.RelativePosition = audiobookPart.CurrentPosition;
      if (audiobookPart is IAudiobook audiobook)
      {
        audioPartIndex = audiobook.CurrentPartIndex;
        this.RelativePosition = audiobook.CurrentPart.CurrentPosition;
      }

      this.Position = audiobookPart.CurrentPosition;
      this.AudioPartIndex = audioPartIndex;

      this.Title = audiobookPart.Tag.Title;
      this.CoverArt = audiobookPart.Tag.CoverArt;
      this.AlbumArtist = audiobookPart.Tag.AlbumArtist;
      this.Album = audiobookPart.Tag.Album;
      this.TrackNumber = audiobookPart.Tag.TrackNumber;
    }

    private Bookmark()
    {
      this.Position = TimeSpan.Zero;
      this.Title = string.Empty;
      this.IsNull = false;
      this.AudioPartIndex = -1;
    }

    private Bookmark(bool isNull) : this()
    {
      this.IsNull = isNull;
    }

    public void MapToAudiobookPart(int partIndex) => this.AudioPartIndex = partIndex;

    /// <inheritdoc />
    public BitmapImage CoverArt { get; set; }


    [DataMember]
    public TimeSpan Position { get; set; }

    [DataMember]
    public TimeSpan RelativePosition { get; set; }

    [DataMember]
    public string Title { get; set; }

    /// <inheritdoc />
    [DataMember]
    public string Album { get; set; }

    /// <inheritdoc />
    [DataMember]
    public string AlbumArtist { get; set; }

    /// <inheritdoc />
    [DataMember]
    public uint TrackNumber { get; set; }

    /// <inheritdoc />
    [DataMember]
    public TimeSpan Duration { get; set; }

    [DataMember]
    public bool IsNull { get; private set; }

    [DataMember]
    public int AudioPartIndex { get; private set; }
  }
}
