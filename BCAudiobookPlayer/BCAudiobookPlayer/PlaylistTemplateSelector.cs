using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BCAudiobookPlayer.Player.Playback.Contract;
using JetBrains.Annotations;

namespace BCAudiobookPlayer
{
    class PlaylistTemplateSelector : DataTemplateSelector, INotifyPropertyChanged
    {
      #region Overrides of DataTemplateSelector

      protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
      {
        return SelectTemplateCore(item);
      }

      protected override DataTemplate SelectTemplateCore(object item)
      {
        return item is IAudiobook
          ? this.AudiobookDataTemplate
          : item is IHttpMediaStream
            ? this.HttpMediaStreamDataTemplate
            : this.AudioFileDataTemplate;
      }

      #endregion

      public event PropertyChangedEventHandler PropertyChanged;

      [NotifyPropertyChangedInvocator]
      protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
      {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }

      private DataTemplate audioFileDataTemplate;   
      public DataTemplate AudioFileDataTemplate
      {
        get { return this.audioFileDataTemplate; }
        set 
        { 
          this.audioFileDataTemplate = value; 
          OnPropertyChanged();
        }
      }

      private DataTemplate audiobookDataTemplate;   
      public DataTemplate AudiobookDataTemplate
      {
        get { return this.audiobookDataTemplate; }
        set 
        { 
          this.audiobookDataTemplate = value; 
          OnPropertyChanged();
        }
      }

      private DataTemplate httpMediaStreamDataTemplate;   
      public DataTemplate HttpMediaStreamDataTemplate
      {
        get { return this.httpMediaStreamDataTemplate; }
        set 
        { 
          this.httpMediaStreamDataTemplate = value; 
          OnPropertyChanged();
        }
      }
    }
}
