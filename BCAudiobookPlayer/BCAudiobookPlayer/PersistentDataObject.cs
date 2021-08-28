using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using BCAudiobookPlayer.Player.Playback;
using BCAudiobookPlayer.Player.Playback.Contract;

namespace BCAudiobookPlayer
{
  [DataContract]
  public class PersistentDataObject
  {
    public static PersistentDataObject NullObject => new PersistentDataObject(true);

    public PersistentDataObject()
    {
      this.Playlist = new Playlist();
      this.FutureAccessTokenMap = new Dictionary<string, string>();
      this.MostRecentUsedFilesTokenMap = new Dictionary<string, string>();
      this.IsNull = false;
    }

    private PersistentDataObject(bool isNull) : this()
    {
      this.IsNull = isNull;
    }

    public bool IsNull { get; }

    [DataMember]
    public IPlaylist Playlist { get; set; }

    [DataMember]
    public IDictionary<string, string> FutureAccessTokenMap { get; set; }

    [DataMember]
    public IDictionary<string, string> MostRecentUsedFilesTokenMap { get; set; }
  }
}