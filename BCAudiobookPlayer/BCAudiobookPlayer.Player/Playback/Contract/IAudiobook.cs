using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Storage;
using Windows.Storage.FileProperties;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer.Player.Playback.Contract
{
  public interface IAudiobook : IAudiobookPart
  {
    void Update();
    void SetCurrentPart(IAudiobookPart currentPart);
    bool TryMoveToPart(IAudiobookPart audiobookPart);
    bool TryMoveToNextPart(out IAudiobookPart nextAudiobookPart);
    bool TryMoveToPreviousPart(out IAudiobookPart previousAudiobookPart);
    bool TryMoveToPartAtAbsolutePosition(TimeSpan absoluteAudiobookPosition, out (TimeSpan RelativeAudiobookPosition, IAudiobookPart AudiobookPart) audiobookPartInfo);
    bool TryMoveToBookmarkedPart(Bookmark bookmark, out IAudiobookPart audiobookPart);
    bool TryGetBookmarksOfPart(IAudiobookPart audiobookPart, out IEnumerable<IBookmark> partBookmarks);
    void RemoveBookmarksOf(IAudiobookPart audiobookPart);
    event EventHandler<PartCompletedEventArgs<MusicProperties>> PartCompleted;
    bool TryGetPartAt(int partIndex, out IAudiobookPart audiobookPart);
    bool IsContinuousPlayEnabled { get; set; }
    bool IsExpanded { get; set; }
    ObservableCollection<IAudiobookPart> Parts { get; set; }
    IAudiobookPart CurrentPart { get; }
    int CurrentPartIndex { get; }
    int PartCount { get; set; }
  }
}