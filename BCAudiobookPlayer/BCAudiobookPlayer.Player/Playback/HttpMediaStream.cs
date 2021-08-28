using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using BCAudiobookPlayer.Player.Helper;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.Player.Playback
{
  [DataContract]
  public class HttpMediaStream : IAudiobookPart, IHttpMediaStream
  {
    public new static IHttpMediaStream NullObject => new HttpMediaStream(true);

    public static async Task<(bool IsSuccessFull, IHttpMediaStream HttpMediaStreamInstance)> TryCreateAsync(string url, string title = null)
    {
      // Remove protocol header from URL
      if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
      {
        url = url.Substring("https://".Length);
      }

      if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
      {
        url = url.Substring("http://".Length);
      }

      // Remove 'www.' from URL
      if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
      {
        url = url.Substring("www.".Length);
      }

      // Remove http arguments from url
      int urlArgsIndex = url.IndexOf('&');
      if (!urlArgsIndex.Equals(-1))
      {
        url = url.Substring(0, urlArgsIndex);
      }

      // Relative URL containing only the video ID e.g. 'youtube.com/watch?v=Dxve7Zl6gcY'
      var uri = new Uri(url, UriKind.Relative);
      if (!uri.IsWellFormedOriginalString() || !VideoPlayerHtmlCreator.TryCreateAutoTypedHtmlDocument(uri, out string htmlContentSource))
      {
        return (false, HttpMediaStream.NullObject);
      }

      return (true, new HttpMediaStream(uri, title) { HtmlContentSource = htmlContentSource });
    }

    public HttpMediaStream()
    {
      this.Url = new Uri(" ", UriKind.Relative);
      this.HtmlContentSource = string.Empty;
      this.Tag = new HttpMediaTag("untitled");
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
      this.IsCreated = false;
      this.IsCreating = false;
    }

    public HttpMediaStream(Uri url, string title = null) : this()
    {
      this.Url = url;
      this.Tag.Title = title ?? string.Empty;
    }

    protected HttpMediaStream(bool isNullObject) : this()
    {
      this.IsNull = isNullObject;
    }
    
    [DataMember]
    private Uri url;
    public Uri Url
    {
      get => this.url;
      set
      {
        if (Equals(value, this.url)) return;
        this.url = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private string htmlContentSource;
    public string HtmlContentSource
    {
      get => this.htmlContentSource;
      set
      {
        if (value == this.htmlContentSource) return;
        this.htmlContentSource = value;
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

    public void SoftReset()
    {
      this.isPlaying = false;
      this.isPaused = false;
      this.isStopped = true;
    }

    /// <inheritdoc />
    public void SetToIsCreating()
    {
      this.IsCreated = false;
      this.IsCreating = true;
    }

    /// <inheritdoc />
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
      this.ProgressReportTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(500) };
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

    [DataMember]
    private string fileSystemPathToken;
    public string FileSystemPathToken
    {
      get => this.fileSystemPathToken;
      set
      {
        if (value == this.fileSystemPathToken) return;
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
        if (object.Equals(value, this.updateCurrentPositionCallback)) return;
        this.updateCurrentPositionCallback = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private bool isPaused;
    public bool IsPaused
    {
      get => this.isPaused;
      set
      {
        if (value == this.IsPaused)
          return;

        this.isPaused = value;
        OnPropertyChanged();

        if (value)
        {
          StopProgressTimer();
          OnPaused();
        }
        this.IsStopped &= !this.IsPaused;
        this.IsPlaying &= !this.IsPaused;
      }
    }

    [DataMember]
    private bool isStopped;
    public bool IsStopped
    {
      get => this.isStopped;
      set
      {
        if (value == this.IsStopped)
          return;

        this.isStopped = value;
        OnPropertyChanged();
        if (value)
        {
          StopProgressTimer();
          this.CurrentPosition = TimeSpan.Zero;
          OnStopped();
        }
        this.IsPlaying &= !this.IsStopped;
        this.IsPaused &= !this.IsStopped;
      }
    }

    public bool HasProgress { get => this.CurrentPosition > TimeSpan.Zero; }

    [DataMember]
    private bool isPlaying;
    public bool IsPlaying
    {
      get => this.isPlaying;
      set
      {
        if (value == this.IsPlaying)
          return;

        this.isPlaying = value;
        OnPropertyChanged();
        if (value)
        {
          StartProgressTimer();
          OnStarted();
        }
        this.IsPaused &= !this.IsPlaying;
        this.IsStopped &= !this.IsPlaying;
        this.IsCompleted &= !this.IsPlaying;
      }
    }

    [DataMember]
    private bool isCompleted;
    public bool IsCompleted
    {
      get => this.isCompleted;
      set
      {
        if (value == this.isCompleted)
          return;

        this.isCompleted = value;
        OnPropertyChanged();

        if (value)
        {
          if (this.IsProgressTimerStarted)
          {
            StopProgressTimer();
          }
          OnCompleted();
        }
        this.IsPaused &= !this.IsCompleted;
        this.IsStopped &= !this.IsCompleted;
        this.IsPlaying &= !this.IsCompleted;
      }
    }

    [DataMember]
    private bool isLoopEnabled;
    public bool IsLoopEnabled
    {
      get => this.isLoopEnabled;
      set
      {
        if (value == this.isLoopEnabled) return;
        this.isLoopEnabled = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private int? loopCount;
    public int? LoopCount
    {
      get => this.loopCount;
      set
      {
        if (value == this.loopCount) return;
        this.loopCount = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private (TimeSpan BeginTime, TimeSpan EndTime) loopRange;
    public (TimeSpan BeginTime, TimeSpan EndTime) LoopRange
    {
      get => this.loopRange;
      set
      {
        if (value.Equals(this.loopRange)) return;
        this.loopRange = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    public bool IsNull { get; private set; }

    [DataMember]
    private double oldVolume;

    [DataMember]
    private double volume;
    public double Volume
    {
      get => this.volume;
      set
      {
        if (value.Equals(this.volume))
        {
          return;
        }
        this.oldVolume = this.volume;
        this.volume = value;
        OnPropertyChanged();
        this.IsMuted = this.Volume.Equals(0d);
      }
    }

    [DataMember]
    private bool isMuted;
    public bool IsMuted
    {
      get => this.isMuted;
      set
      {
        if (value.Equals(this.isMuted))
        {
          return;
        }
        this.isMuted = value;
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

    [DataMember]
    private double speedMultiplier;
    public double SpeedMultiplier
    {
      get => this.speedMultiplier;
      set
      {
        if (value.Equals(this.speedMultiplier)) return;
        this.speedMultiplier = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private bool isSelected;
    public bool IsSelected
    {
      get => this.isSelected;
      set
      {
        this.isSelected = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private bool isVisible;
    public bool IsVisible
    {
      get => this.isVisible;
      set
      {
        this.isVisible = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private ObservableCollection<IBookmark> bookmarks;
    public ObservableCollection<IBookmark> Bookmarks
    {
      get => this.bookmarks;
      set
      {
        if (Equals(value, this.bookmarks)) return;
        this.bookmarks = value;
        OnPropertyChanged();
      }
    }

    public IBookmark this[int bookmarkIndex]
    {
      get => this.Bookmarks[bookmarkIndex];
      set => this.Bookmarks[bookmarkIndex] = value;
    }

    [DataMember]
    private TimeSpan currentPosition;
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

    [DataMember]
    private TimeSpan timeRemaining;
    public TimeSpan TimeRemaining
    {
      get => this.timeRemaining;
      set
      {
        if (value.Equals(this.timeRemaining)) return;
        this.timeRemaining = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private TimeSpan duration;
    public TimeSpan Duration
    {
      get => this.duration;
      set
      {
        if (value.Equals(this.duration)) return;
        this.duration = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    private PlaylistNavigationInfo navigationInfo;
    public PlaylistNavigationInfo NavigationInfo
    {
      get { return this.navigationInfo; }
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

    private bool isCreated;
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
    ~HttpMediaStream()
    {
      Dispose(false);
    }

    #endregion
  }
}
