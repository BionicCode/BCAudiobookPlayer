using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BCAudiobookPlayer.ResourceProvider
{
  public static class ApplicationResourceController
  {
    public static ConcurrentDictionary<string, string> MostRecentlyUsedListFileSystemPathToTokenMap { get; private set; }

    public static ConcurrentDictionary<string, string> FutureAccessListFileSystemPathToTokenMap { get; private set; }
    //internal static DataStorageController DataStorageController { get; }

    static ApplicationResourceController()
    {
      ApplicationResourceController.MostRecentlyUsedListFileSystemPathToTokenMap = new ConcurrentDictionary<string, string>();
      ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap = new ConcurrentDictionary<string, string>();
      //ApplicationResourceController.DataStorageController = new DataStorageController();
    }

    public static void UpdateMostRecentUsedFilesTokenMap(IDictionary<string, string> tokenMap)
    {
      if (tokenMap != null)
      {
        ApplicationResourceController.MostRecentlyUsedListFileSystemPathToTokenMap = new ConcurrentDictionary<string, string>(tokenMap);
      }
    }

    public static void UpdateFutureAccessTokenMap(IDictionary<string, string> tokenMap)
    {
      if (tokenMap != null)
      {
        ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap = new ConcurrentDictionary<string, string>(tokenMap);
      }
    }
  }
}