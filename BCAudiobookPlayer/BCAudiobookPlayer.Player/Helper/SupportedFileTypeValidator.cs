using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace BCAudiobookPlayer.Player.Helper
{
  public static class SupportedFileTypeValidator
  {
    public static bool IsValid(string fileExtensionIncludingDot)
    {
      return fileExtensionIncludingDot.Length > 1 
             && Enum.TryParse(fileExtensionIncludingDot.Substring(1), true, out SupportedFileTypes fileType);
    }

    public static async Task<bool> IsPlaylistValidAsync(StorageFile playlistFile)
    {
      if (playlistFile == null)
      {
        return false;
      }

      using (IRandomAccessStreamWithContentType fileStream = await playlistFile.OpenReadAsync())
      {
        using (var reader = new StreamReader(fileStream.AsStreamForRead()))
        {
          return reader.EndOfStream ||
                 !(await reader.ReadLineAsync()).Equals("#EXTM3U", StringComparison.OrdinalIgnoreCase);
        }
      }
    }
  }
}
