using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer.Player.Playback.Contract
{
  public interface IPlaylist : IExtensibleDataObject
  {
    bool IsLoopCurrentFileEnabled { get; set; }
    bool IsLoopEnabled { get; set; }
    int LastPlayedItemIndex { get; set; }
    List<IAudiobookPart> Files { get; }
    event EventHandler<NotifyCollectionChangedEventArgs> PlaylistChanged;

    bool TryAdd(IAudiobookPart audioFile);
    bool TryAddRange(IEnumerable<IAudiobookPart> audioFiles);
    bool Contains(IAudiobookPart audioFile);
    int IndexOf(IAudiobookPart audioFile);
    bool TryInsert(IAudiobookPart audioFile, int index);
    bool TryInsertRange(IEnumerable<IAudiobookPart> audioFiles, int index);
    bool TryGetItemAt(int index, out IAudiobookPart audioFile);
    bool TryGetLastPlayedItem(out IAudiobookPart audioFile);
    bool TryGetNextItem(out IAudiobookPart audioFile);
    bool TryGetPreviousItem(out IAudiobookPart audioFile);
    bool TryRemove(IAudiobookPart audioFile, out int index);
    bool TryRemoveAt(int index, out IAudiobookPart removedFile);
    void UpdateLastPlayed(IAudiobookPart audioFile);
  }
}