using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace BCAudiobookPlayer.Player.Playback.Contract.Generic
{
  public interface IMediaTag<out TStorageItemExtraProperties> : INotifyPropertyChanged where TStorageItemExtraProperties : IStorageItemExtraProperties
  {
    IMediaTag<TStorageItemExtraProperties> Clone();
    //StorageItemContentProperties FileProperties { get; set; }
    string AlbumArtist { get; set; }
    string Album { get; set; }
    uint BitRate { get; }
    TimeSpan Duration { get; set; }
    string Genre { get; set; }
    string Title { get; set; }
    uint Year { get; set; }
    uint Rating { get; set; }
    uint TrackNumber { get; set; }
    BitmapImage CoverArt { get; set; }
    bool IsNull { get; }
    bool IsAutoSaveTagChangesEnabled { get; set; }
    string MediaFileSystemToken { get; set; }
  }
}