using System;

namespace BCAudiobookPlayer.Player.Helper
{
  static class VideoPlayerHtmlCreator
  {
    public static bool TryCreateAutoTypedHtmlDocument(Uri url, out string htmlDocument)
    {
      if (url.OriginalString.StartsWith("youtube", StringComparison.OrdinalIgnoreCase))
      {
        return VideoPlayerHtmlCreator.TryCreateYoutubeHtmlDocument(url, out htmlDocument);
      }

      htmlDocument = string.Empty;
      return false;
    }

    public static bool TryCreateYoutubeHtmlDocument(Uri url, out string htmlDocument)
    {
      htmlDocument = string.Empty;
      int videoIdDelimiterIndex = url.OriginalString.LastIndexOf('=');
      if (videoIdDelimiterIndex.Equals(-1))
      {
        return false;
      }
      var videoId = url.OriginalString.Substring(videoIdDelimiterIndex + 1);
      htmlDocument = string.IsNullOrWhiteSpace(videoId) 
        ? string.Empty 
        : $"<!DOCTYPE html><html><body><iframe style=\"padding:0px;margin:0px;border:none;width:336px;height:256px;position:absolute;left:0px;top:0px;\" src=\"https://www.youtube.com/embed/{videoId}\"></iframe></body></html>";
      return !string.IsNullOrWhiteSpace(htmlDocument);
    }
  }
}
