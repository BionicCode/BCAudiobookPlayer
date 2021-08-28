using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using BCAudiobookPlayer.Pages;
using BCAudiobookPlayer.Player;
using BCAudiobookPlayer.Player.Playback;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.ResourceProvider;
using BCAudiobookPlayer.ViewModel;

namespace BCAudiobookPlayer
{
    public class CommonPageHandlers
    {
      public CommonPageHandlers()
      {
        this.semaphore = new SemaphoreSlim(1, 1);
        this.MessageBoxTable = new Dictionary<DispatcherTimer, Popup>();
        this.ReverseMessageBoxTable = new Dictionary<Popup, DispatcherTimer>();
        this.DelayedRegistrationTaskTable = new Dictionary<string, Lazy<Task>>();
        this.DelayedRegistrationFileTable = new Dictionary<string, StorageFile>();
    }

      public async void ShowFilePicker(object sender, RoutedEventArgs e)
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

      public async void ShowFolderPicker(object sender, RoutedEventArgs e)
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
      foreach (StorageFile file in folderContent)
      {
        this.DelayedRegistrationFileTable.TryAdd(file.Path, file);

      }
      await this.ViewModel.AddFolderToPlayList(pickedFolder, folderContent);
      this.semaphore.Release();
    }

      public void CleanUpOnCreationCompleted(object sender, ValueChangedEventArgs<IAudiobookPart> e)
    {
      Audiobook.PartCreated -= RegisterFileOnPartCreated;
      this.ViewModel.AudioPartCreated -= CleanUpOnCreationCompleted;
    }

      public async void RegisterFileOnPartCreated(object sender, ValueChangedEventArgs<IAudiobookPart> e)
    {
      if (this.DelayedRegistrationFileTable.TryGetValue(e.Value.FileSystemPath, out StorageFile file))
      {
        e.Value.FileSystemPathToken = await FileController.RegisterFileAsync(file);
        this.DelayedRegistrationFileTable.Remove(e.Value.FileSystemPath);
      }
    }

    public void  ShowMessageBox(string message, Popup messageBox)
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

      timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(CommonPageHandlers.MessageBoxTimerIntervalInSeconds) };
      timer.Tick += OnMessageBoxTimerElapsed;
      this.MessageBoxTable.Add(timer, messageBox);
      this.ReverseMessageBoxTable.Add(messageBox, timer);
      messageBox.IsOpen = true;
      timer.Start();
    }

    public void OnMessageBoxTimerElapsed(object sender, object e)
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

    public void ShowPlaybackPage(object sender, RoutedEventArgs e)
    {
      CommonPageHandlers.NavigationFrame.Navigate(typeof(PlaybackPage));
    }
    public MainPageViewModel ViewModel { get; set; }

    public static Frame NavigationFrame { get; set; }
    private SemaphoreSlim semaphore;
    private DispatcherTimer MessageBoxTimer { get; set; }
    private const double MessageBoxTimerIntervalInSeconds = 2;
    public Popup CurrentMessageBox { get; set; }
    private Dictionary<DispatcherTimer, Popup> MessageBoxTable { get; set; }
    private Dictionary<Popup, DispatcherTimer> ReverseMessageBoxTable { get; set; }

    protected Dictionary<string, Lazy<Task>> DelayedRegistrationTaskTable { get; set; }
    protected Dictionary<string, StorageFile> DelayedRegistrationFileTable { get; set; }
  }
}
