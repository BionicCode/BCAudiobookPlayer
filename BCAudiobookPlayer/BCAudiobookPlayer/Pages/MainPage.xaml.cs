using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using BCAudiobookPlayer.Player;
using BCAudiobookPlayer.Player.Playback;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.ResourceProvider;
using BCAudiobookPlayer.ViewModel;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BCAudiobookPlayer.Pages
{
  /// <summary>
  /// An empty page that can be used on its own or navigated to within a Frame.
  /// </summary>
  public sealed partial class MainPage : Page
  {
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
      "ViewModel",
      typeof(MainPageViewModel),
      typeof(MainPage),
      new PropertyMetadata(default(MainPageViewModel)));

    public MainPageViewModel ViewModel { get => (MainPageViewModel) GetValue(MainPage.ViewModelProperty); set => SetValue(MainPage.ViewModelProperty, value); }

    public static readonly DependencyProperty IsFlattenDirectoriesEnabledProperty = DependencyProperty.Register(
      "IsFlattenDirectoriesEnabled",
      typeof(bool),
      typeof(MainPage),
      new PropertyMetadata(false));

    public bool IsFlattenDirectoriesEnabled { get => (bool) GetValue(MainPage.IsFlattenDirectoriesEnabledProperty); set => SetValue(MainPage.IsFlattenDirectoriesEnabledProperty, value); }


    public MainPage()
    {
      InitializeComponent();
      this.DelayedRegistrationTaskTable = new Dictionary<string, Lazy<Task>>();
      this.DelayedRegistrationFileTable = new Dictionary<string, StorageFile>();
      this.semaphore = new SemaphoreSlim(1, 1);
      this.MessageBoxTable = new Dictionary<DispatcherTimer, Popup>();
      this.ReverseMessageBoxTable = new Dictionary<Popup, DispatcherTimer>();
      this.Loaded += (s, e) => this.ContentFrame.Navigate(typeof(PlaylistPage));
    }

    private async void ShowFilePicker(object sender, RoutedEventArgs e)
    {
      StorageFile pickedFile = await MediaPicker.PickFile();
      if (pickedFile == null)
      {
        return;
      }
      await this.semaphore.WaitAsync();
      await FileController.RegisterFileAsync(pickedFile);
      await this.ViewModel.AddFileToPlaylist(pickedFile);
      this.semaphore.Release();
    }

    private async void ShowFolderPicker(object sender, RoutedEventArgs e)
    {
      StorageFolder pickedFolder = await MediaPicker.PickDirectory();
      if (pickedFolder == null)
      {
        return;
      }

      await this.semaphore.WaitAsync();
      IEnumerable<StorageFile> folderContent = (await FileController.RegisterFolder(pickedFolder)).ToList();
      Audiobook.PartCreated += RegisterFileOnPartCreated;
      this.ViewModel.AudioPartCreated += CleanUpOnCreationCompleted;
//      foreach (StorageFile file in folderContent)
//      {
//        this.DelayedRegistrationFileTable.TryAdd(file.Path, file);
//
//      }
      await this.ViewModel.AddFolderToPlayList(pickedFolder, folderContent);
      this.semaphore.Release();
    }

    private void CleanUpOnCreationCompleted(object sender, ValueChangedEventArgs<IAudiobookPart> e)
    {
      Audiobook.PartCreated -= RegisterFileOnPartCreated;
      this.ViewModel.AudioPartCreated -= CleanUpOnCreationCompleted;
    }

    private void RegisterFileOnPartCreated(object sender, ValueChangedEventArgs<IAudiobookPart> e)
    {
      e.Value.FileSystemPathToken = (sender as IAudiobook).FileSystemPathToken;
//      if (this.DelayedRegistrationFileTable.TryGetValue(e.Value.FileSystemPath, out StorageFile file))
//      {
//        e.Value.FileSystemPathToken = FileController.RegisterFileAsync(file);
//        this.DelayedRegistrationFileTable.Remove(e.Value.FileSystemPath);
//      }
    }

    private async void ShowWebContent(object sender, RoutedEventArgs e)
    {
      if (!(sender is WebView webView) || !(webView.DataContext is IHttpMediaStream httpMediaStream))
      {
        return;
      }

      await this.semaphore.WaitAsync();
      try
      {
        webView.NavigateToString(httpMediaStream.HtmlContentSource);
      }
      catch (Exception)
      {
        webView.GoBack();
      }
      finally
      {
        this.semaphore.Release();
      }
    }

    private async void PlayFile(object sender, RoutedEventArgs e)
    {
      if (sender is ToggleButton toggleButton && toggleButton.CommandParameter is IAudiobookPart audiobookPart)
      {
        bool isToggleButtonChecked = toggleButton.IsChecked ?? false;
        if (this.ViewModel.PauseCommand.CanExecute(audiobookPart))
        {
          await this.semaphore.WaitAsync();
          if (isToggleButtonChecked)
          {
            await (this.ViewModel.PlayCommand as RelayCommand<IAudiobookPart>).ExecuteAsync(audiobookPart);
          }
          else 
          {
            await (this.ViewModel.PauseCommand as RelayCommand<IAudiobookPart>).ExecuteAsync(audiobookPart);
          }
          this.semaphore.Release();
        }
      }
    }

    private async void StopFile(object sender, RoutedEventArgs e)
    {
      if (sender is Button button
          && button.CommandParameter is IAudiobookPart audioFile
          && this.ViewModel.StopCommand.CanExecute(audioFile))
      {
        await this.semaphore.WaitAsync();
        this.ViewModel.StopCommand.Execute(audioFile);
        this.semaphore.Release();
      }
    }

    private async void SkipFileForward(object sender, RoutedEventArgs e)
    {
      if (sender is Button button
          && button.CommandParameter is IAudiobookPart audiobookPart
          && this.ViewModel.StopCommand.CanExecute(audiobookPart))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipForwardCommand as RelayCommand<IAudiobookPart>).ExecuteAsync(audiobookPart);
        this.semaphore.Release();
      }
    }

    private async void SkipFileBack(object sender, RoutedEventArgs e)
    {
      if (sender is Button button
          && button.CommandParameter is IAudiobookPart audiobookPart
          && this.ViewModel.StopCommand.CanExecute(audiobookPart))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipBackCommand as RelayCommand<IAudiobookPart>).ExecuteAsync(audiobookPart);
        this.semaphore.Release();
      }
    }

    private async void SetBookmark_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is Button button && button.DataContext is IAudiobookPart audiobookPart && button.CommandParameter is Popup messageBox)
      {
        if (this.ViewModel.AddBookmarkCommand.CanExecute(audiobookPart))
        {
          await this.semaphore.WaitAsync();
          this.ViewModel.AddBookmarkCommand.Execute(audiobookPart);
          var message = "Bookmark added";
          ShowMessageBox(message, messageBox);
          this.semaphore.Release();
        }
      }
    }

    private void ShowMessageBox(string message, Popup messageBox)
    {
      if (!(messageBox.Child is Border border && border.Child is TextBlock textBlock))
      {
        return;
      }

      textBlock.Text = message;
      DispatcherTimer timer = null;
      if (messageBox.IsOpen)
      {
        if (this.ReverseMessageBoxTable.TryGetValue(messageBox, out timer))
        {
          timer.Stop();
          timer.Start();
          return;
        }
      }

      timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(MainPage.MessageBoxTimerIntervalInSeconds) };
      timer.Tick += OnMessageBoxTimerElapsed;
      this.MessageBoxTable.Add(timer, messageBox);
      this.ReverseMessageBoxTable.Add(messageBox, timer);
      messageBox.IsOpen = true;
      timer.Start();
    }

    private void OnMessageBoxTimerElapsed(object sender, object e)
    {
      var timer = sender as DispatcherTimer;
      timer.Tick -= OnMessageBoxTimerElapsed;
      timer.Stop();
      if (this.MessageBoxTable.TryGetValue(timer, out Popup messageBox))
      {
        messageBox.IsOpen = false;
        this.MessageBoxTable.Remove(timer);
      }

      this.ReverseMessageBoxTable.Remove(messageBox);
      timer = null;
    }

    private void RemoveBookmark_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is FrameworkElement frameworkElement && frameworkElement.DataContext is IBookmark bookmark)
      {
        if (this.ViewModel.RemoveBookmarkCommand.CanExecute(bookmark))
        {
          this.ViewModel.RemoveBookmarkCommand.Execute(bookmark);
        }
      }
    }

    private async void PlayBookmark_OnClick(object sender, ItemClickEventArgs e)
    {
      if (e.ClickedItem is IBookmark bookmark)
      {
        if (this.ViewModel.PlayBookmarkCommand.CanExecute(bookmark))
        {
          await this.semaphore.WaitAsync();
          await (this.ViewModel.PlayBookmarkCommand as RelayCommand<IBookmark>).ExecuteAsync(bookmark);
          this.semaphore.Release();
        }
      }
    }

    private async void JumpToPosition_OnDragCompleted(object sender, EventArgs eventArgs)
    {
      if (sender is Slider slider
          && slider.DataContext is IAudiobookPart audiobookPart
          && this.ViewModel.SkipToPositionCommand.CanExecute((TimeSpan.FromSeconds(slider.Value), audiobookPart)))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipToPositionCommand as RelayCommand<(TimeSpan Position, IAudiobookPart AudiobookPart)>).ExecuteAsync((TimeSpan.FromSeconds(slider.Value), audiobookPart));
        this.semaphore.Release();
      }
    }

    private async void HandlePlayback_OnItemClick(object sender, ItemClickEventArgs e)
    {
      var audioFile = e.ClickedItem as IAudiobookPart;
      await this.semaphore.WaitAsync();
      if (audioFile.IsPlaying && this.ViewModel.PauseCommand.CanExecute(audioFile))
      {
        this.ViewModel.PauseCommand.Execute(audioFile);
      }
      else if (this.ViewModel.PlayCommand.CanExecute(audioFile))
      {
        this.ViewModel.PlayCommand.Execute(audioFile);
      }
      this.semaphore.Release();
    }

    private async void SetRepeatStartTime_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is ToggleButton button && button.CommandParameter is IAudiobookPart audioFile)
      {
        await this.semaphore.WaitAsync();
        TimeSpan beginTime = button.IsChecked ?? false ? audioFile.CurrentPosition : TimeSpan.Zero;
        TimeSpan endTime = audioFile.LoopRange.EndTime;
        if (beginTime > endTime)
        {
          endTime = beginTime;
          beginTime = audioFile.LoopRange.EndTime;
        }
        audioFile.LoopRange = (beginTime, endTime);
        this.semaphore.Release();
      }
    }

    private async void SetRepeatStopTime_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is ToggleButton button && button.CommandParameter is IAudiobookPart audioFile && audioFile.IsLoopEnabled)
      {
        await this.semaphore.WaitAsync();
        TimeSpan endTime = button.IsChecked ?? false ? audioFile.CurrentPosition : audioFile.Duration;
        audioFile.LoopRange = (audioFile.LoopRange.BeginTime, endTime);
        this.semaphore.Release();
      }
    }

    private void RemovePartFromPlaylist_OnClick(object sender, RoutedEventArgs e)
    {
      if (e.OriginalSource is Button button && button.CommandParameter is IAudiobookPart audiobookPart)
      {
        this.ViewModel.RemovePartFromPlaylist(audiobookPart);
      }
    }

    private async void JumpToAudiobookChapter_OnClick(object sender, ItemClickEventArgs e)
    {
      if (e.ClickedItem is IAudiobookPart audiobookPart && e.OriginalSource is FrameworkElement frameworkElement && frameworkElement.DataContext is IAudiobook audiobook)
      {
        if (this.ViewModel.SkipToChapterCommand.CanExecute((audiobook, audiobookPart)))
        {
          await this.semaphore.WaitAsync();
          await (this.ViewModel.SkipToChapterCommand as RelayCommand<(IAudiobook, IAudiobookPart)>).ExecuteAsync((audiobook, audiobookPart));
          this.semaphore.Release();
        }
      }
    }

    private async void AutoTagAudiobookPart_OnClick(object sender, RoutedEventArgs e)
    {
      if (e.OriginalSource is Button button && button.CommandParameter is IAudiobookPart audiobookPart)
      {
        await this.ViewModel.GenerateMediaTagsAsync(audiobookPart);
      }
    }

    private async void ToggleSleepTimer_OnToggled(object sender, RoutedEventArgs e)
    {
      try
      {
        if (sender is ToggleSwitch sleepTimerSwitch)
        {
          await this.semaphore.WaitAsync();
          if (sleepTimerSwitch.IsOn)
          {
            this.ViewModel.StartSleepTimer();
            return;
          }

          this.ViewModel.StopSleepTimer();
        }
      }
      finally
      {
        this.semaphore.Release();
      }
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      ((MainPage) d).DataContext = e.NewValue as MainPageViewModel;
    }

    private void ShowPlaylistPage(object sender, RoutedEventArgs e)
    {
      this.ContentFrame.Navigate(typeof(PlaylistPage));
    }

    private void ShowPlaybackPage(object sender, RoutedEventArgs e)
    {
      this.ContentFrame.Navigate(typeof(PlaybackPage));
    }

    private Dictionary<string, Lazy<Task>> DelayedRegistrationTaskTable { get; set; }
    private Dictionary<string, StorageFile> DelayedRegistrationFileTable { get; set; }
    private SemaphoreSlim semaphore;
    private DispatcherTimer MessageBoxTimer { get; set; }
    private const double MessageBoxTimerIntervalInSeconds = 2;
    private Popup CurrentMessageBox { get; set; }
    private Dictionary<DispatcherTimer, Popup> MessageBoxTable { get; set; }
    private Dictionary<Popup, DispatcherTimer> ReverseMessageBoxTable { get; set; }
  }
}
