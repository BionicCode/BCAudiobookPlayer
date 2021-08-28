using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Media.MediaProperties;
using Windows.Storage.FileProperties;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer.Player.Playback.Contract
{
  public interface IAudiobookPart : INotifyPropertyChanged, IDisposable
  {
    event EventHandler Completed;
    event EventHandler CurrentPositionChanged;
    event EventHandler Started;
    event EventHandler Stopped;
    event EventHandler Paused;
    void SoftReset();
    void SetToIsCreating();
    void SetToIsCreated();
    bool TryAddBookmark(IBookmark bookmark);
    void RemoveBookmark(IBookmark bookmark);
    ObservableCollection<IBookmark> Bookmarks { get; }
    PlaylistNavigationInfo NavigationInfo { get; set; }
    Action UpdateCurrentPositionCallback { get; set; }
    IBookmark this[int bookmarkIndex] { get; set; }
    IMediaTag<MusicProperties> Tag { get; set; }
    //AudioEncodingProperties EncodingProperties { get; set; }
    TimeSpan CurrentPosition { get; set; }
    TimeSpan TimeRemaining { get; set; }
    TimeSpan Duration { get; set; }
    bool HasProgress { get; }
    bool IsCompleted { get; set; }
    bool IsPlaying { get; set; }
    bool IsPaused { get; set; }
    bool IsStopped { get; set; }
    bool IsLoopEnabled { get; set; }
    int? LoopCount { get; set; }
    (TimeSpan BeginTime, TimeSpan EndTime) LoopRange { get; set; }
    bool IsNull { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    double SpeedMultiplier { get; set; }
    bool IsSelected { get; set; }
    bool IsVisible { get; set; }
    object SyncLock { get; }
    string FileSystemPath { get; }
    string FileName { get; }
    string FileSystemPathToken { get; set; }
    bool IsCreating { get; }
    bool IsCreated { get; }
  }
}