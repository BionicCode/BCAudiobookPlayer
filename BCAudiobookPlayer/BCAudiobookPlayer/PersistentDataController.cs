using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Storage;
using Windows.Storage.Streams;
using BCAudiobookPlayer.Player.Playback;
using BCAudiobookPlayer.Player.Playback.Contract;
using BCAudiobookPlayer.Player.Playback.Contract.Generic;

namespace BCAudiobookPlayer
{
  internal class PersistentDataController
  {
    private StorageFile PersistentDataFile { get; set; }
    private const string PersistentDataStorageFileName = "persistent.bcp";
    private const string PersistentDataStorageFolderName = "BCAudiobookPlayer";
    private const string PersistentDataStorageFileFullPath = PersistentDataController.PersistentDataStorageFolderName + @"\" + PersistentDataController.PersistentDataStorageFileName;
    private PersistentDataObject PersistentDataObject { get; set; }
    private bool IsInitialized { get; set; }
    private bool IsDirty { get; set; }
    private bool IsDataObjectOutdated { get; set; }

    public PersistentDataController()
    {
      this.IsInitialized = false;
      this.IsDirty = false;
      this.IsDataObjectOutdated = true;
      this.PersistentDataObject = PersistentDataObject.NullObject;
    }

    public void SavePlaylist(IPlaylist playlist)
    {
      this.PersistentDataObject.Playlist = playlist;
      this.IsDirty = true;
    }

    public void SaveFutureAccessListTokenMap(IDictionary<string, string> futureAccessListTokenMap)
    {
      this.PersistentDataObject.FutureAccessTokenMap = futureAccessListTokenMap;
      this.IsDirty = true;
    }

    public void SaveMostRecentUsedFilesTokenMap(IDictionary<string, string> mostRecentUsedFilesTokenMap)
    {
      this.PersistentDataObject.MostRecentUsedFilesTokenMap = mostRecentUsedFilesTokenMap;
      this.IsDirty = true;
    }

    public async Task<IPlaylist> LoadPlaylistAsync()
    {
      if (this.IsDataObjectOutdated)
      {
        await FetchPersistentDataAsync();
      }

      return this.PersistentDataObject.Playlist;
    }

    public async Task<IDictionary<string, string>> LoadFutureAccessTokenMapAsync()
    {
      if (this.IsDataObjectOutdated)
      {
        await FetchPersistentDataAsync();
      }

      return this.PersistentDataObject.FutureAccessTokenMap;
    }

    public async Task<IDictionary<string, string>> LoadMostRecentUsedFilesTokenMapAsync()
    {
      if (this.IsDataObjectOutdated)
      {
        await FetchPersistentDataAsync();
      }

      return this.PersistentDataObject.MostRecentUsedFilesTokenMap;
    }

    public async Task CommitPersistentDataAsync()
    {
      if (!this.IsDirty)
      {
        return;
      }

      if (!this.IsInitialized && !await TryInitialize())
      {
        return;
      }

      try
      {
        MemoryStream serializedPlaintextStream = SerializeCurrentPersistentData();
        if (serializedPlaintextStream.Length.Equals(0))
        {
          this.PersistentDataObject = PersistentDataObject.NullObject;
          return;
        }
        await EncodeAndWriteSerializedDataToFile(serializedPlaintextStream);
      }
      catch (Exception)
      {
      }
    }

    public async Task FetchPersistentDataAsync()
    {
      if (!this.IsDataObjectOutdated)
      {
        return;
      }

      try
      {
        MemoryStream serializedPlaintextStream = await ReadAndDecodeDataFromFile();
        if (serializedPlaintextStream.Length.Equals(0))
        {
          this.PersistentDataObject = PersistentDataObject.NullObject;
          return;
        }

        this.PersistentDataObject = DeserializeCurrentPersistentData(serializedPlaintextStream);
      }
      catch (SerializationException)
      {
        this.PersistentDataObject = PersistentDataObject.NullObject;
      }

      this.IsDataObjectOutdated = false;
    }

    private async Task EncodeAndWriteSerializedDataToFile(MemoryStream serializedPlaintextStream)
    {
      await TryInitialize();
      string plaintext = Encoding.UTF8.GetString(serializedPlaintextStream.ToArray());
      using (IRandomAccessStream persistentStorageFileStream = await this.PersistentDataFile.OpenAsync(FileAccessMode.ReadWrite))
      {
        using (Stream fileOutputStream = persistentStorageFileStream.GetOutputStreamAt(0U).AsStreamForWrite())
        {
          using (var cryptoStream = new CryptoStream(fileOutputStream, new ToBase64Transform(), CryptoStreamMode.Write))
          {
            using (var encodedFileWriter = new StreamWriter(cryptoStream))
            {
              await encodedFileWriter.WriteAsync(plaintext);
              await encodedFileWriter.FlushAsync();
              await cryptoStream.FlushAsync();
              await fileOutputStream.FlushAsync();
              this.IsDirty = false;
              this.IsDataObjectOutdated = true;
            }
          }
        }
      }
    }

    private async Task<MemoryStream> ReadAndDecodeDataFromFile()
    {
      var decryptedStream = new MemoryStream();
      if (this.IsDataObjectOutdated)
      {
        try
        {
          this.PersistentDataFile =
            await ApplicationData.Current.RoamingFolder.GetFileAsync(
              PersistentDataController.PersistentDataStorageFileFullPath);
        }
        catch (FileNotFoundException)
        {
          return decryptedStream;
        }
      }

      using (IRandomAccessStream persistentStorageFileStream = await this.PersistentDataFile.OpenAsync(FileAccessMode.ReadWrite))
      {
        if (persistentStorageFileStream.Size.Equals(0L))
        {
          return decryptedStream;
        }

        using (Stream fileInputStream = persistentStorageFileStream.GetInputStreamAt(0U).AsStreamForRead())
        {
          using (var cryptoStream = new CryptoStream(fileInputStream, new FromBase64Transform(), CryptoStreamMode.Read))
          {
            using (var decodedFileReader = new StreamReader(cryptoStream))
            {
              if (decodedFileReader.EndOfStream)
              {
                return decryptedStream;
              }

              string plaintext = await decodedFileReader.ReadToEndAsync();
              await fileInputStream.FlushAsync();
              await cryptoStream.FlushAsync();
              byte[] plaintextBuffer = Encoding.UTF8.GetBytes(plaintext);
              decryptedStream = new MemoryStream(plaintextBuffer);
              decryptedStream.Seek(0L, SeekOrigin.Begin);
            }
          }
        }
      }

      return decryptedStream;
    }

    private MemoryStream SerializeCurrentPersistentData()
    {
      var serializedPlaintextStream = new MemoryStream();
      using (XmlDictionaryWriter xmlDictionaryWriter = XmlDictionaryWriter.CreateTextWriter(serializedPlaintextStream, Encoding.UTF8, false))
      {
        var serializer = new DataContractSerializer(
          typeof(PersistentDataObject),
          new[]
          {
            typeof(Playlist), typeof(List<AudioPart>), typeof(AudioPart), typeof(Audiobook), typeof(AudioFile), typeof(HttpMediaStream),
            typeof(HttpMediaTag), typeof(AudioMediaTag), typeof(Bookmark),
            typeof(ObservableCollection<Bookmark>), typeof(PlaylistNavigationInfo),
            typeof(Dictionary<string, string>), typeof(bool),
            typeof(TimeSpan), typeof((TimeSpan BeginTime, TimeSpan EndTime)), typeof(uint), typeof(int)
          });
        serializer.WriteObject(xmlDictionaryWriter, this.PersistentDataObject);
        serializedPlaintextStream.Flush();
      }

      serializedPlaintextStream.Seek(0, SeekOrigin.Begin);
      return serializedPlaintextStream;
    }

    private PersistentDataObject DeserializeCurrentPersistentData(MemoryStream serializedPlaintextStream)
    {
      using (XmlDictionaryReader xmlDictionaryReader = XmlDictionaryReader.CreateTextReader(serializedPlaintextStream, new XmlDictionaryReaderQuotas()))
      {
        var serializer = new DataContractSerializer(typeof(PersistentDataObject),
          new[]
          {
            typeof(Playlist), typeof(List<AudioPart>), typeof(AudioPart), typeof(Audiobook), typeof(AudioFile), typeof(HttpMediaStream),
            typeof(HttpMediaTag), typeof(AudioMediaTag), typeof(Bookmark),
            typeof(ObservableCollection<Bookmark>), typeof(PlaylistNavigationInfo),
            typeof(Dictionary<string, string>), typeof(bool),
            typeof(TimeSpan), typeof((TimeSpan BeginTime, TimeSpan EndTime)), typeof(uint), typeof(int)
          });
        return (PersistentDataObject) serializer.ReadObject(xmlDictionaryReader, false) ?? PersistentDataObject.NullObject;
      }
    }

    private async Task<bool> TryInitialize()
    {
      try
      {
        this.PersistentDataFile = await ApplicationData.Current.RoamingFolder.CreateFileAsync(PersistentDataController.PersistentDataStorageFileFullPath, CreationCollisionOption.ReplaceExisting);
      }
      catch (Exception)
      {
        return false;
      }
    

      this.IsDataObjectOutdated = true;
      return true;
    }
  }
}