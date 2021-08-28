using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel.Core;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using BCAudiobookPlayer.Player.Helper;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.Player.Playback
{
  [DataContract]
  public sealed class AudioFile : AudioPart, IAudioFile
  {
    public new static IAudioFile NullObject => new AudioFile(true);
    internal AudioFile(StorageFile fileInfo) : base(fileInfo)
    {
    }

    private AudioFile(bool isNull) : base(isNull)
    {
    }


    internal AudioFile(StorageFile fileInfo, IEnumerable<IBookmark> bookmarks) : this(fileInfo)
    {
      this.bookmarks = new ObservableCollection<IBookmark>(bookmarks ?? new List<IBookmark>());
    }

    /// <summary>
    /// Factory method that helps to create a valid instance where the file exists and the file type is supported.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns>Instance</returns>
    internal static async Task<(bool IsSuccessFull, IAudioFile AudioFileInstance)> TryCreateAsync(StorageFile fileInfo)
    {
      if (!SupportedFileTypeValidator.IsValid(fileInfo.FileType))
      {
        return (false, AudioFile.NullObject);
      }

      IAudioFile audioFileInstance = new AudioFile(fileInfo);
      audioFileInstance.SetToIsCreating();
      audioFileInstance.Tag = await AudioMediaTag.Create(fileInfo, string.Empty);
      audioFileInstance.Duration = audioFileInstance.Tag.Duration;
      audioFileInstance.TimeRemaining = audioFileInstance.Duration;
      audioFileInstance.SetToIsCreated();
      return (!audioFileInstance.IsNull, audioFileInstance);
    }
  }
}