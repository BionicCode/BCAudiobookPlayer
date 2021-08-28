using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.Player.Playback
{
  [DataContract]
  public class AudioMediaTag : IMediaTag<MusicProperties>
  {
    public static IMediaTag<MusicProperties> NullObject = new AudioMediaTag(true);
    protected AudioMediaTag(bool isNull = false)
    {
      this.IsAutoSaveTagChangesEnabled = false;
      this.IsNull = isNull;
      this.Rating = 0;
      this.IsInitializing = false;
      this.CoverArt = new BitmapImage();
      this.Duration = TimeSpan.Zero;
      this.TrackNumber = 0;
      this.Title = string.Empty;
      this.Album = string.Empty;
      this.AlbumArtist = string.Empty;
      this.Genre = string.Empty;
      this.BitRate = 0;
      this.Year = 0;
    }

    protected AudioMediaTag(IMediaTag<MusicProperties> copySource)
    {
      this.IsNull = copySource.IsNull;
      this.IsInitializing = false;
      this.CoverArt = copySource.CoverArt;
      this.Duration = copySource.Duration;
      this.Rating = copySource.Rating;
      this.TrackNumber = copySource.TrackNumber;
      this.Title = copySource.Title;
      this.Album = copySource.Album;
      this.AlbumArtist = copySource.AlbumArtist;
      this.Genre = copySource.Genre;
      this.BitRate = copySource.BitRate;
      this.Year = copySource.Year;
      this.IsAutoSaveTagChangesEnabled = copySource.IsAutoSaveTagChangesEnabled;
    }

    public static async Task<IMediaTag<MusicProperties>> Create(StorageFile storageFile, string alternateTitle = null)
    {
      var instance = new AudioMediaTag
      {
        IsInitializing = true,
        IsAutoSaveTagChangesEnabled = true
      };

      MusicProperties musicProperties = await storageFile.Properties.GetMusicPropertiesAsync();

      instance.Title = string.IsNullOrWhiteSpace(musicProperties.Title) 
        ? alternateTitle ?? storageFile.DisplayName 
        : musicProperties.Title;
      instance.Rating = musicProperties.Rating;
      instance.Duration = musicProperties.Duration;
      instance.AlbumArtist = string.IsNullOrWhiteSpace(musicProperties.AlbumArtist)  
        ? string.Empty 
        : musicProperties.AlbumArtist;
      instance.Album = musicProperties.Album;
      instance.BitRate = musicProperties.Bitrate;
      instance.Year = musicProperties.Year;
      instance.TrackNumber = musicProperties.TrackNumber;
      instance.Genre = string.IsNullOrWhiteSpace(musicProperties.Genre.FirstOrDefault()) 
        ? string.Empty 
        : musicProperties.Genre.FirstOrDefault();

      await instance.CoverArt.SetSourceAsync(await storageFile.GetThumbnailAsync(
        ThumbnailMode.MusicView,
        256,
        ThumbnailOptions.UseCurrentScale));

      instance.IsInitializing = false;
      return instance;
    }

    public IMediaTag<MusicProperties> Clone() => new AudioMediaTag(this);

    private async void SaveTagToFile()
    {
      if (this.IsInitializing || !this.IsAutoSaveTagChangesEnabled)
      {
        return;
      }

      StorageItemContentProperties fileProperties =
        (await StorageApplicationPermissions.FutureAccessList.GetFileAsync(this.MediaFileSystemToken))?.Properties;
      if (fileProperties == null)
      {
        return;
      }

      MusicProperties musicProperties = await fileProperties.GetMusicPropertiesAsync();
      musicProperties.Album = this.Album;
      musicProperties.Rating = this.Rating;
      musicProperties.AlbumArtist = this.AlbumArtist;
      musicProperties.Title = this.Title;
      musicProperties.TrackNumber = this.TrackNumber;
      musicProperties.Year = this.Year;
      musicProperties.Genre.Add(this.Genre);
      await musicProperties.SavePropertiesAsync();
    }

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool IsInitializing { get; set; }
    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsNull { get; }

    [DataMember]
    private string title;
    public string Title
    {
      get => this.title;
      set
      {
        if (Equals(this.Title, value))
        {
          return;
        }
        this.title = value;
        OnPropertyChanged();
        if (this.IsAutoSaveTagChangesEnabled)
        {
          SaveTagToFile();
        }
      }
    }

    [DataMember]
    private string albumArtist;
    public string AlbumArtist
    {
      get => this.albumArtist;
      set
      {
        if (Equals(this.AlbumArtist, value))
        {
          return;
        }
        this.albumArtist =  value;
        OnPropertyChanged();
        if (this.IsAutoSaveTagChangesEnabled)
        {
          SaveTagToFile();
        }
      }
    }

    [DataMember]
    private string album;   
    public string Album
    {
      get => this.album;
      set
      {
        if (Equals(this.Album, value))
        {
          return;
        }
        this.album = value; 
        OnPropertyChanged();
        if (this.IsAutoSaveTagChangesEnabled)
        {
          SaveTagToFile();
        }
      }
    }

    [DataMember]
    private uint year;
    public uint Year
    {
      get => this.year;
      set
      {
        if (Equals(this.Year, value))
        {
          return;
        }
        this.year = value;
        OnPropertyChanged();
        if (this.IsAutoSaveTagChangesEnabled)
        {
          SaveTagToFile();
        }
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
        if (this.IsAutoSaveTagChangesEnabled)
        {
          SaveTagToFile();
        }
      }
    }

    [DataMember]
    private string genre;
    public string Genre
    {
      get => this.genre;
      set
      {
        if (Equals(this.Genre, value))
        {
          return;
        }
        this.genre = value;
        OnPropertyChanged();
        if (this.IsAutoSaveTagChangesEnabled)
        {
          SaveTagToFile();
        }
      }
    }

    [DataMember]
    private uint bitRate;
    public uint BitRate
    {
      get => this.bitRate;
      private set
      {
        this.bitRate = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private TimeSpan duration;   
    public TimeSpan Duration
    {
      get => this.duration;
      set 
      { 
        this.duration = value; 
        OnPropertyChanged();
      }
    }

    private BitmapImage coverArt;   
    public BitmapImage CoverArt
    {
      get => this.coverArt;
      set 
      { 
        this.coverArt = value; 
        OnPropertyChanged();
      }
    }

    [DataMember]
    private uint trackNumber;   
    public uint TrackNumber
    {
      get => this.trackNumber;
      set 
      { 
        if (Equals(this.TrackNumber, value))
        {
          return;
        }
        this.trackNumber = value; 
        OnPropertyChanged();
        if (this.IsAutoSaveTagChangesEnabled)
        {
          SaveTagToFile();
        }
      }
    }

    [DataMember]
    private bool isAutoSaveTagChangesEnabled;   
    public bool IsAutoSaveTagChangesEnabled
    {
      get => this.isAutoSaveTagChangesEnabled;
      set 
      { 
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
  }
}