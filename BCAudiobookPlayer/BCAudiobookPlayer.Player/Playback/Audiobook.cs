using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using BCAudiobookPlayer.Player.Helper;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;
using BCAudiobookPlayer.ResourceProvider;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.Player.Playback
{
  [DataContract]
  public sealed class Audiobook : IAudiobook, IAudiobookPart
  {
    public static event EventHandler<ValueChangedEventArgs<IAudiobookPart>> PartCreated;
    public static event EventHandler Created;
    public static IAudiobook NullObject => new Audiobook(true);
    private const int ParallelPartCreationCount = 50;

    internal Audiobook()
    {
      this.FileSystemPathToken = string.Empty;
      this.FileSystemPath = string.Empty;
      this.FileName = string.Empty;
      this.IsProgressTimerInitialized = false;
      this.IsProgressTimerStarted = false;
      this.SyncLock = new object();
      this.volume = 1.0;
      this.isStopped = true;
      this.isPaused = false;
      this.isPlaying = false;
      this.loopCount = null;
      this.loopRange = (TimeSpan.MinValue, TimeSpan.MinValue);
      this.timeRemaining = TimeSpan.Zero;
      this.currentPosition = TimeSpan.Zero;
      this.bookmarks = new ObservableCollection<IBookmark>();
      this.tag = AudioMediaTag.NullObject;
    }

    private Audiobook(bool isNull) : this()
    {
      this.IsNull = isNull;
      this.IsCreated = false;
    }

    internal Audiobook(StorageFolder folderInfo) : this()
    {
      this.FileSystemPath = folderInfo.Path;
      this.FileName = folderInfo.Name;
      this.Parts = new ObservableCollection<IAudiobookPart>();
      this.IsContinuousPlayEnabled = true;
      this.CurrentPart = AudioFile.NullObject;
      if (ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap.TryGetValue(
        folderInfo.Path,
        out string filePathToken))
      {
        this.FileSystemPathToken = filePathToken;
      }
    }

    public void SoftReset()
    {
      this.isPlaying = false;
      this.isPaused = false;
      this.isStopped = true;
    }

    public void RemoveBookmarksOf(IAudiobookPart audiobookPart)
    {
      if (!this.Parts.Contains(audiobookPart))
      {
        throw new ArgumentException("Part does not belong to this audiobook instance.", nameof(audiobookPart));
      }

      int partIndex = this.Parts.IndexOf(audiobookPart);
      this.bookmarks.ToList().ForEach(
        (mark) =>
        {
          if (mark.AudioPartIndex.Equals(partIndex))
          {
            RemoveBookmark(mark);
          }
        });
    }

    public void SetCurrentPart(IAudiobookPart newCurrentPart)
    {
      if (!this.Parts.Contains(newCurrentPart))
      {
        throw new ArgumentException("Part doesn't belong to audiobook instance.", nameof(newCurrentPart));
      }

      this.CurrentPartIndex = this.Parts.IndexOf(newCurrentPart);
      this.CurrentPart = newCurrentPart;
    }

    internal static async Task<(bool IsSuccessFull, IAudiobook AudiobookInstance)> TryCreateAsync(
      StorageFolder rawFolder,
      List<StorageFile> rawFolderContent,
      int startingPartFileIndex = 0)
    {
      (bool IsSuccessFull, IAudiobook AudiobookInstance) result = (false, Audiobook.NullObject);
      if (rawFolder == null)
      {
        return result;
      }

      IAudiobook audiobookInstance = new Audiobook(rawFolder);

      if (audiobookInstance.IsNull || !rawFolderContent.Any())
      {
        return result;
      }

      result.AudiobookInstance = audiobookInstance;
      audiobookInstance.SetToIsCreating();
      if (startingPartFileIndex >= rawFolderContent.Count || startingPartFileIndex < 0)
      {
        startingPartFileIndex = 0;
      }

      result.IsSuccessFull = true;

      StorageFile playlistFile = rawFolderContent.FirstOrDefault(
        (storageFile) => storageFile.FileType.Equals(".m3u", StringComparison.OrdinalIgnoreCase));
      rawFolderContent.RemoveAll((file) => !SupportedFileTypeValidator.IsValid(file.FileType));

      int index = 0;
      var indexedPlaylist = new Dictionary<string, int>();
      bool isPlaylistValid = await SupportedFileTypeValidator.IsPlaylistValidAsync(playlistFile);

      if (isPlaylistValid)
      {
        indexedPlaylist = rawFolderContent.ToDictionary((storageFile) => storageFile.Name, (storageFile) => index++);

        startingPartFileIndex = rawFolderContent.FindIndex(
          (storageFile) => storageFile.Name.Equals(
            indexedPlaylist.Keys.ElementAt(startingPartFileIndex),
            StringComparison.OrdinalIgnoreCase));
      }

      if (!isPlaylistValid || !indexedPlaylist.Any())
      {
        indexedPlaylist = rawFolderContent.ToDictionary((storageFile) => storageFile.Name, (storageFile) => index++);
      }

      StorageFile startingFile = rawFolderContent[startingPartFileIndex];

      var dummyParts = new IAudiobookPart[indexedPlaylist.Count];
      Array.Fill(dummyParts, AudioFile.NullObject);
      audiobookInstance.Parts = new ObservableCollection<IAudiobookPart>(dummyParts);
      audiobookInstance.PartCount = indexedPlaylist.Count;

      if (await Audiobook.TryCreateStartingPartInstanceAsync(
        audiobookInstance,
        (startingFile, indexedPlaylist[startingFile.Name])))
      {
        rawFolderContent.RemoveAt(startingPartFileIndex);
        indexedPlaylist.Remove(startingFile.Name);
      }
      TaskScheduler synchronizationContext = TaskScheduler.FromCurrentSynchronizationContext();
      Task.Factory.StartNew(
        () =>
        {
          int creatingPartsCounter = indexedPlaylist.Count;
          bool isStartingPartCreated = false;
          Parallel.ForEach(
            rawFolderContent,
            new ParallelOptions() {TaskScheduler = synchronizationContext},
            async (nextFileInOrder) =>
            {
              if (indexedPlaylist.TryGetValue(nextFileInOrder.Name, out int fileIndex))
              {
                await Audiobook.CreatePart(audiobookInstance, nextFileInOrder, fileIndex);
                Interlocked.Decrement(ref creatingPartsCounter);
                if (creatingPartsCounter.Equals(0))
                {
                  audiobookInstance.SetToIsCreated();
                }
              }
            });
          //audiobookInstance.SetToIsCreated();
        },
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        synchronizationContext);

      return result;
    }

    private static async Task<bool> TryCreateStartingPartInstanceAsync(
      IAudiobook audiobookInstance,
      (StorageFile File, int FileIndex) startingPartInfo)
    {
      (bool IsSuccessFull, IAudiobook AudiobookInstance) result = (false, Audiobook.NullObject);

      (bool IsSuccessful, IAudiobookPart StartingAudiobookPart) createdStartingPartResult =
        await AudioFile.TryCreateAsync(startingPartInfo.File);
      if (createdStartingPartResult.IsSuccessful)
      {
        // Replace the dummy part with the starting audiobook part
        audiobookInstance.Parts[startingPartInfo.FileIndex] = createdStartingPartResult.StartingAudiobookPart;
        ////createdStartingPartResult.StartingAudiobookPart.FileSystemPathToken = 
        audiobookInstance.Tag = await AudioMediaTag.Create(startingPartInfo.File, string.Empty);
        audiobookInstance.Tag.IsAutoSaveTagChangesEnabled = false;
        audiobookInstance.Tag.CoverArt = createdStartingPartResult.StartingAudiobookPart.Tag.CoverArt;
        audiobookInstance.SetCurrentPart(createdStartingPartResult.StartingAudiobookPart);
        Audiobook.OnPartCreated(audiobookInstance, createdStartingPartResult.StartingAudiobookPart);
      }

      result.IsSuccessFull = !audiobookInstance.IsNull;
      result.AudiobookInstance = audiobookInstance;
      return result.IsSuccessFull;
    }

    private static async Task CreatePart(IAudiobook audiobookInstance, StorageFile file, int fileIndex)
    {
      (bool IsSuccessful, IAudiobookPart CreatedAudiobook) result =
        await Audiobook.CreateAudiobookPartInstance(audiobookInstance, (file, fileIndex));
      if (result.IsSuccessful)
      {
        Audiobook.OnPartCreated(audiobookInstance, result.CreatedAudiobook);
      }
    }

    private static async Task<(bool IsSuccessful, IAudiobookPart CreatedAudiobook)> CreateAudiobookPartInstance(
      IAudiobook audiobookInstance,
      (StorageFile File, int FileIndex) partInfo)
    {
      (bool IsSuccessful, IAudiobookPart Audiobook) createdPartResult = await AudioFile.TryCreateAsync(partInfo.File);
      if (createdPartResult.IsSuccessful)
      {
        // Replace the dummy part with the fresh audiobook part at the corresponding index result from sorting
        audiobookInstance.Parts[partInfo.FileIndex] = createdPartResult.Audiobook;
      }

      return createdPartResult;
    }

    private static async Task<IDictionary<string, int>> IndexPlaylistFileAsync(StorageFile playlistFile)
    {
      var indexedPartList = new Dictionary<string, int>();

      using (IRandomAccessStreamWithContentType fileStream = await playlistFile.OpenReadAsync())
      {
        using (var reader = new StreamReader(fileStream.AsStreamForRead()))
        {
          int index = 0;
          while (!reader.EndOfStream)
          {
            string lineContent = await reader.ReadLineAsync();
            while (!reader.EndOfStream && lineContent.StartsWith('#'))
            {
              lineContent = await reader.ReadLineAsync();
            }

            string nextPlaylistEntryName = HttpUtility.UrlDecode(lineContent);
            if (!string.IsNullOrWhiteSpace(nextPlaylistEntryName))
            {
              indexedPartList.Add(nextPlaylistEntryName, index++);
            }
          }
        }
      }

      return indexedPartList;
    }

    public bool TryGetPartAt(int partIndex, out IAudiobookPart audiobookPart)
    {
      audiobookPart = Audiobook.NullObject;
      if (partIndex > -1 && partIndex < this.Parts.Count)
      {
        audiobookPart = this.Parts[partIndex];
        return true;
      }

      return false;
    }

    public bool TryMoveToPart(IAudiobookPart audiobookPart)
    {
      if (!audiobookPart.IsCreated)
      {
        return false;
      }

      int oldIndex = this.CurrentPartIndex;
      SetCurrentPart(audiobookPart);
      this.NavigationInfo = oldIndex.Equals(this.CurrentPartIndex)
        ? PlaylistNavigationInfo.Current
        : oldIndex < this.CurrentPartIndex
          ? PlaylistNavigationInfo.Next
          : PlaylistNavigationInfo.Previous;
      return true;
    }

    public bool TryMoveToPartAtAbsolutePosition(
      TimeSpan absoluteAudiobookPosition,
      out (TimeSpan RelativeAudiobookPosition, IAudiobookPart AudiobookPart) audiobookPartInfo)
    {
      audiobookPartInfo = (absoluteAudiobookPosition, this.CurrentPart);
      if (this.PartCount.Equals(1))
      {
        return this.Duration >= absoluteAudiobookPosition;
      }

      long requiredDuration = 0;
      foreach (IAudiobookPart part in this.Parts)
      {
        requiredDuration += part.Duration.Ticks;
        if (requiredDuration >= absoluteAudiobookPosition.Ticks)
        {
          if (!part.IsCreated)
          {
            return false;
          }

          audiobookPartInfo.AudiobookPart = part;
          long remainingTicksOfRequiredPart = requiredDuration - absoluteAudiobookPosition.Ticks;
          audiobookPartInfo.RelativeAudiobookPosition = TimeSpan.FromTicks(part.Duration.Ticks - remainingTicksOfRequiredPart);
          SetCurrentPart(part);
          this.CurrentPosition = absoluteAudiobookPosition;
          return true;
        }
      }

      return false;
    }

    public bool TryGetBookmarksOfPart(IAudiobookPart audiobookPart, out IEnumerable<IBookmark> partBookmarks)
    {
      if (!this.Parts.Contains(audiobookPart))
      {
        throw new ArgumentException("Part does not belong to this audiobook instance", nameof(audiobookPart));
      }

      int partIndex = this.Parts.IndexOf(audiobookPart);
      partBookmarks = this.bookmarks.Where((bookmark) => bookmark.AudioPartIndex.Equals(partIndex));
      return partBookmarks.Any();
    }

    public bool TryMoveToBookmarkedPart(Bookmark bookmark, out IAudiobookPart audiobookPart)
    {
      audiobookPart = AudioFile.NullObject;
      if (!this.bookmarks.Contains(bookmark))
      {
        return false;
      }

      audiobookPart = this.Parts[bookmark.AudioPartIndex];
      if (audiobookPart.IsCreated)
      {
        return false;
      }

      SetCurrentPart(audiobookPart);
      return true;
      ;
    }

    public bool TryMoveToNextPart(out IAudiobookPart nextAudiobookPart)
    {
      nextAudiobookPart = Audiobook.NullObject;
      if (TryGetPartAt(this.CurrentPartIndex + 1, out nextAudiobookPart) && nextAudiobookPart.IsCreated)
      {
        this.NavigationInfo = PlaylistNavigationInfo.Next;
        nextAudiobookPart.NavigationInfo = this.NavigationInfo;
        SetCurrentPart(nextAudiobookPart);
        return true;
      }

      return false;
    }

    public bool TryMoveToPreviousPart(out IAudiobookPart previousAudiobookPart)
    {
      previousAudiobookPart = Audiobook.NullObject;
      if (TryGetPartAt(this.CurrentPartIndex - 1, out previousAudiobookPart) && previousAudiobookPart.IsCreated)
      {
        this.NavigationInfo = PlaylistNavigationInfo.Previous;
        previousAudiobookPart.NavigationInfo = this.NavigationInfo;
        SetCurrentPart(previousAudiobookPart);
        return true;
      }

      return false;
    }

    public void Update()
    {
      if (this.Tag?.IsNull ?? true)
      {
        this.Tag = this.Parts.FirstOrDefault()?.Tag ?? AudioMediaTag.NullObject;
      }

      //UpdateDuration();
      //ListenToAudiobookPartOnPartAdded(this.Parts);
      //SetCurrentPart(this.Parts[this.CurrentPartIndex]);
      SetCurrentPositionOnCurrentPartChanged();
    }

    private void HandleCurrentPartChanged(IAudiobookPart lastPlayedPart)
    {
      if (lastPlayedPart != null)
      {
        this.CurrentPart.Completed -= HandleContinuousPlay;
        lastPlayedPart.Stopped -= HandlePartStopped;
        lastPlayedPart.Started -= HandlePartStarted;
        lastPlayedPart.Paused -= HandlePartPaused;
        lastPlayedPart.CurrentPositionChanged -= UpdateCurrentPositionOnPartCurrentPositionChanged;
      }

      if (this.CurrentPart == null)
      {
        return;
      }

      SetCurrentPositionOnCurrentPartChanged();
      this.CurrentPart.Volume = this.Volume;
      this.CurrentPart.SpeedMultiplier = this.SpeedMultiplier;
      this.Tag = this.CurrentPart.Tag;

      this.CurrentPart.Completed += HandleContinuousPlay;
      this.CurrentPart.Stopped += HandlePartStopped;
      this.CurrentPart.Started += HandlePartStarted;
      this.CurrentPart.Paused += HandlePartPaused;
      this.CurrentPart.CurrentPositionChanged += UpdateCurrentPositionOnPartCurrentPositionChanged;
    }

    private void HandlePartPaused(object sender, EventArgs e)
    {
      this.IsPaused = true;
    }

    private void HandlePartStarted(object sender, EventArgs e)
    {
      this.IsPlaying = true;
    }

    private void HandlePartStopped(object sender, EventArgs e)
    {
      //this.IsStopped = !this.isContinuousPlayEnabled;
    }

    private void SetCurrentPositionOnCurrentPartChanged()
    {
      if (this.Parts.Any((part) => part.Tag?.IsNull ?? true))
      {
        return;
      }

      var totalTicksPlayed = this.CurrentPartIndex.Equals(0)
        ? this.CurrentPart.CurrentPosition.Ticks
        : this.Parts.Take(this.CurrentPartIndex)
            .Sum((part) => part.Duration.Ticks) + this.CurrentPart.CurrentPosition.Ticks;
      this.CurrentPositionOffset = TimeSpan.FromTicks(totalTicksPlayed);
      this.CurrentPosition = this.CurrentPositionOffset;
    }

    private void UpdateCurrentPositionOnPartCurrentPositionChanged(object sender, EventArgs e)
    {
      if (this.CurrentPosition.Equals(this.CurrentPart.CurrentPosition))
      {
        return;
      }

      this.CurrentPosition = this.CurrentPositionOffset.Add(this.CurrentPart.CurrentPosition);
      this.TimeRemaining = this.Duration.Subtract(this.CurrentPosition);
    }

    private void UpdateDuration()
    {
      long totalDurationChangeInTicks = this.Parts.Sum((audioFile) => audioFile.Duration.Ticks);
      this.Duration = TimeSpan.FromTicks(this.Duration.Ticks + totalDurationChangeInTicks);
    }

    private void UpdateDurationProperties(
      IEnumerable<IAudiobookPart> changedParts,
      NotifyCollectionChangedAction changeAction)
    {
      changedParts = changedParts.Where((part) => !part.IsNull).ToList();
      if (!changedParts.Any())
      {
        return;
      }

      TimeSpan totalDurationChange = TimeSpan.FromTicks(changedParts.Sum((audioFile) => audioFile.Duration.Ticks));
      if (changeAction == NotifyCollectionChangedAction.Remove)
      {
        totalDurationChange = totalDurationChange.Negate();
      }

      this.Duration = this.Duration.Add(totalDurationChange);
      this.TimeRemaining = this.Duration.Subtract(this.CurrentPosition);
    }

    private void HandlePartsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      switch (e.Action)
      {
        case NotifyCollectionChangedAction.Replace:
          OnPartsRemoved(e.OldItems.Cast<IAudiobookPart>().ToList());
          OnPartsAdded(e.NewItems.Cast<IAudiobookPart>().ToList());
          break;

        case NotifyCollectionChangedAction.Add:
          OnPartsAdded(e.NewItems.Cast<IAudiobookPart>().ToList());
          break;

        case NotifyCollectionChangedAction.Remove:
          OnPartsRemoved(e.OldItems.Cast<IAudiobookPart>().ToList());
          break;
      }
    }

    private void OnPartsAdded(List<IAudiobookPart> addedAudiobookParts)
    {
      UpdateDurationProperties(addedAudiobookParts, NotifyCollectionChangedAction.Add);
      //ListenToAudiobookPartOnPartAdded(addedAudiobookParts);
    }

    private void OnPartsRemoved(IList<IAudiobookPart> removedAudiobookParts)
    {
      UpdateDurationProperties(removedAudiobookParts, NotifyCollectionChangedAction.Remove);
      //StopListenToAudiobookPartOnPartRemoved(removedAudiobookParts);
    }

    private void ListenToAudiobookPartOnPartAdded(IEnumerable<IAudiobookPart> newParts)
    {
      newParts.ToList().ForEach((audioFile) => audioFile.Completed += HandleContinuousPlay);
    }

    private void StopListenToAudiobookPartOnPartRemoved(IEnumerable<IAudiobookPart> audioFiles)
    {
      audioFiles.ToList().ForEach((audioFile) => audioFile.Completed -= HandleContinuousPlay);
    }

    private void HandleContinuousPlay(object sender, EventArgs eventArgs)
    {
      var completedAudioFile = sender as IAudiobookPart;
      //if (!this.Parts.Contains(completedAudioFile))
      //{
      //  completedAudioFile.Completed -= HandleContinuousPlay;
      //  OnPartCompleted(
      //    new PartCompletedEventArgs<MusicProperties>(
      //      completedAudioFile,
      //      Audiobook.NullObject,
      //      PlaylistNavigationInfo.Completed));
      //  return;
      //}

      //if (this.NavigationInfo == PlaylistNavigationInfo.Current)
      //{
      //  OnPartCompleted(
      //    new PartCompletedEventArgs<MusicProperties>(
      //      completedAudioFile,
      //      completedAudioFile,
      //      this.IsContinuousPlayEnabled ? PlaylistNavigationInfo.Current : PlaylistNavigationInfo.Completed));
      //  return;
      //}

      if (this.isContinuousPlayEnabled && TryMoveToNextPart(out IAudiobookPart nextPart))
      {
        OnPartCompleted(
          new PartCompletedEventArgs<MusicProperties>(completedAudioFile, nextPart, PlaylistNavigationInfo.Next));
      }
      else
      {
        this.IsCompleted = true;
      }
    }

    private void OnPartCompleted(PartCompletedEventArgs<MusicProperties> completedPartArgs)
    {
      this.PartCompleted?.Invoke(this, completedPartArgs);
    }

    private static void OnCreated(IAudiobook sender)
    {
      Audiobook.Created?.Invoke(sender, EventArgs.Empty);
    }

    private static int createdPartCount = 0;

    private static void OnPartCreated(IAudiobook sender, IAudiobookPart audiobookPart)
    {
      Audiobook.PartCreated?.Invoke(sender, new ValueChangedEventArgs<IAudiobookPart>(audiobookPart));
      ++Audiobook.createdPartCount;
      if (Audiobook.createdPartCount.Equals(sender.PartCount))
      {
        Audiobook.OnCreated(sender);
        Audiobook.createdPartCount = 0;
      }
    }


    public void SetToIsCreating()
    {
      this.IsCreated = false;
      this.IsCreating = true;
    }

    public void SetToIsCreated()
    {
      this.IsCreating = false;
      this.IsCreated = true;
    }

    public bool TryAddBookmark(IBookmark bookmark)
    {
      if (bookmark.Position < TimeSpan.Zero || bookmark.Position > this.Duration)
      {
        return false;
      }

      this.Bookmarks.Add(bookmark);
      return true;
    }

    public void RemoveBookmark(IBookmark bookmark)
    {
      this.Bookmarks.Remove(bookmark);
    }

    private void UpdateCurrentPosition(object sender, object e)
    {
      this.UpdateCurrentPositionCallback();
      OnCurrentPositionChanged();
    }

    private void StopProgressTimer()
    {
      if (!this.IsProgressTimerStarted)
      {
        return;
      }

      this.IsProgressTimerStarted = false;
      this.ProgressReportTimer.Stop();
    }

    private void StartProgressTimer()
    {
      if (!this.IsProgressTimerInitialized)
      {
        InitializeProgressTimer();
      }

      if (this.IsProgressTimerStarted)
      {
        return;
      }

      this.IsProgressTimerStarted = true;
      this.ProgressReportTimer.Start();
    }

    private void InitializeProgressTimer()
    {
      if (this.IsProgressTimerInitialized)
      {
        return;
      }

      this.ProgressReportTimer = new DispatcherTimer() {Interval = TimeSpan.FromMilliseconds(500)};
      this.ProgressReportTimer.Tick += UpdateCurrentPosition;
      this.IsProgressTimerInitialized = true;
    }

    [NotifyPropertyChangedInvocator]
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    private void OnCurrentPositionChanged()
    {
      this.CurrentPositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCompleted()
    {
      this.Completed?.Invoke(this, EventArgs.Empty);
    }

    private void OnStarted()
    {
      this.Started?.Invoke(this, EventArgs.Empty);
    }

    private void OnStopped()
    {
      this.Stopped?.Invoke(this, EventArgs.Empty);
    }

    private void OnPaused()
    {
      this.Paused?.Invoke(this, EventArgs.Empty);
    }


    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler Completed;
    public event EventHandler Started;
    public event EventHandler Stopped;
    public event EventHandler Paused;
    public event EventHandler CurrentPositionChanged;

    [DataMember]
    public string FileSystemPath { get; private set; }

    [DataMember]
    public string FileName { get; private set; }

    [DataMember] private string fileSystemPathToken;

    public string FileSystemPathToken
    {
      get => this.fileSystemPathToken;
      set
      {
        if (value == this.fileSystemPathToken)
        {
          return;
        }

        this.fileSystemPathToken = value;
        OnPropertyChanged();
        if (this.Tag != null)
        {
          this.Tag.MediaFileSystemToken = this.FileSystemPathToken;
        }
      }
    }

    private bool IsProgressTimerStarted { get; set; }
    private bool IsProgressTimerInitialized { get; set; }
    private DispatcherTimer ProgressReportTimer { get; set; }

    public object SyncLock { get; }

    private Action updateCurrentPositionCallback;
    public Action UpdateCurrentPositionCallback
    {
      get => this.updateCurrentPositionCallback;
      set
      {
        if (object.Equals(value, this.updateCurrentPositionCallback))
        {
          return;
        }

        this.updateCurrentPositionCallback = value;
        OnPropertyChanged();
      }
    }


    public bool HasProgress { get => this.CurrentPosition > TimeSpan.Zero; }


    [DataMember] private bool isLoopEnabled;
    public bool IsLoopEnabled
    {
      get => this.isLoopEnabled;
      set
      {
        if (value == this.isLoopEnabled)
        {
          return;
        }

        this.isLoopEnabled = value;
        this.CurrentPart.IsLoopEnabled = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private int? loopCount;

    public int? LoopCount
    {
      get => this.loopCount;
      set
      {
        if (value == this.loopCount)
        {
          return;
        }

        this.loopCount = value;
        this.CurrentPart.LoopCount = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private (TimeSpan BeginTime, TimeSpan EndTime) loopRange;

    public (TimeSpan BeginTime, TimeSpan EndTime) LoopRange
    {
      get => this.loopRange;
      set
      {
        if (value.Equals(this.loopRange))
        {
          return;
        }

        this.loopRange = value;
        this.CurrentPart.LoopRange = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    public bool IsNull { get; private set; }


    [DataMember] private double speedMultiplier;

    public double SpeedMultiplier
    {
      get => this.speedMultiplier;
      set
      {
        if (value.Equals(this.speedMultiplier))
        {
          return;
        }

        this.speedMultiplier = value;
        this.CurrentPart.SpeedMultiplier = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private bool isSelected;

    public bool IsSelected
    {
      get => this.isSelected;
      set
      {
        this.isSelected = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private bool isVisible;

    public bool IsVisible
    {
      get => this.isVisible;
      set
      {
        this.isVisible = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private IMediaTag<MusicProperties> tag;

    public IMediaTag<MusicProperties> Tag
    {
      get => this.tag;
      set
      {
        this.tag = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private ObservableCollection<IBookmark> bookmarks;

    public ObservableCollection<IBookmark> Bookmarks
    {
      get => this.bookmarks;
      set
      {
        if (Equals(value, this.bookmarks))
        {
          return;
        }

        this.bookmarks = value;
        OnPropertyChanged();
      }
    }

    public IBookmark this[int bookmarkIndex]
    {
      get => this.Bookmarks[bookmarkIndex];
      set => this.Bookmarks[bookmarkIndex] = value;
    }

    [DataMember] private TimeSpan currentPosition;

    public TimeSpan CurrentPosition
    {
      get => this.currentPosition;
      set
      {
        //if (value.Equals(this.currentPosition)) return;
        this.currentPosition = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private TimeSpan timeRemaining;

    public TimeSpan TimeRemaining
    {
      get => this.timeRemaining;
      set
      {
        if (value.Equals(this.timeRemaining))
        {
          return;
        }

        this.timeRemaining = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private TimeSpan duration;

    public TimeSpan Duration
    {
      get => this.duration;
      set
      {
        if (value.Equals(this.duration))
        {
          return;
        }

        this.duration = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private PlaylistNavigationInfo navigationInfo;

    public PlaylistNavigationInfo NavigationInfo
    {
      get => this.navigationInfo;
      set
      {
        this.navigationInfo = value;
        OnPropertyChanged();
      }
    }

    private bool isCreating;

    public bool IsCreating
    {
      get => this.isCreating;
      private set
      {
        this.isCreating = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private bool isCreated;

    public bool IsCreated
    {
      get => this.isCreated;
      private set
      {
        this.isCreated = value;
        OnPropertyChanged();
      }
    }


    #region IDisposable

    private void ReleaseUnmanagedResources()
    {
    }

    private void Dispose(bool disposing)
    {
      ReleaseUnmanagedResources();
      if (disposing)
      {
        this.UpdateCurrentPositionCallback = null;
      }
    }

    /// <inheritdoc />
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    ~Audiobook()
    {
      Dispose(false);
    }

    #endregion


    public event EventHandler<PartCompletedEventArgs<MusicProperties>> PartCompleted;

    [DataMember]
    private TimeSpan CurrentPositionOffset { get; set; }

    //[DataMember]
    //private Dictionary<Bookmark, int> BookmarkAudiobookMap { get; set; }

    [DataMember] private ObservableCollection<IAudiobookPart> parts;

    public ObservableCollection<IAudiobookPart> Parts
    {
      get => this.parts;
      set
      {
        if (this.Parts != null)
        {
          this.Parts.CollectionChanged -= HandlePartsChanged;
          StopListenToAudiobookPartOnPartRemoved(this.Parts.OfType<IAudiobookPart>());
        }

        this.parts = value;
        OnPropertyChanged();
        {
          if (this.Parts != null)
          {
            if (this.Parts.Any())
            {
              //ListenToAudiobookPartOnPartAdded(this.Parts);
            }

            this.Parts.CollectionChanged += HandlePartsChanged;
          }
        }
      }
    }

    private IAudiobookPart currentPart;

    public IAudiobookPart CurrentPart
    {
      get => this.currentPart;
      set
      {
        if (ReferenceEquals(this.CurrentPart, value))
        {
          return;
        }
        IAudiobookPart oldValue = this.CurrentPart;
        this.currentPart = value;
        OnPropertyChanged();
        HandleCurrentPartChanged(oldValue);
      }
    }

    [DataMember] private int currentPartIndex;

    public int CurrentPartIndex
    {
      get => this.currentPartIndex;
      private set
      {
        this.currentPartIndex = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    protected double oldVolume;

    [DataMember] private double volume;
    public double Volume
    {
      get => this.volume;
      set
      {
        if (Equals(value, this.volume))
        {
          return;
        }

        this.oldVolume = this.volume;
        this.volume = value;

        this.CurrentPart.Volume = value;
        OnPropertyChanged();
        this.IsMuted = this.Volume.Equals(0d);
      }
    }

    [DataMember] private bool isMuted;

    public bool IsMuted
    {
      get => this.isMuted;
      set
      {
        if (Equals(value, this.isMuted))
        {
          return;
        }

        this.isMuted = value;
        this.CurrentPart.IsMuted = value;
        OnPropertyChanged();
        if (this.IsMuted && this.Volume > 0d)
        {
          this.Volume = 0d;
        }
        else if (!this.IsMuted && this.Volume.Equals(0d))
        {
          this.Volume = this.oldVolume;
        }
      }
    }

    [DataMember] private bool isExpanded;

    public bool IsExpanded
    {
      get => this.isExpanded;
      set
      {
        this.isExpanded = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private bool isContinuousPlayEnabled;

    public bool IsContinuousPlayEnabled
    {
      get => this.isContinuousPlayEnabled;
      set
      {
        this.isContinuousPlayEnabled = value;
        OnPropertyChanged();
      }
    }

    [DataMember] private bool isPaused;

    public bool IsPaused
    {
      get => this.isPaused;
      set
      {
        if (value == this.IsPaused)
        {
          return;
        }

        this.isPaused = value;
        OnPropertyChanged();

        if (this.IsPaused)
        {
          OnPaused();
        }

        this.IsPlaying &= !this.IsPaused;
        this.IsStopped = !this.IsPaused && !this.IsPlaying;
      }
    }

    [DataMember] private bool isStopped;

    public bool IsStopped
    {
      get => this.isStopped;
      set
      {
        if (value == this.IsStopped)
        {
          return;
        }

        this.isStopped = value;
        OnPropertyChanged();
        if (this.IsStopped)
        {
          this.CurrentPosition = TimeSpan.Zero;
          this.TimeRemaining = this.Duration;
          SetCurrentPart(this.Parts.FirstOrDefault());
          OnStopped();
        }

        this.IsPlaying &= !this.IsStopped;
        this.IsPaused &= !this.IsStopped;
      }
    }

    [DataMember] private bool isPlaying;

    public bool IsPlaying
    {
      get => this.isPlaying;
      set
      {
        if (value == this.IsPlaying)
        {
          return;
        }

        this.isPlaying = value;
        OnPropertyChanged();
        if (this.IsPlaying)
        {
          OnStarted();
        }

        this.IsPaused &= !this.IsPlaying;
        this.IsStopped &= !this.IsPlaying && !this.IsPaused;
        //this.IsCompleted &= !this.IsPlaying;
      }
    }

    [DataMember] private bool isCompleted;

    public bool IsCompleted
    {
      get => this.isCompleted;
      set
      {
        if (value == this.isCompleted)
        {
          return;
        }

        this.isCompleted = value;
        OnPropertyChanged();
        if (this.IsCompleted)
        {
          OnCompleted();
        }

        this.IsPaused &= !this.IsCompleted;
        this.IsStopped &= !this.IsCompleted;
        this.IsPlaying &= !this.IsCompleted;
      }
    }

    [DataMember] private int partCount;

    public int PartCount
    {
      get => this.partCount;
      set
      {
        this.partCount = value;
        OnPropertyChanged();
      }
    }
  }
}