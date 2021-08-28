using System;
using Windows.Storage.FileProperties;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer.Player.Playback
{
  public class PartCompletedEventArgs<TStorageProperty> : EventArgs where TStorageProperty : IStorageItemExtraProperties
  {
    public PartCompletedEventArgs(IAudiobookPart completedPart, IAudiobookPart nextPart, PlaylistNavigationInfo navigationInfo)
    {
      this.CompletedPart = completedPart;
      this.NextPart = nextPart;
      this.NavigationInfo = navigationInfo;
    }

    public IAudiobookPart CompletedPart { get; set; }
    public IAudiobookPart NextPart { get; set; }
    public PlaylistNavigationInfo NavigationInfo { get; set; }
  }
}