using System;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using BCAudiobookPlayer.Player.Playback;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.ViewModel;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace BCAudiobookPlayer.Pages
{
  /// <summary>
  /// An empty page that can be used on its own or navigated to within a Frame.
  /// </summary>
  public sealed partial class PlaybackPage : Page
  {

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
      "ViewModel",
      typeof(MainPageViewModel),
      typeof(PlaybackPage),
      new PropertyMetadata(default(MainPageViewModel), PlaybackPage.OnViewModelChanged));

    public MainPageViewModel ViewModel { get => (MainPageViewModel) GetValue(PlaybackPage.ViewModelProperty); set => SetValue(PlaybackPage.ViewModelProperty, value); }

    public static readonly DependencyProperty IsFlattenDirectoriesEnabledProperty = DependencyProperty.Register(
      "IsFlattenDirectoriesEnabled",
      typeof(bool),
      typeof(PlaybackPage),
      new PropertyMetadata(false));

    public bool IsFlattenDirectoriesEnabled { get => (bool) GetValue(PlaybackPage.IsFlattenDirectoriesEnabledProperty); set => SetValue(PlaybackPage.IsFlattenDirectoriesEnabledProperty, value); }

    public static readonly DependencyProperty CommonPageHandlersProperty = DependencyProperty.Register(
      "CommonPageHandlers",
      typeof(CommonPageHandlers),
      typeof(PlaybackPage),
      new PropertyMetadata(default(CommonPageHandlers)));

    public CommonPageHandlers CommonPageHandlers { get { return (CommonPageHandlers) GetValue(PlaybackPage.CommonPageHandlersProperty); } set { SetValue(PlaybackPage.CommonPageHandlersProperty, value); } }

    protected static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      ((PlaybackPage) d).DataContext = e.NewValue as MainPageViewModel;
    }
    public PlaybackPage()
    {
      this.InitializeComponent();
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
          && this.ViewModel.StopCommand.CanExecute(audiobookPart))
      {
        await this.semaphore.WaitAsync();
        await (this.ViewModel.SkipForwardCommand as RelayCommand<IAudiobookPart>).ExecuteAsync(audiobookPart);
        this.semaphore.Release();
      }
    }

    public async void SkipFileBack(object sender, RoutedEventArgs e)
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


    private SemaphoreSlim semaphore;
  }
}
