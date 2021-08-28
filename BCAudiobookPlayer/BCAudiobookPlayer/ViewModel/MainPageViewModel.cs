using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;
using BCAudiobookPlayer.Player;
using BCAudiobookPlayer.Player.Playback;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.ResourceProvider;
using MetaBrainz.MusicBrainz;

namespace BCAudiobookPlayer.ViewModel
{
  public interface IPageViewModel : IViewModel
  {
    Task RestorePersistentState();
  }

  public class MainPageViewModel : ViewModel, IPageViewModel, INotifyPropertyChanged
  {
    private readonly TimeSpan SleepTimerInterval = TimeSpan.FromSeconds(1d);

    public MainPageViewModel()
    {
      this.pendingAudiobookParts = new Dictionary<IAudiobook, IEnumerable<IAudiobookPart>>();
      this.LastPlayedFiles = new ObservableCollection<IAudiobookPart>();
      //SetLastPlayedPart(AudioPart.NullObject);
      this.Bookmarks = new Dictionary<IBookmark, IAudiobookPart>();
      this.Playlist = new ObservableCollection<IAudiobookPart>();
      this.AudioPlaybackController = new AudioPlaybackController();
      this.AudioPlaybackController.AudioPartStarted += HandleOnAudioPartStarted;
      this.AudioPlaybackController.AudioPartStopped += HandleOnAudioPartStopped;
      this.AudioPlaybackController.AudioPartPaused += HandleOnAudioPartPaused;
      this.SleepTimer = new DispatcherTimer();
      this.SleepTimer.Interval = this.SleepTimerInterval;
      this.SleepTimer.Tick += PausePlaylistOnSleepTimerIntervalElapsed;
      this.SleepTimer.Tick += UpdateSleepTimerProgressOnSleepTimerIntervalElapsed;
      //this.IsSleepTimerAutoStartEnabled = false;
      this.IsSleepTimerEnabled = this.IsSleepTimerAutoStartEnabled;
      this.IsSleepTimerInCountdownMode = true;
      this.SleepTime = TimeSpan.FromMinutes(45);
    }

    public void StartSleepTimer()
    {
      this.RemainingTimeUntilSleep = this.IsSleepTimerInCountdownMode
        ? this.SleepTime.Negate()
        : this.SleepTime.Subtract(DateTime.Now.TimeOfDay).Negate();

      // When there are no playing pendingParts, the sleep timer wil be started as soon as one is played by invoking the Play() member
      if (this.AudioPlaybackController.HasPlayingAudioParts)
      {
        this.SleepTimer.Start();
      }
      else
      {
        this.IsDeferredSleepTimerStartEnabled = true;
      }

      this.IsSleepTimerEnabled = true;
    }

    public void StopSleepTimer()
    {
      this.SleepTimer.Stop();
      //this.IsSleepTimerEnabled = this.IsSleepTimerAutoStartEnabled;
    }

    public void PauseSleepTimer()
    {
      this.SleepTimer.Stop();
    }

    public void ResumeSleepTimer()
    {
      this.SleepTimer.Start();
    }

    public async Task RestorePersistentState()
    {
      this.InitializationCancellationTokenSource = new CancellationTokenSource();
      SetIsCreating();
      this.AudioPlaybackController.Playlist = await DataStorageController
        .PersistentDataController.LoadPlaylistAsync();
      ObservableCollection<IAudiobookPart> recentPlaylist = new ObservableCollection<IAudiobookPart>(this.AudioPlaybackController.Playlist.Files);
      if (recentPlaylist.Any())
      {
        await InitializeFromRecentPlaylistAsync(recentPlaylist);
      }

      ClearIsCreating();
    }

    private async Task InitializeFromRecentPlaylistAsync(ObservableCollection<IAudiobookPart> recentPlaylist)
    {
      await Task.Factory.StartNew(
        async () =>
        {
          foreach (IAudiobookPart audiobookPart in recentPlaylist.ToList())
          {
            this.Playlist.Add(audiobookPart);
            if (audiobookPart is IHttpMediaStream)
            {
              continue;
            }
            audiobookPart.SetToIsCreating();
            await PreloadAudiobookParts(audiobookPart);
          }

          try
          {
            CompleteAudiobookPartsCancellable();
          }
          catch (OperationCanceledException)
          {
            this.InitializationCancellationTokenSource?.Dispose();
            this.InitializationCancellationTokenSource = new CancellationTokenSource();
            CompleteAudiobookPartsCancellable(true);
          }
        },
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void CompleteAudiobookPartsCancellable(bool isHighPriorityJob = false)
    {
      if (isHighPriorityJob)
      {
        CompleteHighPriorityAudiobookFirst();
        this.pendingAudiobookParts.Remove(this.HighPriorityAudiobook);
        this.HighPriorityAudiobook = Audiobook.NullObject;
      }

      CompletePendingAudiobookParts();
    }

    private void CompleteHighPriorityAudiobookFirst()
    {
      if (this.pendingAudiobookParts.TryGetValue(this.HighPriorityAudiobook, out IEnumerable<IAudiobookPart> pendingPartsOfHighPriorityAudiobook))
      {
        List<IAudiobookPart> nextPartsInPlaybackQueue = this.HighPriorityAudiobook.Parts
          .Skip(this.HighPriorityAudiobook.CurrentPartIndex + 1).ToList();

        List<IAudiobookPart> previousPartsInPlaybackQueue =
          this.HighPriorityAudiobook.Parts.Take(this.HighPriorityAudiobook.CurrentPartIndex).ToList();

        CreatePendingAudiobookParts(this.HighPriorityAudiobook, nextPartsInPlaybackQueue);
        CreatePendingAudiobookParts(this.HighPriorityAudiobook, previousPartsInPlaybackQueue);
      }
    }

    private void UpdateBookmarks(IAudiobookPart audiobookPart)
    {
      audiobookPart.Bookmarks.ToList().ForEach(
        (bookmark) =>
        {
          bookmark.CoverArt =
            audiobookPart is IAudiobook audiobook && audiobook.TryGetPartAt(
              bookmark.AudioPartIndex,
              out IAudiobookPart bookmarkedPart)
              ? bookmarkedPart.Tag.CoverArt
              : audiobookPart.Tag.CoverArt;
          this.Bookmarks.Add(bookmark, audiobookPart);
        });
    }
    
    // E.g. event handlers or tag info is not serialized
    private async Task PreloadAudiobookParts(IAudiobookPart audiobookPart)
    {
      if (audiobookPart is IAudiobook audiobook && audiobook.Parts.Any())
      {
        audiobook.SoftReset();
        List<IAudiobookPart> audiobookParts = audiobook.Parts.ToList();
        await SetupLastPlayedPartAsync(audiobook, audiobookParts);
        audiobookParts.RemoveAt(audiobook.CurrentPartIndex);
        //InitializeAudiobookTagAfterRestore(audiobook);
        this.pendingAudiobookParts.Add(audiobook, audiobookParts);
        await UpdateAudiobookPart(audiobook.CurrentPart, true);
        return;
      }

      await UpdateAudiobookPart(audiobookPart);
      OnAudioPartCreated(audiobookPart);
      audiobookPart.SetToIsCreated();
    }

    // E.g. event handlers or tag info is not serialized
    private void CompletePendingAudiobookParts()
    {
      foreach (KeyValuePair<IAudiobook, IEnumerable<IAudiobookPart>> entry in this.pendingAudiobookParts)
      {
        this.InitializationCancellationTokenSource.Token.ThrowIfCancellationRequested();
        CreatePendingAudiobookParts(entry.Key, entry.Value.ToList());
      }
    }

    private void CreatePendingAudiobookParts(IAudiobook audiobookWithPendingParts, List<IAudiobookPart> pendingParts)
    {
      //audiobookWithPendingParts.SetToIsCreating();
      Parallel.ForEach(
        pendingParts, new ParallelOptions() { CancellationToken = CancellationToken.None, TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext()},
        async (audiobookPart) =>
        {
          audiobookPart.SetToIsCreating();
          this.InitializationCancellationTokenSource.Token.ThrowIfCancellationRequested();
          await UpdateAudiobookPart(audiobookPart, true);
          ((List<IAudiobookPart>) this.pendingAudiobookParts[audiobookWithPendingParts]).Remove(audiobookPart);
          audiobookPart.SoftReset();
          audiobookPart.SetToIsCreated();
        });

      audiobookWithPendingParts.Update();
      UpdateBookmarks(audiobookWithPendingParts);
      audiobookWithPendingParts.SoftReset();
      audiobookWithPendingParts.SetToIsCreated();
      OnAudioPartCreated(audiobookWithPendingParts);
    }

    // E.g. event handlers or tag info is not serialized
    private async Task UpdateAudiobookPart(IAudiobookPart audiobookPart, bool isPartOfAudiobook = false)
    {
      StorageFile file = isPartOfAudiobook 
        ? await GetFileFromFolder(audiobookPart.FileSystemPathToken, audiobookPart.FileName) 
        : await StorageApplicationPermissions.FutureAccessList.GetFileAsync(
          audiobookPart.FileSystemPathToken,
          AccessCacheOptions.FastLocationsOnly);

      //audiobookPart.Tag.FileProperties = file.Properties;
      var imageSource = new BitmapImage();
      await imageSource.SetSourceAsync(
        await file.GetThumbnailAsync(
          ThumbnailMode.MusicView,
          256,
          ThumbnailOptions.UseCurrentScale));
      audiobookPart.Tag.CoverArt = imageSource;

      if (!isPartOfAudiobook)
      {
        UpdateBookmarks(audiobookPart);
      }
    }

    private async Task<StorageFile> GetFileFromFolder(string audiobookFileSystemPathToken, string fileName)
    {
      StorageFolder storageFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(
        audiobookFileSystemPathToken,
        AccessCacheOptions.FastLocationsOnly);
      return await storageFolder.GetFileAsync(fileName);
    }

    private void InitializeAudiobookTagAfterRestore(IAudiobook audiobook)
    {
      audiobook.Tag = audiobook.CurrentPart.Tag;
    }

    private async Task SetupLastPlayedPartAsync(
      IAudiobook audiobook,
      List<IAudiobookPart> audiobookParts)
    {
      if (audiobook.CurrentPartIndex > -1 && audiobook.CurrentPartIndex < audiobook.PartCount)
      {
        IAudiobookPart startingPart = audiobook.Parts[audiobook.CurrentPartIndex];
        await UpdateAudiobookPart(startingPart, true);
        audiobook.TryMoveToPart(startingPart);
        startingPart.SoftReset();
        SetLastPlayedPart(audiobook);
      }
    }

    public async Task<(bool IsSuccessful, IAudiobookPart AudiobookPart)> AddFileToPlaylist(StorageFile rawAudioFile)
    {
      SetIsCreating();
      (bool IsSuccessful, IAudiobookPart AudiobookPart) factoryResult =
        await this.AudioPlaybackController.TryCreateAudioFileAsync(rawAudioFile);
      if (factoryResult.IsSuccessful)
      {
        factoryResult.AudiobookPart.FileSystemPathToken =
          ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap[factoryResult.AudiobookPart
            .FileSystemPath];

        this.Playlist.Add(factoryResult.AudiobookPart);

        if (this.LastPlayedFile?.IsNull ?? true)
        {
          SetLastPlayedPart(factoryResult.AudiobookPart);
        }
      }

      OnAudioPartCreated(factoryResult.AudiobookPart);
      ClearIsCreating();
      return factoryResult;
    }

    public async Task<(bool IsSuccessful, IAudiobookPart AudiobookPart)> AddFolderToPlayList(
      StorageFolder rawFolder,
      IEnumerable<StorageFile> rawFolderContent)
    {
      SetIsCreating();
      //Audiobook.PartCreated += SetAudiobookFileSystemToken;
      Audiobook.Created += HandleAudiobookCreationCompleted;

      (bool IsSuccessful, IAudiobook Audiobook) factoryResult =
        await this.AudioPlaybackController.TryCreateAudioBookAsync(rawFolder, rawFolderContent);
      if (factoryResult.IsSuccessful)
      {
        factoryResult.Audiobook.FileSystemPathToken = ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap[factoryResult.Audiobook.FileSystemPath];

        this.Playlist.Add(factoryResult.Audiobook);

        if (this.LastPlayedFile?.IsNull ?? true)
        {
          SetLastPlayedPart(factoryResult.Audiobook);
        }
      }

      return factoryResult;
    }

    public void RemovePartFromPlaylist(IAudiobookPart part)
    {
      this.Playlist.Remove(part);
    }

    private void HandleAudiobookCreationCompleted(object sender, EventArgs e)
    {
      //Audiobook.PartCreated -= SetAudiobookFileSystemToken;
      Audiobook.Created -= HandleAudiobookCreationCompleted;
      ClearIsCreating();
      OnAudioPartCreated((IAudiobookPart) sender);
    }

    public async Task<(bool IsSuccessful, IAudiobookPart AudiobookPart)> AddHttpStreamToPlayListAsync(string url)
    {
      (bool IsSuccessful, IAudiobookPart AudiobookPart) factoryResult =
        await this.AudioPlaybackController.TryCreateHttpMediaStreamAsync(url, "New HTTP Stream");
      if (!factoryResult.IsSuccessful)
      {
        return factoryResult;
      }

      this.Playlist.Add(factoryResult.AudiobookPart);
      if (this.LastPlayedFile.IsNull)
      {
        SetLastPlayedPart(factoryResult.AudiobookPart);
      }

      return factoryResult;
    }

    public void AddBookmark(IAudiobookPart targetPart)
    {
      IBookmark bookmark = new Bookmark(targetPart);
      this.Bookmarks.Add(bookmark, targetPart);
      targetPart.TryAddBookmark(bookmark);
    }

    public void RemoveBookmark(IBookmark bookmarkToRemove)
    {
      if (this.Bookmarks.Remove(bookmarkToRemove, out IAudiobookPart targetPart))
      {
        targetPart.RemoveBookmark(bookmarkToRemove);
      }
    }

    private void Play(IAudiobookPart audioPart)
    {
      HandleSleepTimer();
      this.AudioPlaybackController.Play(audioPart);
      if (audioPart is IAudiobook audiobook && !audiobook.IsCreated && !audiobook.IsCreating)
      {
        this.HighPriorityAudiobook = audiobook;
        this.InitializationCancellationTokenSource.Cancel();
      }
    }

    private void HandleSleepTimer()
    {
      if (this.IsSleepTimerEnabled && !this.SleepTimer.IsEnabled &&
          (this.IsSleepTimerAutoStartEnabled || this.IsDeferredSleepTimerStartEnabled || this.IsSleepTimerPaused))
      {
        this.IsDeferredSleepTimerStartEnabled = false;
        if (this.IsSleepTimerPaused)
        {
          this.IsSleepTimerPaused = false;
          ResumeSleepTimer();
          return;
        }

        StartSleepTimer();
      }
    }

    private void Stop(IAudiobookPart audioPart)
    {
      this.AudioPlaybackController.Stop(audioPart);
      if (this.SleepTimer.IsEnabled)
      {
        PauseSleepTimer();
        this.IsSleepTimerPaused = true;
      }
    }

    private void JumpToPosition(IAudiobook audiobook, IAudiobookPart audiobookPart)
    {
      HandleSleepTimer();
      this.AudioPlaybackController.JumpToAudiobookPartAsync(audiobook, audiobookPart);
    }

    private void Pause(IAudiobookPart audioPart)
    {
      this.AudioPlaybackController.Pause(audioPart);
      if (this.SleepTimer.IsEnabled)
      {
        PauseSleepTimer();
        this.IsSleepTimerPaused = true;
      }
    }

    private void Resume(IAudiobookPart audioPart)
    {
      this.AudioPlaybackController.Resume(audioPart);
      if (this.SleepTimer.IsEnabled)
      {
        ResumeSleepTimer();
        this.IsSleepTimerPaused = false;
      }
    }

    private async Task PlayBookmarkAsync(IBookmark bookmark)
    {
      if (!bookmark.IsNull && this.Bookmarks.TryGetValue(bookmark, out IAudiobookPart audiobookPart))
      {
        HandleSleepTimer();
        await this.AudioPlaybackController.PlayBookmark(audiobookPart, bookmark);
      }
    }

    public async Task GenerateMediaTagsAsync(IAudiobookPart audiobookPart)
    {
      if (audiobookPart is IAudiobook audiobook)
      {
        await GenerateAudiobookTags(audiobook);
        return;
      }

      await GenerateTag(audiobookPart);
    }

    private async Task GenerateTag(IAudiobookPart audiobookPart, int audiobookPartIndex = 0)
    {
      //StorageFile underlyingFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(audiobookPart.FileSystemPathToken);

      //audiobookPart.Tag.Title = underlyingFile.DisplayName;
      //audiobookPart.Tag.TrackNumber = (uint) (audiobookPartIndex + 1);
      //Uri requestUri = new Uri("https://musicbrainz.org/ws/2/recording?query=%22thriller%22");
      Uri requestUri = new Uri("https://musicbrainz.org/ws/2/", UriKind.Absolute);
      var query = new MetaBrainz.MusicBrainz.Query(@"BCAudiobookPlayer");
      var recordings = await query.FindRecordingsAsync("%22Bad%22&fmt=json");
      var lures = await query.LookupRecordingAsync(recordings.Results.FirstOrDefault().MbId, Include.Artists | Include.Tags | Include.Releases);
      var httpClient = new HttpClient();
      //HttpRequestHeaderCollection headers = httpClient.DefaultRequestHeaders;

      //HttpResponseMessage response = await httpClient.GetAsync(requestUri);
      //response.EnsureSuccessStatusCode();
      //string content = await response.Content.ReadAsStringAsync();
    }

    private async Task GenerateAudiobookTags(IAudiobookPart audiobookPart)
    {
      await GenerateTag(audiobookPart);
    }

    private void SetIsCreating()
    {
      ++this.creatingNewPlaylistItemCount;
      this.IsCreatingPlaylistItem = this.creatingNewPlaylistItemCount > 0;
    }

    private void ClearIsCreating()
    {
      --this.creatingNewPlaylistItemCount;
      this.IsCreatingPlaylistItem = !this.creatingNewPlaylistItemCount.Equals(0);
    }

    private void HandleOnAudioPartStarted(object sender, ValueChangedEventArgs<IAudiobookPart> e)
    {
      IAudiobookPart audioPart = e.Value;
      SetLastPlayedPart(audioPart);
    }

    private void SetLastPlayedPart(IAudiobookPart audioPart)
    {
      this.LastPlayedFile = audioPart;
     
      if (this.LastPlayedFiles.Contains(audioPart))
      {
        this.LastPlayedFiles.Move(this.LastPlayedFiles.IndexOf(audioPart), 0);
      }
      else
      {
        this.LastPlayedFiles.Insert(0, audioPart);
      }
    }

    private void HandleOnAudioPartStopped(object sender, ValueChangedEventArgs<IAudiobookPart> e)
    {
      if (this.LastPlayedFiles.Count.Equals(1) || !this.LastPlayedFiles.Contains(e.Value))
      {
        return;
      }

      if (this.LastPlayedFiles.Count > 1)
      {
        IAudiobookPart audioPart = e.Value;
        this.LastPlayedFiles.Remove(audioPart);
        SetLastPlayedPart(this.LastPlayedFiles.FirstOrDefault());
      }
    }

    private void HandleOnAudioPartPaused(object sender, ValueChangedEventArgs<IAudiobookPart> e)
    {
      IAudiobookPart audioPart = e.Value;
      if (!this.LastPlayedFiles.Contains(audioPart))
      {
        return;
      }

      ////// Move to end
      ////this.LastPlayedFiles.Move(this.LastPlayedFiles.IndexOf(audioPart), this.LastPlayedFiles.Count - 1);
      ////if (!this.LastPlayedFile.Equals(audioPart))
      ////{
      ////  SetLastPlayedPart(audioPart);
      ////}
    }

    private void SynchronizePlaylistWithPlaybackControllerOnPlaylistChanged(
      object sender,
      NotifyCollectionChangedEventArgs e)
    {
      int insertionIndex = e.NewStartingIndex;
      switch (e.Action)
      {
        case NotifyCollectionChangedAction.Replace:
          e.NewItems
            .OfType<IAudiobookPart>()
            .ToList()
            .ForEach(
              (audioFile) => { this.AudioPlaybackController.InsertIntoPlaylist(audioFile, insertionIndex++); });
          e.OldItems.OfType<IAudiobookPart>()
            .ToList()
            .ForEach((audioFile) => this.AudioPlaybackController.RemoveFromPlaylist(audioFile));
          break;

        case NotifyCollectionChangedAction.Add:
          if (e.NewStartingIndex > -1)
          {
            e.NewItems
              .OfType<IAudiobookPart>()
              .ToList()
              .ForEach(this.AudioPlaybackController.AddToPlaylist);
          }
          else
          {
            e.NewItems
              .OfType<IAudiobookPart>()
              .ToList()
              .ForEach(
                (audioFile) => { this.AudioPlaybackController.InsertIntoPlaylist(audioFile, insertionIndex++); });
          }

          break;

        case NotifyCollectionChangedAction.Remove:
          e.OldItems.OfType<IAudiobookPart>()
            .ToList()
            .ForEach((audioFile) => this.AudioPlaybackController.RemoveFromPlaylist(audioFile));
          break;
      }

      DataStorageController.PersistentDataController.SavePlaylist(this.AudioPlaybackController.Playlist);
    }

    private bool CanExecutePlaybackCommands(IAudiobookPart audiobookPart)
    {
      var partToVerify = audiobookPart;
      if (audiobookPart is IAudiobook audiobook)
      {
        partToVerify = audiobook.CurrentPart;
      }
      //return (!partToVerify?.IsNull ?? false) && partToVerify.IsCreated;
      return !partToVerify?.IsNull ?? false;
    }

    private void OnAudioPartCreated(IAudiobookPart createdItem)
    {
      this.AudioPartCreated?.Invoke(this, new ValueChangedEventArgs<IAudiobookPart>(createdItem));
    }

    private void PausePlaylistOnSleepTimerIntervalElapsed(object sender, object e)
    {
      if (this.RemainingTimeUntilSleep >= TimeSpan.Zero && !this.IsSleepTimerStopRequested)
      {
        this.IsSleepTimerStopRequested = true;
        this.Playlist.ToList().ForEach(this.AudioPlaybackController.Pause);
      }
    }

    private void UpdateSleepTimerProgressOnSleepTimerIntervalElapsed(object sender, object e)
    {
      if (this.IsSleepTimerInCountdownMode)
      {
        this.RemainingTimeUntilSleep = this.RemainingTimeUntilSleep.Negate().Subtract(this.SleepTimerInterval).Negate();
      }
      else
      {
        this.RemainingTimeUntilSleep = this.SleepTime.Subtract(DateTime.Now.TimeOfDay).Negate();
      }

      if (this.IsSleepTimerStopRequested)
      {
        this.IsSleepTimerStopRequested = false;
        StopSleepTimer();
        this.RemainingTimeUntilSleep = TimeSpan.Zero;
      }
    }

    public event EventHandler<ValueChangedEventArgs<IAudiobookPart>> AudioPartCreated;

    public ICommand AddBookmarkCommand => new RelayCommand<IAudiobookPart>(AddBookmark, CanExecutePlaybackCommands);
    public ICommand RemoveBookmarkCommand => new RelayCommand<IBookmark>(RemoveBookmark,
      (bookmark) => !bookmark.IsNull);

    public ICommand PlayBookmarkCommand => new RelayCommand<IBookmark>(PlayBookmarkAsync, (param) => !param.IsNull);

    public ICommand PlayCommand => new RelayCommand<IAudiobookPart>(
      (audioPart) => Play(audioPart),
      CanExecutePlaybackCommands);

    public ICommand SkipForwardCommand => new RelayCommand<IAudiobookPart>(
      this.AudioPlaybackController.SkipForward,
      (param) => CanExecutePlaybackCommands(param) && (param is IAudiobook audiobook && audiobook.CurrentPartIndex < audiobook.PartCount - 1));

    public ICommand SkipBackCommand => new RelayCommand<IAudiobookPart>(
      this.AudioPlaybackController.SkipBack,
      (param) => CanExecutePlaybackCommands(param) && param is IAudiobook audiobook);

    public ICommand StopCommand => new RelayCommand<IAudiobookPart>(
      (audioFile) => Stop(audioFile),
      CanExecutePlaybackCommands);

    public ICommand PauseCommand => new RelayCommand<IAudiobookPart>(
      (audioFile) => Pause(audioFile),
      CanExecutePlaybackCommands);

    public ICommand ResumeCommand => new RelayCommand<IAudiobookPart>(
      (audioFile) => Resume(audioFile),
      CanExecutePlaybackCommands);

    public ICommand SkipToPositionCommand => new RelayCommand<(TimeSpan Position, IAudiobookPart AudiobookPart)>(
      (param) => this.AudioPlaybackController.JumpToPosition(param.AudiobookPart, param.Position),
      (param) => CanExecutePlaybackCommands(param.AudiobookPart));

    public ICommand SkipToChapterCommand =>
      new RelayCommand<(IAudiobook Audiobook, IAudiobookPart TargetAudiobookPart)>(
        (param) => JumpToPosition(param.Audiobook, param.TargetAudiobookPart),
        (param) => CanExecutePlaybackCommands(param.TargetAudiobookPart));

    public ICommand AddHttpMediaStreamCommand => new RelayCommand<string>(
      AddHttpStreamToPlayListAsync,
      (url) => !string.IsNullOrWhiteSpace(url));


    private ObservableCollection<IAudiobookPart> playlist;

    public ObservableCollection<IAudiobookPart> Playlist
    {
      get => this.playlist;
      set
      {
        if (this.Playlist != null)
        {
          this.Playlist.CollectionChanged -= SynchronizePlaylistWithPlaybackControllerOnPlaylistChanged;
        }

        if (value != null)
        {
          value.CollectionChanged += SynchronizePlaylistWithPlaybackControllerOnPlaylistChanged;
        }

        TrySetPropertyValue(ref this.playlist, value);
      }
    }

    private ObservableCollection<IAudiobookPart> lastPlayedFiles;

    public ObservableCollection<IAudiobookPart> LastPlayedFiles
    {
      get => this.lastPlayedFiles;
      private set => TrySetPropertyValue(ref this.lastPlayedFiles, value);
    }

    private IAudiobookPart lastPlayedFile;

    public IAudiobookPart LastPlayedFile
    {
      get => this.lastPlayedFile;
      private set => TrySetPropertyValue(ref this.lastPlayedFile, value, true);
    }

    private bool isPlaylistLoopEnabled;   
    public bool IsPlaylistLoopEnabled
    {
      get => this.AudioPlaybackController.IsPlaylistLoopEnabled;
      set
      {
        if (TrySetPropertyValue(ref this.isPlaylistLoopEnabled, value))
        {
          if (this.isPlaylistLoopEnabled)
          {
            this.AudioPlaybackController.EnablePlaylistLoop();
          }
          else
          {
            this.AudioPlaybackController.DisablePlaylistLoop();
          }
          DataStorageController.PersistentDataController.SavePlaylist(this.AudioPlaybackController.Playlist);
        }
      }
    }

    private Dictionary<IBookmark, IAudiobookPart> bookmarks;
    public Dictionary<IBookmark, IAudiobookPart> Bookmarks
    {
      get => this.bookmarks;
      set => TrySetPropertyValue(ref this.bookmarks, value);
    }

    private int creatingNewPlaylistItemCount = 0;
    private bool isCreatingPlaylistItem;

    public bool IsCreatingPlaylistItem
    {
      get => this.isCreatingPlaylistItem;
      set => TrySetPropertyValue(ref this.isCreatingPlaylistItem, value);
    }

    private TimeSpan sleepTime;

    public TimeSpan SleepTime
    {
      get => this.sleepTime;
      set
      {
        if (TrySetPropertyValue(ref this.sleepTime, value > TimeSpan.Zero ? value : TimeSpan.Zero))
        {
          if (this.IsSleepTimerEnabled)
          {
            StopSleepTimer();
            StartSleepTimer();
          }
        }
      }
    }

    private TimeSpan remainingTimeUntilSleep;

    public TimeSpan RemainingTimeUntilSleep
    {
      get => this.remainingTimeUntilSleep;
      set => TrySetPropertyValue(ref this.remainingTimeUntilSleep, value);
    }


    private bool isSleepTimerInCountdownMode;

    public bool IsSleepTimerInCountdownMode
    {
      get => this.isSleepTimerInCountdownMode;
      set
      {
        if (TrySetPropertyValue(ref this.isSleepTimerInCountdownMode, value))
        {
          if (this.IsSleepTimerEnabled)
          {
            StopSleepTimer();
            StartSleepTimer();
          }
        }
      }
    }

    private bool isSleepTimerAutoStartEnabled;

    public bool IsSleepTimerAutoStartEnabled
    {
      get => this.isSleepTimerAutoStartEnabled;
      set => TrySetPropertyValue(ref this.isSleepTimerAutoStartEnabled, value);
    }

    private bool isSleepTimerEnabled;

    public bool IsSleepTimerEnabled
    {
      get => this.isSleepTimerEnabled;
      set => TrySetPropertyValue(ref this.isSleepTimerEnabled, value);
    }

    private Dictionary<IAudiobook, IEnumerable<IAudiobookPart>> pendingAudiobookParts;
    private DispatcherTimer SleepTimer { get; set; }
    private bool IsDeferredSleepTimerStartEnabled { get; set; }
    private IAudioPlaybackController AudioPlaybackController { get; set; }
    private bool IsSleepTimerStopRequested { get; set; }
    private bool IsSleepTimerPaused { get; set; }
    private CancellationTokenSource InitializationCancellationTokenSource { get; set; }
    private IAudiobook HighPriorityAudiobook { get; set; }
    private object SyncLock { get; set; } = new object();
  }
}

