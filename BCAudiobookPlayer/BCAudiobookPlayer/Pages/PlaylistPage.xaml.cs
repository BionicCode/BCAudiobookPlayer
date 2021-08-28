using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using BCAudiobookPlayer.Player;
using BCAudiobookPlayer.Player.Playback;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.ViewModel;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace BCAudiobookPlayer.Pages
{
  /// <summary>
  /// An empty page that can be used on its own or navigated to within a Frame.
  /// </summary>
  public sealed partial class PlaylistPage : Page
  {
    public PlaylistPage()
    {
      InitializeComponent();
      CommonPageHandlers.NavigationFrame = this.Frame;
      this.semaphore = new SemaphoreSlim(1, 1);
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
     "ViewModel",
     typeof(MainPageViewModel),
     typeof(PlaylistPage),
     new PropertyMetadata(default(MainPageViewModel), PlaylistPage.OnViewModelChanged));

    public MainPageViewModel ViewModel { get => (MainPageViewModel) GetValue(PlaylistPage.ViewModelProperty); set => SetValue(PlaylistPage.ViewModelProperty, value); }

    public static readonly DependencyProperty IsFlattenDirectoriesEnabledProperty = DependencyProperty.Register(
      "IsFlattenDirectoriesEnabled",
      typeof(bool),
      typeof(PlaylistPage),
      new PropertyMetadata(false));

    public bool IsFlattenDirectoriesEnabled { get => (bool) GetValue(PlaylistPage.IsFlattenDirectoriesEnabledProperty); set => SetValue(PlaylistPage.IsFlattenDirectoriesEnabledProperty, value); }

    public static readonly DependencyProperty CommonPageHandlersProperty = DependencyProperty.Register(
      "CommonPageHandlers",
      typeof(CommonPageHandlers),
      typeof(PlaylistPage),
      new PropertyMetadata(default(CommonPageHandlers)));

    public CommonPageHandlers CommonPageHandlers { get { return (CommonPageHandlers) GetValue(PlaylistPage.CommonPageHandlersProperty); } set { SetValue(PlaylistPage.CommonPageHandlersProperty, value); } }
    
    protected static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      ((PlaylistPage) d).DataContext = e.NewValue as MainPageViewModel;
    }

    public void RemoveBookmark_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is FrameworkElement frameworkElement && frameworkElement.DataContext is IBookmark bookmark)
      {
        if (this.ViewModel.RemoveBookmarkCommand.CanExecute(bookmark))
        {
          this.ViewModel.RemoveBookmarkCommand.Execute(bookmark);
        }
      }
    }

    public async void SetBookmark_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is Button button && button.DataContext is IAudiobookPart audiobookPart && button.CommandParameter is Popup messageBox)
      {
        if (this.ViewModel.AddBookmarkCommand.CanExecute(audiobookPart))
        {
          await this.semaphore.WaitAsync();
          this.ViewModel.AddBookmarkCommand.Execute(audiobookPart);
          var message = "Bookmark added";
          this.CommonPageHandlers.ShowMessageBox(message, messageBox);
          this.semaphore.Release();
        }
      }
    }

    public async void JumpToPosition_OnDragCompleted(object sender, EventArgs eventArgs)
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

    public async void SeekForward(object sender, RoutedEventArgs eventArgs)
    {
      if (sender is Button button
          && button.CommandParameter is IAudiobookPart audiobookPart
          && this.ViewModel.SkipToPositionCommand.CanExecute((audiobookPart.CurrentPosition.Add(TimeSpan.FromSeconds(0.5)), audiobookPart)))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipToPositionCommand as RelayCommand<(TimeSpan Position, IAudiobookPart AudiobookPart)>).ExecuteAsync((audiobookPart.CurrentPosition.Add(TimeSpan.FromSeconds(0.5)), audiobookPart));
        this.semaphore.Release();
      }
    }

    public async void SeekBackward(object sender, RoutedEventArgs eventArgs)
    {
      if (sender is Button button
          && button.CommandParameter is IAudiobookPart audiobookPart
          && this.ViewModel.SkipToPositionCommand.CanExecute((audiobookPart.CurrentPosition.Add(TimeSpan.FromSeconds(0.5)), audiobookPart)))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipToPositionCommand as RelayCommand<(TimeSpan Position, IAudiobookPart AudiobookPart)>).ExecuteAsync((audiobookPart.CurrentPosition.Subtract(TimeSpan.FromSeconds(10)), audiobookPart));
        this.semaphore.Release();
      }
    }

    public async void PlayFile(object sender, RoutedEventArgs e)
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

    public async void StopFile(object sender, RoutedEventArgs e)
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

    public async void SkipFileForward(object sender, RoutedEventArgs e)
    {
      if (sender is Button button
          && button.CommandParameter is IAudiobookPart audiobookPart
          && this.ViewModel.SkipForwardCommand.CanExecute(audiobookPart))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipForwardCommand as RelayCommand<IAudiobookPart>)?.ExecuteAsync(audiobookPart);
        this.semaphore.Release();
      }
    }

    public async void SkipFileBack(object sender, RoutedEventArgs e)
    {
      if (sender is Button button
          && button.CommandParameter is IAudiobookPart audiobookPart
          && this.ViewModel.SkipBackCommand.CanExecute(audiobookPart))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipBackCommand as RelayCommand<IAudiobookPart>).ExecuteAsync(audiobookPart);
        this.semaphore.Release();
      }
    }

    public async void SetRepeatStartTime_OnClick(object sender, RoutedEventArgs e)
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

    public async void SetRepeatStopTime_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is ToggleButton button && button.CommandParameter is IAudiobookPart audioFile && audioFile.IsLoopEnabled)
      {
        await this.semaphore.WaitAsync();
        TimeSpan endTime = button.IsChecked ?? false ? audioFile.CurrentPosition : audioFile.Duration;
        audioFile.LoopRange = (audioFile.LoopRange.BeginTime, endTime);
        this.semaphore.Release();
      }
    }

    public async void PlayBookmark_OnClick(object sender, ItemClickEventArgs e)
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

    public void RemovePartFromPlaylist_OnClick(object sender, RoutedEventArgs e)
    {
      if (e.OriginalSource is Button button && button.CommandParameter is IAudiobookPart audiobookPart)
      {
        this.ViewModel.RemovePartFromPlaylist(audiobookPart);
      }
    }

    public async void ShowWebContent(object sender, RoutedEventArgs e)
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

    public async void JumpToAudiobookChapter_OnClick(object sender, ItemClickEventArgs e)
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

    public async void AutoTagAudiobookPart_OnClick(object sender, RoutedEventArgs e)
    {
      if (e.OriginalSource is Button button && button.CommandParameter is IAudiobookPart audiobookPart)
      {
        await this.ViewModel.GenerateMediaTagsAsync(audiobookPart);
      }
    }

    public async void HandlePlayback_OnItemClick(object sender, ItemClickEventArgs e)
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

    public async void ToggleSleepTimer_OnToggled(object sender, RoutedEventArgs e)
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

    private SemaphoreSlim semaphore;

    private void UIElement_OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
      throw new NotImplementedException();
    }
  }
}
