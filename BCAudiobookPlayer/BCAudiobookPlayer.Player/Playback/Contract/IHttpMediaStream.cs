using System;

namespace BCAudiobookPlayer.Player.Playback.Contract
{
  public interface IHttpMediaStream : IAudiobookPart
  {
    Uri Url { get; set; }
    string HtmlContentSource { get; set; }
  }
}