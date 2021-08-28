using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.Player.Playback.Contract.Generic
{
  [DataContract]
  public class HttpMediaTag : IMediaTag<MusicProperties>
  {
    public static IMediaTag<MusicProperties> NullObject => new HttpMediaTag(true);

    public HttpMediaTag()
    {
      this.Title = string.Empty;
    }

    public HttpMediaTag(string title) : this()
    {
      this.Title = title;
    }

    public HttpMediaTag(IMediaTag<MusicProperties> copySource) : this()
    {
      this.Title = copySource.Title;
    }

    private HttpMediaTag(bool isNull) : this()
    {
      this.IsNull = isNull;
    }

    #region Implementation of INotifyPropertyChanged

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    public IMediaTag<MusicProperties> Clone() => new HttpMediaTag(this);

    /// <inheritdoc />
    public StorageItemContentProperties FileProperties { get; set; }

    #region Implementation of IMediaTag<out BasicProperties>

    private string albumArtist;
    /// <inheritdoc />
    public string AlbumArtist
    {
      get => this.albumArtist;
      set
      {
        if (value == this.albumArtist) return;
        this.albumArtist = value;
        OnPropertyChanged();
      }
    }

    private string album;
    /// <inheritdoc />
    public string Album
    {
      get => this.album;
      set
      {
        if (value == this.album) return;
        this.album = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private uint bitRate;
    /// <inheritdoc />
    public uint BitRate
    {
      get => this.bitRate;
      set
      {
        if (value == this.bitRate) return;
        this.bitRate = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private TimeSpan duration;
    /// <inheritdoc />
    public TimeSpan Duration
    {
      get => this.duration;
      set
      {
        if (value.Equals(this.duration)) return;
        this.duration = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private string genre;
    /// <inheritdoc />
    public string Genre
    {
      get => this.genre;
      set
      {
        if (value == this.genre) return;
        this.genre = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private string title;
    /// <inheritdoc />
    public string Title
    {
      get => this.title;
      set
      {
        if (value == this.title) return;
        this.title = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private uint year;
    /// <inheritdoc />
    public uint Year
    {
      get => this.year;
      set
      {
        if (value == this.year) return;
        this.year = value;
        OnPropertyChanged();
      }
    }

    private uint rating;
    /// <inheritdoc />
    public uint Rating
    {
      get => this.rating;
      set
      {
        if (value == this.rating) return;
        this.rating = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private uint trackNumber;
    /// <inheritdoc />
    public uint TrackNumber
    {
      get => this.trackNumber;
      set
      {
        if (value == this.trackNumber) return;
        this.trackNumber = value;
        OnPropertyChanged();
      }
    }

    private BitmapImage coverArt;
    /// <inheritdoc />
    public BitmapImage CoverArt
    {
      get => this.coverArt;
      set
      {
        if (object.Equals(value, this.coverArt)) return;
        this.coverArt = value;
        OnPropertyChanged();
      }
    }

    /// <inheritdoc />
    [DataMember]
    public bool IsNull { get; private set; }

    [DataMember]
    private bool isAutoSaveTagChangesEnabled;
    /// <inheritdoc />
    public bool IsAutoSaveTagChangesEnabled
    {
      get => this.isAutoSaveTagChangesEnabled;
      set
      {
        if (value == this.isAutoSaveTagChangesEnabled) return;
        this.isAutoSaveTagChangesEnabled = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private string mediaFileSystemToken;   
    public string MediaFileSystemToken
    {
      get => this.mediaFileSystemToken;
      set 
      { 
        this.mediaFileSystemToken = value; 
        OnPropertyChanged();
      }
    }

    #endregion

  }
}