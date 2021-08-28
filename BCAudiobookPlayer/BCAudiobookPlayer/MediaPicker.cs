using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using BCAudiobookPlayer.Player.Playback;

namespace BCAudiobookPlayer
{
  public static class MediaPicker
  {
    public static async Task<StorageFile> PickFile()
    {
      var filePicker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
      filePicker.FileTypeFilter.Add(".mp3");
      filePicker.FileTypeFilter.Add(".wav");
      filePicker.FileTypeFilter.Add(".wma");
      filePicker.FileTypeFilter.Add(".m4a");
      //filePicker.FileTypeFilter.Add(".ogg");
      filePicker.ViewMode = PickerViewMode.Thumbnail;
      return await filePicker.PickSingleFileAsync();
    }

    public static async Task<StorageFolder> PickDirectory()
    {
      var folderPicker = new FolderPicker() { SuggestedStartLocation = PickerLocationId.ComputerFolder };
      folderPicker.FileTypeFilter.Add(".mp3");
      folderPicker.FileTypeFilter.Add(".wav");
      folderPicker.FileTypeFilter.Add(".wma");
      folderPicker.FileTypeFilter.Add(".m4a");
      //folderPicker.FileTypeFilter.Add(".ogg");
      folderPicker.ViewMode = PickerViewMode.Thumbnail;
      return await folderPicker.PickSingleFolderAsync();
    }
  }
}
