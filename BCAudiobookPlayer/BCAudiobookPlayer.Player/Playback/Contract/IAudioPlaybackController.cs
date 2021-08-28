using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Windows.Storage;

namespace BCAudiobookPlayer.Player.Playback.Contract
{
  public interface IAudioPlaybackController
  {
    void InitializeAudiobook(IAudiobook audiobook);
    void AddToPlaylist(IAudiobookPart audioPart);
    void InsertIntoPlaylist(IAudiobookPart audioPart, int index);
    void RemoveFromPlaylist(IAudiobookPart audioPart);
    event EventHandler<NotifyCollectionChangedEventArgs> PlaylistChanged;
    event EventHandler<ValueChangedEventArgs<IAudiobookPart>> AudioPartStarted;
    event EventHandler<ValueChangedEventArgs<IAudiobookPart>> AudioPartStopped;
    event EventHandler<ValueChangedEventArgs<IAudiobookPart>> AudioPartPaused;
    void DisableCurrentFileLoop();
    void DisablePlaylistLoop();
    void Dispose();
    void EnableCurrentFileLoop();
    void EnablePlaylistLoop();
    IPlaylist Playlist { get; set; }
    bool IsPlaylistLoopEnabled { get; }
    Task JumpToAudiobookPartAsync(IAudiobook audiobook, IAudiobookPart targetPart);
    Task JumpToPosition(IAudiobookPart audiobookPart, TimeSpan position);
    Task JumpToPosition(int playlistIndex, TimeSpan position);
    void Pause(IAudiobookPart audioFile);
    void Pause(int playlistIndex);
    void PauseAll();
    Task Play(IAudiobookPart audioPart);
    Task Play(int playlistIndex);
    Task PlayAlone(IAudiobookPart audioFile);
    Task PlayAlone(int playlistIndex);
    Task PlayBookmark(IAudiobookPart audioFile, IBookmark bookmark);
    Task PlayBookmark(int playlistIndex, IBookmark bookmark);
    Task PlayNextAsync();
    Task PlayPreviousAsync();
    Task Resume(IAudiobookPart audioPart);
    Task Resume(int playlistIndex);
    void ResumeAll();
    Task SkipBack(IAudiobookPart previousAudioPart);
    Task SkipBack(int playlistIndex);
    Task SkipForward(IAudiobookPart audioFile);
    Task SkipForward(int playlistIndex);
    Task StartLoopRangeAsync(IAudiobookPart audioFile);
    void Stop(IAudiobookPart audioFile);
    void Stop(int playlistIndex);
    void StopAll();
    Task<(bool IsSuccessful, IAudioFile AudioFileInstance)> TryCreateAudioFileAsync(StorageFile rawFile);
    Task<(bool IsSuccessful, IAudiobook AudioBook)> TryCreateAudioBookAsync(StorageFolder rawFolder, IEnumerable<StorageFile> rawFolderContent);
    Task<(bool IsSuccessful, IHttpMediaStream HttpMediaStream)> TryCreateHttpMediaStreamAsync(string url, string title = null);
    bool HasPlayingAudioParts { get; }
  }
}