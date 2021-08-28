using System.Runtime.Serialization;

namespace BCAudiobookPlayer.Player.Playback
{
  [DataContract]
  public enum PlaylistNavigationInfo
  {
    [EnumMember]
    Undefined = 0,

    [EnumMember]
    Previous,

    [EnumMember]
    Next,

    [EnumMember]
    Current,

    [EnumMember]
    Completed
  }
}