using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Pipelines.GetMediaCreatorOptions;
using Sitecore.Resources.Media;
using Sitecore.SecurityModel;
using System;
using System.IO;

namespace Sitecore.Support.Resources.Media
{
  public class MediaCreator
  {
    protected const string DefaultSite = "shell";

    public static string GetFileBasedStreamPath(string itemPath, string filePath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(itemPath, "itemPath");
      Assert.ArgumentNotNull(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      return GetOutputFilePath(itemPath, filePath, options);
    }

    public virtual Item AttachStreamToMediaItem(Stream stream, string itemPath, string fileName, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(stream, "stream");
      Assert.ArgumentNotNullOrEmpty(fileName, "fileName");
      Assert.ArgumentNotNull(options, "options");
      Assert.ArgumentNotNull(itemPath, "itemPath");
      Sitecore.Resources.Media.Media media = MediaManager.GetMedia(CreateItem(itemPath, fileName, options));
      media.SetStream(stream, FileUtil.GetExtension(fileName));
      return media.MediaData.MediaItem;
    }

    public virtual MediaItem CreateFromFile(string filePath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNullOrEmpty(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      string path = FileUtil.MapPath(filePath);
      using (new SecurityDisabler())
      {
        using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
          return CreateFromStream(stream, filePath, options);
        }
      }
    }

    public virtual Item CreateFromFolder(string folderPath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNullOrEmpty(folderPath, "folderPath");
      Assert.ArgumentNotNull(options, "options");
      string itemPath = GetItemPath(folderPath, options);
      return CreateFolder(itemPath, options);
    }

    public virtual Item CreateFromStream(Stream stream, string filePath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(stream, "stream");
      Assert.ArgumentNotNull(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      return CreateFromStream(stream, filePath, true, options);
    }

    public virtual Item CreateFromStream(Stream stream, string filePath, bool setStreamIfEmpty, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(stream, "stream");
      Assert.ArgumentNotNullOrEmpty(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      string itemPath = GetItemPath(filePath, options);
      return AttachStreamToMediaItem(stream, itemPath, filePath, options);
    }

    public virtual void FileCreated(string filePath)
    {
      Assert.ArgumentNotNullOrEmpty(filePath, "filePath");
      SetContext();
      lock (FileUtil.GetFileLock(filePath))
      {
        if (FileUtil.IsFolder(filePath))
        {
          MediaCreatorOptions empty = MediaCreatorOptions.Empty;
          empty.Build(GetMediaCreatorOptionsArgs.FileBasedContext);
          CreateFromFolder(filePath, empty);
        }
        else
        {
          MediaCreatorOptions empty2 = MediaCreatorOptions.Empty;
          long length = new FileInfo(filePath).Length;
          empty2.FileBased = (length > Settings.Media.MaxSizeInDatabase || Settings.Media.UploadAsFiles);
          empty2.Build(GetMediaCreatorOptionsArgs.FileBasedContext);
          CreateFromFile(filePath, empty2);
        }
      }
    }

    public virtual void FileDeleted(string filePath)
    {
      Assert.ArgumentNotNullOrEmpty(filePath, "filePath");
    }

    public virtual void FileRenamed(string filePath, string oldFilePath)
    {
      Assert.ArgumentNotNullOrEmpty(filePath, "filePath");
      Assert.ArgumentNotNullOrEmpty(oldFilePath, "oldFilePath");
      SetContext();
      lock (FileUtil.GetFileLock(filePath))
      {
        MediaCreatorOptions empty = MediaCreatorOptions.Empty;
        empty.Build(GetMediaCreatorOptionsArgs.FileBasedContext);
        string itemPath = GetItemPath(oldFilePath, empty);
        Item item = GetDatabase(empty).GetItem(itemPath);
        if (item != null)
        {
          string fileName = FileUtil.GetFileName(GetItemPath(filePath, empty));
          string extension = FileUtil.GetExtension(filePath);
          using (new EditContext(item, SecurityCheck.Disable))
          {
            item.Name = fileName;
            item["extension"] = extension;
          }
        }
      }
    }

    public virtual string GetItemPath(string filePath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      if (!string.IsNullOrEmpty(options.Destination))
      {
        return options.Destination;
      }
      string text = FileUtil.SubtractPath(filePath, Settings.MediaFolder);
      Assert.IsNotNull(text, typeof(string), "File based media must be located beneath the media folder: '{0}'. Current file: {1}", Settings.MediaFolder, filePath);
      int num = text.LastIndexOf('.');
      if (num < text.LastIndexOf('\\'))
      {
        num = -1;
      }
      bool flag = FileUtil.IsFolder(filePath);
      if (num >= 0 && !flag)
      {
        string str = string.Empty;
        if (options.IncludeExtensionInItemName)
        {
          str = Settings.Media.WhitespaceReplacement + StringUtil.Mid(text, num + 1).ToLowerInvariant();
        }
        text = StringUtil.Left(text, num) + str;
      }
      Assert.IsNotNullOrEmpty(text, "The relative path of a media to create is empty. Original file path: '{0}'.", filePath);
      return Assert.ResultNotNull(MediaPathManager.ProposeValidMediaPath(FileUtil.MakePath("/sitecore/media library", text.Replace('\\', '/')), flag));
    }

    public virtual string GetMediaStorageFolder(ID itemID, string fullPath)
    {
      Assert.IsNotNull(itemID, "itemID is null");
      Assert.IsNotNullOrEmpty(fullPath, "fullPath is empty");
      string fileName = FileUtil.GetFileName(fullPath);
      string text = itemID.ToString();
      return string.Format("/{0}/{1}/{2}/{3}{4}", text[1], text[2], text[3], text, fileName);
    }

    protected virtual Item CreateFolder(string itemPath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNullOrEmpty(itemPath, "itemPath");
      Assert.ArgumentNotNull(options, "options");
      using (new SecurityDisabler())
      {
        using (new LanguageSwitcher(options.Language))
        {
          TemplateItem folderTemplate = GetFolderTemplate(options);
          Database database = GetDatabase(options);
          Item item = database.GetItem(itemPath, options.Language);
          if (item != null)
          {
            return item;
          }
          Item item2 = database.CreateItemPath(itemPath, folderTemplate, folderTemplate);
          Assert.IsNotNull(item2, typeof(Item), "Could not create media folder: '{0}'.", itemPath);
          return item2;
        }
      }
    }

    protected virtual Item CreateItem(string itemPath, string filePath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNullOrEmpty(itemPath, "itemPath");
      Assert.ArgumentNotNullOrEmpty(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      Item item = null;
      using (new SecurityDisabler())
      {
        Database database = GetDatabase(options);
        Item item2 = options.OverwriteExisting ? database.GetItem(itemPath, options.Language) : null;
        Item parentFolder = GetParentFolder(itemPath, options);
        string itemName = GetItemName(itemPath);
        if (item2 != null && !item2.HasChildren && item2.TemplateID != TemplateIDs.MediaFolder)
        {
          item = item2;
          item.Versions.RemoveAll(true);
          item = item.Database.GetItem(item.ID, item.Language, Sitecore.Data.Version.Latest);
          Assert.IsNotNull(item, "item");
          item.Editing.BeginEdit();
          foreach (Field field in item.Fields)
          {
            field.Reset();
          }
          item.Editing.EndEdit();
          item.Editing.BeginEdit();
          item.Name = itemName;
          item.TemplateID = GetItemTemplate(filePath, options).ID;
          item.Editing.EndEdit();
        }
        else
        {
          item = parentFolder.Add(itemName, GetItemTemplate(filePath, options));
        }
        Assert.IsNotNull(item, typeof(Item), "Could not create media item: '{0}'.", itemPath);
        Language[] itemMediaLanguages = GetItemMediaLanguages(options, item);
        string extension = FileUtil.GetExtension(filePath);
        Language[] array = itemMediaLanguages;
        foreach (Language language in array)
        {
          MediaItem mediaItem = item.Database.GetItem(item.ID, language, Sitecore.Data.Version.Latest);
          if (mediaItem != null)
          {
            using (new EditContext(mediaItem, SecurityCheck.Disable))
            {
              mediaItem.Extension = StringUtil.GetString(mediaItem.Extension, extension);
              mediaItem.FilePath = GetFullFilePath(item.ID, filePath, itemPath, options);
              mediaItem.Alt = StringUtil.GetString(mediaItem.Alt, options.AlternateText);
              mediaItem.InnerItem.Statistics.UpdateRevision();
            }
          }
        }
      }
      item.Reload();
      return item;
    }

    protected virtual Language[] GetItemMediaLanguages(MediaCreatorOptions options, Item item)
    {
      Assert.ArgumentNotNull(options, "options");
      Assert.ArgumentNotNull(item, "item");
      Assert.Required(item.Database, "item.Database");
      return new Language[1]
      {
            item.Language
      };
    }

    protected virtual string GetFullFilePath(ID itemID, string fileName, string itemPath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(itemID, "itemID");
      Assert.ArgumentNotNull(fileName, "fileName");
      Assert.ArgumentNotNull(itemPath, "itemPath");
      Assert.ArgumentNotNull(options, "options");
      return GetOutputFilePath(itemPath, GetMediaStorageFolder(itemID, fileName), options);
    }

    private static string GetOutputFilePath(string itemPath, string filePath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(itemPath, "itemPath");
      Assert.ArgumentNotNull(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      if (!options.FileBased)
      {
        return string.Empty;
      }
      if (!string.IsNullOrEmpty(options.OutputFilePath))
      {
        return options.OutputFilePath;
      }
      string extension = FileUtil.GetExtension(filePath);
      string text = FileUtil.GetFileName(filePath);
      if (extension.Length > 0)
      {
        text = text.Substring(0, text.Length - extension.Length - 1);
      }
      return MediaPathManager.GetMediaFilePath(string.Format("{0}/{1}", FileUtil.GetParentPath(filePath), text), extension);
    }

    private Database GetDatabase(MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(options, "options");
      return Assert.ResultNotNull(options.Database ?? Context.ContentDatabase ?? Context.Database);
    }

    private TemplateItem GetFolderTemplate(MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(options, "options");
      TemplateItem templateItem = GetDatabase(options).Templates[TemplateIDs.MediaFolder];
      Assert.IsNotNull(templateItem, typeof(TemplateItem), "Could not find folder template for media. Template: '{0}'", TemplateIDs.MediaFolder.ToString());
      return templateItem;
    }

    private string GetItemName(string itemPath)
    {
      Assert.ArgumentNotNull(itemPath, "itemPath");
      string lastPart = StringUtil.GetLastPart(itemPath, '/', string.Empty);
      if (string.IsNullOrEmpty(lastPart))
      {
        if (!Settings.Media.IncludeExtensionsInItemNames)
        {
          return "unnamed";
        }
        throw new InvalidOperationException("Invalid item path for media item: " + itemPath);
      }
      return lastPart;
    }

    private TemplateItem GetItemTemplate(string filePath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(filePath, "filePath");
      Assert.ArgumentNotNull(options, "options");
      string extension = FileUtil.GetExtension(filePath);
      string template = MediaManager.Config.GetTemplate(extension, options.Versioned);
      Assert.IsNotNullOrEmpty(template, "Could not find template for extension '{0}' (versioned: {1}).", extension, options.Versioned);
      TemplateItem templateItem = GetDatabase(options).Templates[template];
      Assert.IsNotNull(templateItem, typeof(TemplateItem), "Could not find item template for media. Template: '{0}'", template);
      return templateItem;
    }

    private Item GetParentFolder(string itemPath, MediaCreatorOptions options)
    {
      Assert.ArgumentNotNull(itemPath, "itemPath");
      Assert.ArgumentNotNull(options, "options");
      string[] array = StringUtil.Divide(itemPath, '/', true);
      string itemPath2 = (array.Length > 1) ? array[0] : "/sitecore/media library";
      return CreateFolder(itemPath2, options);
    }

    private void SetContext()
    {
      if (Context.Site == null)
      {
        Context.SetActiveSite("shell");
      }
    }
  }
}