using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Controls;
using BCAudiobookPlayer.Player.Helper;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer.Player.Playback
{
  [DataContract]
  public class Playlist : IPlaylist
  {
    public Playlist()
    {
      this.Files = new List<IAudiobookPart>();
      this.LastPlayedItemIndex = 0;
      this.IsLoopEnabled = false;
      this.IsLoopCurrentFileEnabled = false;
    }

    public bool TryAdd(IAudiobookPart audioFile)
    {
      // Initialize on first add
      if (this.LastPlayedItemIndex < 0)
      {
        this.LastPlayedItemIndex = 0;
      }

      if (this.Files.Contains(audioFile))
      {
        return false;
      }

      this.Files.Add(audioFile);
      OnPlaylistChanged(new List<IAudiobookPart> { audioFile }, NotifyCollectionChangedAction.Add, this.Files.Count - 1);
      return true;

    }

    public bool TryAddRange(IEnumerable<IAudiobookPart> audioFiles)
    {
      List<IAudiobookPart> files = audioFiles.ToList();
      if (!files.Any())
      {
        return false;
      }

      // Initialize on first add
      if (this.LastPlayedItemIndex < 0)
      {
        this.LastPlayedItemIndex = 0;
      }

      files = files.Where((file) => !this.Files.Contains(file)).ToList();
      if (files.Any())
      {
        int insertionIndex = this.Files.Count;
        this.Files.AddRange(files);
        OnPlaylistChanged(files, NotifyCollectionChangedAction.Add, insertionIndex);
        return true;
      }

      return false;
    }

    public bool TryInsert(IAudiobookPart audioFile, int index)
    {
      // Initialize on first insert
      if (this.LastPlayedItemIndex < 0)
      {
        this.LastPlayedItemIndex = 0;
      }

      if (index >= this.Files.Count)
      {
        return TryAdd(audioFile);
      }

      if (this.Files.Contains(audioFile))
      {
        return false;
      }

      int insertionIndex = Math.Max(0, index);
      this.Files.Insert(insertionIndex, audioFile);
      OnPlaylistChanged(new []{audioFile}, NotifyCollectionChangedAction.Add, insertionIndex);
      return true;
    }

    public bool TryInsertRange(IEnumerable<IAudiobookPart> audioFiles, int index)
    {
      List<IAudiobookPart> files = audioFiles.ToList();
      if (!files.Any())
      {
        return false;
      }

      if (index >= this.Files.Count)
      {
        return TryAddRange(files);
      }

      // Initialize on first insert
      if (this.LastPlayedItemIndex < 0)
      {
        this.LastPlayedItemIndex = 0;
      }

      files = files.Where((file) => !this.Files.Contains(file)).ToList();
      if (!files.Any())
      {
        return false;
      }

      int insertionIndex = Math.Max(0, Math.Min(this.Files.Count - 1, index));
      this.Files.InsertRange(insertionIndex, files);
      OnPlaylistChanged(files, NotifyCollectionChangedAction.Add, insertionIndex);
      return true;
    }

    public bool TryRemoveAt(int index, out IAudiobookPart removedFile)
    {
      if (TryGetItemAt(index, out removedFile))
      {
        this.Files.RemoveAt(index);
        OnPlaylistChanged(new[] {removedFile}, NotifyCollectionChangedAction.Remove, index);
        return true;
      }

      return false;
    }

    public bool TryRemove(IAudiobookPart audioFile, out int index)
    {
      index = -1;
      if (!Contains(audioFile))
      {
        return false;
      }

      index = IndexOf(audioFile);
      if (TryRemoveAt(index, out IAudiobookPart removedAudioFile))
      {
        OnPlaylistChanged(new []{removedAudioFile}, NotifyCollectionChangedAction.Remove, index);
        return true;
      }
      return false;
    }

    public bool TryGetLastPlayedItem(out IAudiobookPart audioFile)
    {
      audioFile = AudioFile.NullObject;
      if (this.Files.Any())
      {
        audioFile = this.Files[this.LastPlayedItemIndex];
      }

      return !audioFile.IsNull;
    }

    public bool TryGetNextItem(out IAudiobookPart audioFile)
    {
      audioFile = AudioFile.NullObject;
      if (!this.Files.Any())
      {
        return false;
      }

      bool indexIsValid = this.LastPlayedItemIndex + 1 < this.Files.Count;
      if (this.IsLoopEnabled)
      {
        audioFile = indexIsValid ? this.Files[++this.LastPlayedItemIndex] : this.Files.First();
        return true;
      }

      if (indexIsValid)
      {
        audioFile = this.Files[++this.LastPlayedItemIndex];
      }

      return !audioFile.IsNull;
    }

    public bool TryGetPreviousItem(out IAudiobookPart audioFile)
    {
      audioFile = AudioFile.NullObject;
      if (!this.Files.Any())
      {
        return false;
      }

      bool indexIsValid = this.LastPlayedItemIndex - 1 > -1;
      if (this.IsLoopEnabled)
      {
        audioFile = indexIsValid ? this.Files[--this.LastPlayedItemIndex] : this.Files.Last();
        return true;
      }

      if (indexIsValid)
      {
        audioFile = this.Files[--this.LastPlayedItemIndex];
      }

      return !audioFile.IsNull;
    }

    public bool TryGetItemAt(int index, out IAudiobookPart audioFile)
    {
      audioFile = AudioFile.NullObject;
      if (!this.Files.Any() || index < 0 || index >= this.Files.Count)
      {
        return false;
      }

      audioFile = this.Files[index];
      return true;
    }

    public void UpdateLastPlayed(IAudiobookPart audioFile)
    {
      if (Contains(audioFile))
      {
        this.LastPlayedItemIndex = IndexOf(audioFile);
      }
    }

    protected virtual void OnPlaylistChanged(
      IEnumerable<IAudiobookPart> changedFiles,
      NotifyCollectionChangedAction collectionChangeAction,
      int changeIndex)
    {
      this.PlaylistChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(collectionChangeAction, changedFiles.ToList(), changeIndex));
    }

    public int IndexOf(IAudiobookPart audioFile) => this.Files.IndexOf(audioFile);
    public bool Contains(IAudiobookPart audioFile) => this.Files.Contains(audioFile);

    public event EventHandler<NotifyCollectionChangedEventArgs> PlaylistChanged;

    [DataMember]
    public int LastPlayedItemIndex { get; set; }

    [DataMember]
    public bool IsLoopEnabled { get; set; }

    [DataMember]
    public bool IsLoopCurrentFileEnabled { get; set; }

    [DataMember]
    public List<IAudiobookPart> Files { get; internal set; }

    #region Implementation of IExtensibleDataObject

    private ExtensionDataObject extensionData;
    /// <inheritdoc />
    public ExtensionDataObject ExtensionData { get => this.extensionData; set => this.extensionData = value; }

    #endregion
  }
}
