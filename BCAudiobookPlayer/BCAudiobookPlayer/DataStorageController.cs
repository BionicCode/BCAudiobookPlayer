using BCAudiobookPlayer.ResourceProvider;

namespace BCAudiobookPlayer
{
  internal class DataStorageController
  {
    public static void PersistState()
    {
      DataStorageController.PersistentDataController.SaveFutureAccessListTokenMap(ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap);
      DataStorageController.PersistentDataController.SaveMostRecentUsedFilesTokenMap(ApplicationResourceController.MostRecentlyUsedListFileSystemPathToTokenMap);
    }

    public static PersistentDataController PersistentDataController { get; set; } = new PersistentDataController();
  }
}