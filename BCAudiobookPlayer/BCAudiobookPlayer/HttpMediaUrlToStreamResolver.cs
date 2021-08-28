using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.Web;
using BCAudiobookPlayer.Player.Playback.Contract;

namespace BCAudiobookPlayer
{
  public sealed class HttpMediaUrlToStreamResolver : IUriToStreamResolver
  {
    private static HttpMediaUrlToStreamResolver instance;
    public static HttpMediaUrlToStreamResolver Instance =>
      HttpMediaUrlToStreamResolver.instance ?? (HttpMediaUrlToStreamResolver.instance = new HttpMediaUrlToStreamResolver());

    private HttpMediaUrlToStreamResolver()
    {
      this.UriToStreamMap = new Dictionary<Uri, IInputStream>();
    }

    public async Task AddHttpMediaStream(IHttpMediaStream httpMediaStream, Uri streamUri)
    {
      if (this.UriToStreamMap.ContainsKey(streamUri))
      {
        return;
      }

      this.UriToStreamMap.Add(streamUri, await CreateMemoryStreamFromHtmlContentAsync(httpMediaStream.HtmlContentSource));
    }

    private async Task<IInputStream> CreateMemoryStreamFromHtmlContentAsync(string htmlDocument)
    {
      var outputStream = new InMemoryRandomAccessStream();
      using (var dataWriter = new DataWriter(outputStream))
      {
        dataWriter.WriteString(htmlDocument);
        await dataWriter.StoreAsync();
        dataWriter.DetachStream();
        dataWriter.Dispose();
      }

      outputStream.Seek(0);
      return outputStream;
    }

    #region Implementation of IUriToStreamResolver

    public IAsyncOperation<IInputStream> UriToStreamAsync(Uri uri)
    {
      return CreateMemoryStreamFromHtmlContentAsync(uri).AsAsyncOperation();
    }

    private Task<IInputStream> CreateMemoryStreamFromHtmlContentAsync(Uri uri)
    {
      var taskCompletionSource = new TaskCompletionSource<IInputStream>();
      taskCompletionSource.SetResult(this.UriToStreamMap.TryGetValue(uri, out IInputStream inputStream)
        ? inputStream
        : new InMemoryRandomAccessStream());

      return taskCompletionSource.Task;
    }

    #endregion

    private Dictionary<Uri, IInputStream> UriToStreamMap { get; set; }
  }
}