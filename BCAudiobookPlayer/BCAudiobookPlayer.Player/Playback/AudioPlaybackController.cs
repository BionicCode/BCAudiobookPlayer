using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Media.Audio;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.UI.Core;
using BCAudiobookPlayer.Player.Helper;
using BCAudiobookPlayer.Player.Playback.Contract;

namespace BCAudiobookPlayer.Player.Playback
{
  public class AudioPlaybackController : IDisposable, IAudioPlaybackController
  {
    public readonly TimeSpan ProgressReportTimerPeriod = TimeSpan.FromMilliseconds(500d);
    public AudioPlaybackController()
    {
      this.MediaGraphSettings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media) {MaxPlaybackSpeedFactor = 5};
      this.AudioGraphInputNodeReverseTable = new ConcurrentDictionary<AudioFileInputNode, IAudiobookPart>();
      this.AudioGraphInputNodeTable = new ConcurrentDictionary<IAudiobookPart, AudioFileInputNode>();
      this.Playlist = new Playlist();
    }

    /// <summary>
    /// Factory method that helps to create a valid instance where the file exists and the file type is supported.
    /// </summary>
    /// <param name="rawFile"></param>
    /// <param name="audioFileInstance"></param>
    /// <returns></returns>
    public async Task<(bool IsSuccessful, IAudioFile AudioFileInstance)> TryCreateAudioFileAsync(StorageFile rawFile)
    {
      if (!SupportedFileTypeValidator.IsValid(rawFile.FileType))
      {
        return (false, AudioFile.NullObject);
      }

     return await AudioFile.TryCreateAsync(rawFile);
    }

    public async Task<(bool IsSuccessful, IAudiobook AudioBook)> TryCreateAudioBookAsync(StorageFolder rawFolder, IEnumerable<StorageFile> rawFolderContent)
    {
      (bool IsSuccessFull, IAudiobook AudiobookInstance) result = await Audiobook.TryCreateAsync(rawFolder, rawFolderContent.ToList(), 0);

      if (result.IsSuccessFull)
      {
        InitializeAudiobook(result.AudiobookInstance);
        return (result.IsSuccessFull, result.AudiobookInstance);
      }

      return (false, Audiobook.NullObject);
    }

    public async Task<(bool IsSuccessful, IHttpMediaStream HttpMediaStream)> TryCreateHttpMediaStreamAsync(
      string url,
      string title = null)
    {
      return await HttpMediaStream.TryCreateAsync(url, title);
    }

    public void InitializeAudiobook(IAudiobook audiobook)
    {
      audiobook.PartCompleted -= HandleAudiobookContinuousPlay;
      audiobook.PartCompleted += HandleAudiobookContinuousPlay;
    }

    public void AddToPlaylist(IAudiobookPart audioPart)
    {
      ObserveNewPlayListItem(audioPart, NotifyCollectionChangedAction.Add);
      if (this.Playlist.TryAdd(audioPart) && audioPart is IAudiobook audiobook)
      {
        InitializeAudiobook(audiobook);
      }

      OnPlaylistChanged(new List<IAudiobookPart> { audioPart }, NotifyCollectionChangedAction.Add, this.Playlist.Files.Count - 1);
    }

    public void InsertIntoPlaylist(IAudiobookPart audioPart, int index)
    {
      ObserveNewPlayListItem(audioPart, NotifyCollectionChangedAction.Add);
      if (this.Playlist.TryInsert(audioPart, index) && audioPart is IAudiobook audiobook)
      {
        InitializeAudiobook(audiobook);
      }

      OnPlaylistChanged(new List<IAudiobookPart> { audioPart }, NotifyCollectionChangedAction.Add, index);
    }

    public void RemoveFromPlaylist(IAudiobookPart audioPart)
    {
      ObserveNewPlayListItem(audioPart, NotifyCollectionChangedAction.Remove);
      if (!this.Playlist.TryRemove(audioPart, out int index))
      {
        return;
      }
      if (this.AudioGraphInputNodeTable.TryRemove(audioPart, out AudioFileInputNode inputNode))
      {
        this.AudioGraphInputNodeReverseTable.TryRemove(inputNode, out audioPart);
        inputNode.FileCompleted -= OnAudioPartCompleted;
        inputNode.Stop();
        inputNode.RemoveOutgoingConnection(this.CurrentAudioFileOutputNode);
        inputNode?.Dispose();
        audioPart.Dispose();
        OnPlaylistChanged(new List<IAudiobookPart> { audioPart }, NotifyCollectionChangedAction.Remove, index);
      }
    }

    public IEnumerator<IAudiobookPart> PeekPlaylist()
    {
      foreach (IAudiobookPart audiobookPart in this.Playlist.Files)
      {
        yield return audiobookPart;
      }
    }

    /// <summary>
    /// Playback the file and add to playlist if not contained
    /// </summary>
    /// <returns></returns>
    public async Task Play(IAudiobookPart audioPart)
    {
      audioPart.NavigationInfo = PlaylistNavigationInfo.Next;
      //if (audioPart.IsPlaying)
      //{
      //  return;
      //}

      await InternalPlayAsync(audioPart);
    }

    /// <summary>
    /// Playback the current file in playlist
    /// </summary>
    /// <returns></returns>
    public async Task Play(int playlistIndex)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioFile))
      {
        await Play(audioFile);
      }
    }

    public async Task PlayNextAsync()
    {
      if (this.Playlist.TryGetNextItem(out IAudiobookPart nextFile))
      {
        await Play(nextFile);
      }
    }

    public async Task PlayPreviousAsync()
    {
      if (this.Playlist.TryGetPreviousItem(out IAudiobookPart previousFile))
      {
        await Play(previousFile);
      }
    }

    /// <summary>
    /// Plays the file and appends it to the playlist
    /// </summary>
    /// <param name="audioPart"></param>
    /// <param name="audioFile"></param>
    /// <returns></returns>
    public async Task PlayAlone(IAudiobookPart audioPart)
    {
      this.Playlist.Files
        .ToList()
        .ForEach((part) =>
        {
          if (this.AudioGraphInputNodeTable.TryGetValue(part, out AudioFileInputNode audioFileInputNode))
          {
            audioFileInputNode.Stop();
          }
        });

      await Play(audioPart);
    }

    /// <summary>
    /// Plays the file and appends it to the playlist
    /// </summary>
    /// <param name="playlistIndex"></param>
    /// <returns></returns>
    public async Task PlayAlone(int playlistIndex)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioPart))
      {
        await PlayAlone(audioPart);
      }
    }

    public async Task PlayBookmark(IAudiobookPart audioPart, IBookmark bookmark)
    {
      if (audioPart is IAudiobook audiobook)
      {
        IAudiobookPart currentlyPlayingPart = audiobook.CurrentPart;
        if (audiobook.TryMoveToPartAtAbsolutePosition(bookmark.Position, out (TimeSpan RelativeAudiobookPosition, IAudiobookPart AudiobookPart) bookmarkPositionResult))
        {
          Stop(currentlyPlayingPart);
          await InternalPlayAsync(audiobook);
          await JumpToPosition(bookmarkPositionResult.AudiobookPart, bookmarkPositionResult.RelativeAudiobookPosition);
          return;
        }
      }
      await JumpToPosition(audioPart, bookmark.Position);
    }

    public async Task PlayBookmark(int playlistIndex, IBookmark bookmark)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioFile))
      {
        await PlayBookmark(audioFile, bookmark);
      }
    }

    public void Stop(IAudiobookPart audioPart)
    {
      IAudiobookPart partToStop = audioPart;
      if (audioPart is IAudiobook audioBook)
      {
        partToStop = audioBook.CurrentPart;
        audioBook.IsStopped = true;
      }

      if (this.AudioGraphInputNodeTable.TryGetValue(partToStop, out AudioFileInputNode audioFileInputNode))
      {
        partToStop.IsStopped = true;
        audioFileInputNode.Stop();
        audioFileInputNode.Reset();

        OnAudioPartStopped(audioPart);
      }
    }

    public void StopAll()
    {
      this.Playlist.Files.ForEach(Stop);
    }

    public void Stop(int playlistIndex)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioFile))
      {
        Stop(audioFile);
      }
    }

    public void Pause(IAudiobookPart audioPart)
    {
      //if (!this.Playlist.Contains(audioPart))
      //{
      //  throw new InvalidOperationException("Trying to play a part that is not part of the playlist. Add file to playlist before playing");
      //}

      IAudiobookPart partToPause = audioPart;

      AudioFileInputNode audioFileInputNode = null;
      if (audioPart is IAudiobook audioBook)
      {
        partToPause = audioBook.CurrentPart;
      }

      if (this.AudioGraphInputNodeTable.TryGetValue(partToPause, out audioFileInputNode))
      {
        audioFileInputNode.Stop();
        partToPause.IsPaused = true;
        partToPause.CurrentPosition = audioFileInputNode.Position;

        OnAudioPartPaused(partToPause);
      }
    }

    public void Pause(int playlistIndex)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioFile))
      {
        Pause(audioFile);
      }
    }

    public void PauseAll()
    {
      this.Playlist.Files.ForEach(Pause);
    }

    public async Task Resume(IAudiobookPart audioPart)
    {
      await Play(audioPart);
    }

    public async Task Resume(int playlistIndex)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioFile))
      {
        await Resume(audioFile);
      }
    }

    public void ResumeAll()
    {
      this.Playlist.Files.ForEach(async audioPart => await Resume(audioPart));
    }

    public async Task SkipForward(IAudiobookPart audioPart)
    {
      //if (!this.Playlist.Contains(audioPart))
      //{
      //  throw new InvalidOperationException("Trying to play a part that is not part of the playlist. Add file to playlist before playing");
      //}

      if (audioPart is IAudiobook audioBook)
      {
        IAudiobookPart currentlyPlayingPart = audioBook.CurrentPart;
        if (audioBook.TryMoveToNextPart(out IAudiobookPart nextAudioPart))
        {
          Stop(currentlyPlayingPart);
          nextAudioPart.NavigationInfo = PlaylistNavigationInfo.Next;
          await InternalPlayAsync(audioBook);
        }
        return;
      }

      Stop(audioPart);

      if (this.Playlist.TryGetNextItem(out IAudiobookPart nextPart))
      {
        nextPart.NavigationInfo = PlaylistNavigationInfo.Next;
        await InternalPlayAsync(nextPart);
      }
    }

    public async Task SkipForward(int playlistIndex)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioFile))
      {
        await SkipForward(audioFile);
      }
    }

    public async Task SkipBack(IAudiobookPart audioPart)
    {
      //if (!this.Playlist.Contains(audioPart))
      //{
      //  throw new InvalidOperationException("Trying to play a part that is not part of the playlist. Add file to playlist before playing");
      //}

      if (audioPart is IAudiobook audioBook)
      {
        IAudiobookPart currentlyPlayingPart = audioBook.CurrentPart;
        Stop(currentlyPlayingPart);
        if (audioBook.TryMoveToPreviousPart(out IAudiobookPart previousAudioPart))
        {
          previousAudioPart.NavigationInfo = PlaylistNavigationInfo.Next;
        }
        await InternalPlayAsync(audioBook);
        return;
      }

      Stop(audioPart);
      if (this.Playlist.TryGetPreviousItem(out IAudiobookPart previousPart))
      {
        previousPart.NavigationInfo = PlaylistNavigationInfo.Previous;
        await InternalPlayAsync(previousPart);
      }
    }

    public async Task SkipBack(int playlistIndex)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioFile))
      {
        await SkipBack(audioFile);
      }
    }

    public async Task JumpToAudiobookPartAsync(IAudiobook audiobook, IAudiobookPart targetPart)
    {
      if (targetPart == audiobook.CurrentPart)
      {
        return;
      }

      IAudiobookPart lastPlayingPart = audiobook.CurrentPart;
      if (audiobook.TryMoveToPart(targetPart))
      {
        Stop(lastPlayingPart);
        await InternalPlayAsync(targetPart);
      }
    }

    public async Task JumpToPosition(IAudiobookPart audiobookPart, TimeSpan position)
    {
      TimeSpan coercedPosition = audiobookPart.IsLoopEnabled
        ? position <= audiobookPart.LoopRange.BeginTime
          ? audiobookPart.LoopRange.BeginTime
          : position >= audiobookPart.LoopRange.EndTime
            ? audiobookPart.LoopRange.BeginTime
            : position
        : position;

      if (audiobookPart is IAudiobook audiobook)
      {
        await PlayAudiobookAtPosition(audiobook, coercedPosition);
        return;
      }

      await InternalPlayAsync(audiobookPart, position);
    }

    public async Task JumpToPosition(int playlistIndex, TimeSpan position)
    {
      if (this.Playlist.TryGetItemAt(playlistIndex, out IAudiobookPart audioPart))
      {
        await JumpToPosition(audioPart, position);
      }
    }

    public async Task StartLoopRangeAsync(IAudiobookPart audioPart)
    {
      if (!this.Playlist.Contains(audioPart))
      {
        throw new InvalidOperationException("Trying to play a part that is not part of the playlist. Add file to playlist before playing");
      }

      //if (audioPart is IAudiobook audioBook)
      //{
      //  audioBook.IsStopped = true;
      //  audioPart = audioBook.CurrentPart;
      //}
      if (!audioPart.IsLoopEnabled)
      {
        audioPart.IsLoopEnabled = true;
      }

      await InternalPlayAsync(audioPart);
      OnAudioPartStarted(audioPart);
    }

    public void EnablePlaylistLoop() => this.Playlist.IsLoopEnabled = true;
    public void DisablePlaylistLoop() => this.Playlist.IsLoopEnabled = false;
    public void EnableCurrentFileLoop() => this.Playlist.IsLoopCurrentFileEnabled = true;
    public void DisableCurrentFileLoop() => this.Playlist.IsLoopCurrentFileEnabled = false;

    private async Task InitAudioGraph()
    {
      CreateAudioGraphResult result = await AudioGraph.CreateAsync(this.MediaGraphSettings);

      // Creation of the audio graph will fail e.g. if no audio device was detected (maybe no speakers plugged in while auto-disable of audio hardware when inactive is turned on)
      if (result.Status != AudioGraphCreationStatus.Success)
      {
        throw new InvalidOperationException("Error initializing audio graph. Check audio device.");
      }

      this.AudioGraph = result.Graph;
      await CreateDefaultDeviceOutputNode();
      this.AudioGraph.Start();
    }


    /// <summary>
    /// Create a node to output audio data to the default audio device (e.g. soundcard)
    /// </summary>
    private async Task CreateDefaultDeviceOutputNode()
    {
      CreateAudioDeviceOutputNodeResult result = await this.AudioGraph.CreateDeviceOutputNodeAsync();

      if (result.Status != AudioDeviceNodeCreationStatus.Success)
      {
        throw new Exception(result.Status.ToString());
      }

      this.CurrentAudioFileOutputNode = result.DeviceOutputNode;
    }

    private async Task<AudioFileInputNode> CreateFileInputNode(StorageFile newAudioFile)
    {
      // File can be null if cancel is hit in the file picker
      if (newAudioFile == null)
      {
        return null;
      }

      CreateAudioFileInputNodeResult result = await this.AudioGraph.CreateFileInputNodeAsync(newAudioFile);

      if (result.Status != AudioFileNodeCreationStatus.Success)
      {
        throw new InvalidOperationException("Error initializing audio graph");
      }

      // Connect output node
      if (!result.FileInputNode.OutgoingConnections.Any())
      {
        result.FileInputNode.AddOutgoingConnection(this.CurrentAudioFileOutputNode);
      }

      result.FileInputNode.Stop();
      result.FileInputNode.Reset();
      return result.FileInputNode;
    }

    private async Task InternalPlayAsync(IAudiobookPart audioPart)
    {
      if (audioPart is IHttpMediaStream)
      {
        return;
      }

      var audioFile = audioPart as IAudiobookPart;
      if (audioPart is IAudiobook audioBook)
      {
        //audioBook.IsPlaying = true;
        audioFile = (IAudiobookPart) audioBook.CurrentPart;
      }

      if (!this.AudioGraphInputNodeTable.TryGetValue(audioFile, out AudioFileInputNode audioFileInputNode))
      {
        StorageFile storageFile = audioPart is IAudiobook 
          ? await GetFileFromFolder(audioFile.FileSystemPathToken, audioFile.FileName) 
          : await StorageApplicationPermissions.FutureAccessList.GetFileAsync(audioPart.FileSystemPathToken);
        if (storageFile == null)
        {
          return;
        }

        audioFileInputNode = await InitializeAudiobookPartAsync(audioFile, storageFile);
      }

      audioFileInputNode.OutgoingGain = audioPart.Volume;
      audioFile.IsPlaying = true;
      audioFileInputNode.Start();
      this.Playlist.UpdateLastPlayed(audioPart);
      OnAudioPartStarted(audioPart);
    }

    private async Task<StorageFile> GetFileFromFolder(string audiobookFileSystemPathToken, string fileName)
    {
      StorageFolder storageFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(
        audiobookFileSystemPathToken,
        AccessCacheOptions.FastLocationsOnly);
      return await storageFolder.GetFileAsync(fileName);
    }

    private async Task InternalPlayAsync(IAudiobookPart audioPart, TimeSpan playbackPosition)
    {
      await InternalPlayAsync(audioPart);
      if (audioPart is IAudiobook audiobook)
      {
        audioPart = audiobook.CurrentPart;
      }

      if (this.AudioGraphInputNodeTable.TryGetValue(audioPart, out AudioFileInputNode audioFileInputNode))
      {
        audioFileInputNode.Seek(playbackPosition);
      }
    }

    private async Task<AudioFileInputNode> InitializeAudiobookPartAsync(IAudiobookPart audiobookPart, StorageFile rawFile)
    {
      if (!(audiobookPart is IAudiobookPart audioFile) || this.AudioGraphInputNodeTable.ContainsKey(audioFile))
      {
        return null;
      }

      if (this.AudioGraph == null)
      {
        await InitAudioGraph();
      }

      AudioFileInputNode audioInputNode = await CreateFileInputNode(rawFile);
      audioInputNode.FileCompleted += OnAudioPartCompleted;
      audioInputNode.Seek(audiobookPart.CurrentPosition);
      this.AudioGraphInputNodeTable.TryAdd(audioFile, audioInputNode);
      this.AudioGraphInputNodeReverseTable.TryAdd(audioInputNode, audioFile);

      //audiobookPart.EncodingProperties = audioInputNode.EncodingProperties;
      audiobookPart.LoopRange = (TimeSpan.Zero, audiobookPart.Tag.Duration);
      audiobookPart.TimeRemaining = audiobookPart.Tag.Duration;
      audiobookPart.Duration = audiobookPart.Tag.Duration;
      audiobookPart.UpdateCurrentPositionCallback = () => UpdateCurrentPosition(audiobookPart, audioInputNode);
      return audioInputNode;
    }

    private async void HandleAudiobookContinuousPlay(object sender, PartCompletedEventArgs<MusicProperties> partCompletedEventArgs)
    {
      var audiobook = sender as IAudiobook;
      Stop(partCompletedEventArgs.CompletedPart);
      if (partCompletedEventArgs.NavigationInfo != PlaylistNavigationInfo.Completed)
      {
        await InternalPlayAsync(audiobook);
      }
    }

    private void UpdateCurrentPosition(IAudiobookPart audiobookPart, AudioFileInputNode audioInputNode)
    {
      if (audioInputNode == null || audiobookPart.IsStopped || audiobookPart.CurrentPosition == audioInputNode.Position)
      {
        return;
      }
      audiobookPart.CurrentPosition = audioInputNode.Position;
      audiobookPart.TimeRemaining = audiobookPart.Duration.Subtract(audiobookPart.CurrentPosition);
    }

    private async Task PlayAudiobookAtPosition(IAudiobook audiobook, TimeSpan position)
    {
      IAudiobookPart oldPart = audiobook.CurrentPart;

      if (audiobook.TryMoveToPartAtAbsolutePosition(position, out (TimeSpan RelativePosition, IAudiobookPart AudiobookPart) targetPartInfo))
      {
        if (!oldPart.Equals(targetPartInfo.AudiobookPart))
        {
          Stop(oldPart);
        }
        await InternalPlayAsync(audiobook, targetPartInfo.RelativePosition);
      }
    }

    private void ObserveNewPlayListItem(IAudiobookPart audioPart, NotifyCollectionChangedAction changeAction)
    {
      if (this.isInternalCollectionChangeAction)
      {
        return;
      }
      if (changeAction == NotifyCollectionChangedAction.Add)
      {
        audioPart.PropertyChanged += HandleAudioPartPropertyChanged;
        return;
      }
      if (changeAction == NotifyCollectionChangedAction.Remove)
      {
        audioPart.PropertyChanged -= HandleAudioPartPropertyChanged;
        return;
      }
    }

    private void HandleAudioPartPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      IAudiobookPart audiobookPart = sender is IAudiobook audiobook ? audiobook.CurrentPart : (IAudiobookPart) sender;
      if (audiobookPart == null)
      {
        return;
      }

      AudioFileInputNode fileInputNode;
      switch (e.PropertyName)
      {
        case nameof(audiobookPart.Volume):
          if (this.AudioGraphInputNodeTable.TryGetValue(audiobookPart, out fileInputNode))
          {
            fileInputNode.OutgoingGain = audiobookPart.Volume;
          }

          break;

        case nameof(audiobookPart.LoopRange):
          if (audiobookPart.IsLoopEnabled && this.AudioGraphInputNodeTable.TryGetValue(audiobookPart, out fileInputNode))
          {
            fileInputNode.StartTime = audiobookPart.LoopRange.BeginTime;
            fileInputNode.EndTime = audiobookPart.LoopRange.EndTime;
          }
          break;

        case nameof(audiobookPart.SpeedMultiplier):
          if (this.AudioGraphInputNodeTable.TryGetValue(audiobookPart, out fileInputNode))
          {
            fileInputNode.PlaybackSpeedFactor = audiobookPart.SpeedMultiplier;
          }
          break;

        case nameof(audiobookPart.LoopCount):
          if (audiobookPart.IsLoopEnabled && this.AudioGraphInputNodeTable.TryGetValue(audiobookPart, out fileInputNode))
          {
            fileInputNode.LoopCount = audiobookPart.LoopCount;
          }
          break;

        case nameof(audiobookPart.IsLoopEnabled):
          this.isDisablingLoop = true;

          ////TODO::validate line below
          //this.IsPaused = true;
          //this.AudioGraphInputNode.StartTime = value ? this.LoopRange.BeginTime : TimeSpan.Zero;
          //this.AudioGraphInputNode.EndTime = value ? this.LoopRange.EndTime : this.Tag.Duration;
          if (this.AudioGraphInputNodeTable.TryGetValue(audiobookPart, out fileInputNode))
          {
            fileInputNode.LoopCount = audiobookPart.IsLoopEnabled ? audiobookPart.LoopCount : 0;
          }

          if (audiobookPart.IsLoopEnabled && audiobookPart.CurrentPosition > audiobookPart.LoopRange.EndTime)
          {
            //this.IsPlaying = true;
            this.AudioGraphInputNodeTable[audiobookPart].Reset();
          }

          this.isDisablingLoop = false;
          break;
      }
    }

    protected virtual void OnPlaylistChanged(
      IEnumerable<IAudiobookPart> changedFiles,
      NotifyCollectionChangedAction collectionChangeAction,
      int changeIndex)
    {
      this.PlaylistChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(collectionChangeAction, changedFiles.ToList(), changeIndex));
    }

    private async void OnAudioPartCompleted(AudioFileInputNode audioFileInputNode, object args)
    {
      if (this.isDisablingLoop)
      {
        return;
      }

      if (!this.AudioGraphInputNodeReverseTable.TryGetValue(audioFileInputNode, out IAudiobookPart audioPart) || audioPart.IsLoopEnabled)
      {
        return;
      }

      await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
        CoreDispatcherPriority.Low,
        async () =>
        {
          audioPart.IsCompleted = true;
          Stop(audioPart);
      if (this.Playlist.IsLoopEnabled)
      {
        await PlayNextAsync();
      }
        });
    }

    protected virtual void OnAudioPartStarted(IAudiobookPart audioPart)
    {
      this.AudioPartStarted?.Invoke(this, new ValueChangedEventArgs<IAudiobookPart>(audioPart));
    }

    protected virtual void OnAudioPartStopped(IAudiobookPart audioPart)
    {
      this.AudioPartStopped?.Invoke(this, new ValueChangedEventArgs<IAudiobookPart>(audioPart));
    }

    protected virtual void OnAudioPartPaused(IAudiobookPart audioPart)
    {
      this.AudioPartPaused?.Invoke(this, new ValueChangedEventArgs<IAudiobookPart>(audioPart));
    }


    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
      if (!this.disposedValue)
      {
        if (disposing)
        {
          // TODO: dispose managed state (managed objects).
          this.AudioGraph?.Dispose();
          this.CurrentAudioFileOutputNode?.Dispose();
          this.AudioGraphInputNodeTable.Values.ToList().ForEach((audioInputNode) => audioInputNode.Dispose());
          this.AudioGraphInputNodeTable = null;
          this.AudioGraphInputNodeReverseTable.Keys.ToList().ForEach((audioInputNode) => audioInputNode.Dispose());
          this.AudioGraphInputNodeReverseTable = null;
        }

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.

        this.disposedValue = true;
      }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    //~AudioPlaybackController()
    //{
    //  // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    //  Dispose(false);
    //}

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
      // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
      Dispose(true);
      // TODO: uncomment the following line if the finalizer is overridden above.
      //GC.SuppressFinalize(this);
    }
    #endregion

    public event EventHandler<NotifyCollectionChangedEventArgs> PlaylistChanged;
    public event EventHandler<ValueChangedEventArgs<IAudiobookPart>> AudioPartStarted;
    public event EventHandler<ValueChangedEventArgs<IAudiobookPart>> AudioPartStopped;
    public event EventHandler<ValueChangedEventArgs<IAudiobookPart>> AudioPartPaused;
    public bool HasPlayingAudioParts { get => this.Playlist.TryGetLastPlayedItem(out IAudiobookPart lastPLayedPart) && lastPLayedPart.IsPlaying || this.Playlist.Files.Any((audioPart) => audioPart.IsPlaying); }
    //public event EventHandler<ValueChangedEventArgs<IAudiobook>> AudiobookPaused;
    //public event EventHandler<ValueChangedEventArgs<IAudiobook>> AudiobookStarted;
    //public event EventHandler<ValueChangedEventArgs<IAudiobook>> AudiobookStopped;
    public bool IsPlaylistLoopEnabled => this.Playlist.IsLoopEnabled;
    public IPlaylist Playlist { get; set; }
    private ConcurrentDictionary<IAudiobookPart, AudioFileInputNode> AudioGraphInputNodeTable { get; set; }    
    private ConcurrentDictionary<AudioFileInputNode, IAudiobookPart> AudioGraphInputNodeReverseTable { get; set; }    
    private AudioGraph AudioGraph { get; set; }
    private AudioDeviceOutputNode CurrentAudioFileOutputNode { get; set; }
    private AudioGraphSettings MediaGraphSettings { get; set; }
    private bool isDisablingLoop;
    private bool isInternalCollectionChangeAction;
  }
}
