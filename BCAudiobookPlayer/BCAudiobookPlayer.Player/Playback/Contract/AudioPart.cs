using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.Player.Playback.Contract
{
  [DataContract]
  public class AudioPart : IAudiobookPart
  {
    public static IAudiobookPart NullObject => new AudioPart(true);

    #region ctor

    protected AudioPart()
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

    public AudioPart(IStorageItem storageItem) : this()
    {
      this.FileSystemPath = storageItem.Path;
      this.FileName = storageItem.Name;
    }

    protected AudioPart(bool isNull) : this()
    {
      this.IsNull = isNull;
    }

    #endregion

    public void SoftReset()
    {
      this.isPlaying = false;
      this.isPaused = false;
      this.isStopped = true;
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

    public virtual bool TryAddBookmark(IBookmark bookmark)
    {
      if (bookmark.Position < TimeSpan.Zero || bookmark.Position > this.Duration)
      {
        return false;
      }
      this.Bookmarks.Add(bookmark);
      return true;
    }

    public virtual void RemoveBookmark(IBookmark bookmark)
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
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    private void OnCurrentPositionChanged()
    {
      this.CurrentPositionChanged?.Invoke(this, EventArgs.Empty);
    }
    protected virtual void OnCompleted()
    {
      this.Completed?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnStarted()
    {
      this.Started?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnStopped()
    {
      this.Stopped?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnPaused()
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
    public string FileSystemPath { get; protected set; }

    [DataMember]
    public string FileName { get; protected set; }

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

    protected Action updateCurrentPositionCallback;
    public virtual Action UpdateCurrentPositionCallback
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
    protected bool isPaused;
    public virtual bool IsPaused
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
    protected bool isStopped;
    public virtual bool IsStopped
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

    public virtual bool HasProgress { get => this.CurrentPosition > TimeSpan.Zero; }

    [DataMember]
    protected bool isPlaying;
    public virtual bool IsPlaying
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
    protected bool isCompleted;
    public virtual bool IsCompleted
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
    protected bool isLoopEnabled;
    public virtual bool IsLoopEnabled
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
    protected int? loopCount;
    public virtual int? LoopCount
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
    protected (TimeSpan BeginTime, TimeSpan EndTime) loopRange;
    public virtual (TimeSpan BeginTime, TimeSpan EndTime) LoopRange
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
    public bool IsNull { get; protected set; }

    [DataMember]
    protected double oldVolume;

    [DataMember]
    protected double volume;
    public virtual double Volume
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
    protected bool isMuted;   
    public virtual bool IsMuted
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
    protected double speedMultiplier;
    public virtual double SpeedMultiplier
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
    protected bool isSelected;
    public virtual bool IsSelected
    {
      get => this.isSelected;
      set
      {
        this.isSelected = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    protected bool isVisible;
    public virtual bool IsVisible
    {
      get => this.isVisible;
      set
      {
        this.isVisible = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    protected IMediaTag<MusicProperties> tag;
    public virtual IMediaTag<MusicProperties> Tag
    {
      get => this.tag;
      set
      {
        this.tag = value;
        OnPropertyChanged();
      }
    }

    [DataMember]
    protected ObservableCollection<IBookmark> bookmarks;
    public virtual ObservableCollection<IBookmark> Bookmarks
    {
      get => this.bookmarks;
      set
      {
        if (Equals(value, this.bookmarks)) return;
        this.bookmarks = value;
        OnPropertyChanged();
      }
    }

    public virtual IBookmark this[int bookmarkIndex]
    {
      get => this.Bookmarks[bookmarkIndex];
      set => this.Bookmarks[bookmarkIndex] = value;
    }

    [DataMember]
    protected TimeSpan currentPosition;
    public virtual TimeSpan CurrentPosition
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
    protected TimeSpan timeRemaining;
    public virtual TimeSpan TimeRemaining
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
    protected TimeSpan duration;
    public virtual TimeSpan Duration
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
    protected PlaylistNavigationInfo navigationInfo;   
    public virtual PlaylistNavigationInfo NavigationInfo
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
      protected set 
      { 
        this.isCreating = value; 
        OnPropertyChanged();
      }
    }

    [DataMember] private bool isCreated;   
    public bool IsCreated
    {
      get => this.isCreated;
      protected set 
      { 
        this.isCreated = value; 
        OnPropertyChanged();
      }
    }


    #region IDisposable

    private void ReleaseUnmanagedResources()
    {
    }

    protected virtual void Dispose(bool disposing)
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
    ~AudioPart()
    {
      Dispose(false);
    }

    #endregion
  }
}