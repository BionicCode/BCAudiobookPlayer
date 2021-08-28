using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.UI.Xaml.Shapes;

namespace BCAudiobookPlayer.ResourceProvider
{
  public static class FileController
  {
    public static async Task<IEnumerable<StorageFile>> RegisterFolder(
      StorageFolder folder,
      bool flattenDirectory = false,
      bool addToFutureAccessList = true,
      bool addToRecentFilesList = true)
    {
      if (folder == null)
      {
        return new List<StorageFile>();
      }

      if (addToFutureAccessList)
      {
        FileController.AddStorageItemToFutureAccessList(folder);
      }

      if (addToRecentFilesList)
      {
        await FileController.AddStorageItemToMostRecentFilesListAsync(folder);
      }

      IEnumerable<StorageFile> pickedFolderContent = flattenDirectory
        ? await Task.Run(async () => await FileController.FlattenDirectoryAsync(folder))
        : await folder.GetFilesAsync(CommonFileQuery.OrderByName);

      // Register files for future access in background
      //TaskScheduler synchronizationContext = TaskScheduler.FromCurrentSynchronizationContext();
      //var lazyTask = new Lazy<Task>(() => new Task(() =>
      //  {
      //    Parallel.ForEach(
      //      pickedFolderContent.Skip(1).ToList(),
      //      new ParallelOptions() {TaskScheduler = synchronizationContext},
      //      (folderContent) => FileController.RegisterFileAsync(folderContent, addToFutureAccessList, addToRecentFilesList));
      //  }, CancellationToken.None, TaskCreationOptions.LongRunning));

      return pickedFolderContent;
    }

    public static async Task<string> RegisterFileAsync(
      StorageFile file,
      bool addToFutureAccessList = true,
      bool addToRecentFilesList = true)
    {
      if (file == null)
      {
        return string.Empty;
      }

      string accessPermissionToken = string.Empty;
      if (addToRecentFilesList)
      {
        accessPermissionToken = await FileController.AddStorageItemToMostRecentFilesListAsync(file);
      }

      if (addToFutureAccessList)
      {
        accessPermissionToken = FileController.AddStorageItemToFutureAccessList(file);
      }

      return accessPermissionToken;
    }

    private static async Task<IEnumerable<StorageFile>> FlattenDirectoryAsync(
      StorageFolder folder,
      bool addToFutureAccessList = true,
      bool addToRecentFilesList = true)
    {
      var flattenedFolderFiles = new List<StorageFile>();
      IReadOnlyList<IStorageItem> pickedFolderContent = await folder.GetItemsAsync();
      foreach (IStorageItem storageItem in pickedFolderContent)
      {
        if (storageItem is StorageFile storageFile)
        {
          await FileController.RegisterFileAsync(storageFile, addToFutureAccessList, addToRecentFilesList);
          flattenedFolderFiles.Add(storageFile);
        }
        else if (storageItem is StorageFolder storageFolder)
        {
          flattenedFolderFiles.AddRange(
            await FileController.FlattenDirectoryAsync(storageFolder, addToFutureAccessList, addToRecentFilesList));
        }
      }
      return flattenedFolderFiles;
    }

    private static async Task<string> AddStorageItemToMostRecentFilesListAsync(IStorageItem pickedFile)
    {
      if (!ApplicationResourceController.MostRecentlyUsedListFileSystemPathToTokenMap.TryGetValue(
        pickedFile.Path,
        out string accessPermissionToken))
      {
        accessPermissionToken = StorageApplicationPermissions.MostRecentlyUsedList.Add(pickedFile);

        var newFilePathTokenMap = new Dictionary<string, string>();
        foreach (AccessListEntry accessListEntry in StorageApplicationPermissions.MostRecentlyUsedList.Entries)
        {
          try
          {
            IStorageItem storageItem = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(accessListEntry.Token);

            string filePath = storageItem.Path;
            if (!newFilePathTokenMap.ContainsKey(filePath))
            {
              newFilePathTokenMap.Add(filePath, accessListEntry.Token);
            }
          }
          catch (FileNotFoundException)
          {
            StorageApplicationPermissions.MostRecentlyUsedList.Remove(accessListEntry.Token);
          }
        }

        ApplicationResourceController.UpdateMostRecentUsedFilesTokenMap(newFilePathTokenMap);
      }

      return accessPermissionToken;
    }

    private static string AddStorageItemToFutureAccessList(IStorageItem pickedFile)
    {
      if (!ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap.TryGetValue(pickedFile.Path, out string accessPermissionToken))
      {
        if (StorageApplicationPermissions.FutureAccessList.MaximumItemsAllowed == StorageApplicationPermissions.FutureAccessList.Entries.Count)
        {
          string lastFileToken = StorageApplicationPermissions.FutureAccessList.Entries
            .SkipWhile((entry, index) => StorageApplicationPermissions.MostRecentlyUsedList.ContainsItem(entry.Token))
            .Take(1)
            .FirstOrDefault()
            .Token;

          StorageApplicationPermissions.FutureAccessList.Remove(lastFileToken);
          ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap.TryRemove(pickedFile.Path, out lastFileToken);
        }

        accessPermissionToken = StorageApplicationPermissions.FutureAccessList.Add(pickedFile);
        ApplicationResourceController.FutureAccessListFileSystemPathToTokenMap.TryAdd(
          pickedFile.Path,
          accessPermissionToken);
      }

      return accessPermissionToken;
    }
  }
}
