using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Pipelines.GetMediaCreatorOptions;
using Sitecore.Resources.Media;
using Sitecore.Support.Resources.Media;
using Sitecore.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web;

namespace Sitecore.Support.Resources.Media
{
  public class MediaUploader
  {
    private Sitecore.Support.Resources.Media.MediaCreator mediaCreator;
   
    private string _alternateText;

    private HttpPostedFile _file;

    private Language _language;

    private string _folder = string.Empty;

    public string AlternateText
    {
      get
      {
        return _alternateText;
      }
      set
      {
        _alternateText = value;
      }
    }

    public HttpPostedFile File
    {
      get
      {
        return _file;
      }
      set
      {
        Assert.ArgumentNotNull(value, "value");
        _file = value;
      }
    }

    public string Folder
    {
      get
      {
        return _folder;
      }
      set
      {
        Assert.ArgumentNotNull(value, "value");
        _folder = value;
      }
    }

    public bool Unpack
    {
      get;
      set;
    }

    public bool Versioned
    {
      get;
      set;
    }

    public Language Language
    {
      get
      {
        return _language;
      }
      set
      {
        Assert.ArgumentNotNull(value, "value");
        _language = value;
      }
    }

    public bool Overwrite
    {
      get;
      set;
    }

    public bool FileBased
    {
      get;
      set;
    }

    public Database Database
    {
      get;
      set;
    }

    public MediaUploader()
    {
      mediaCreator = new Sitecore.Support.Resources.Media.MediaCreator();
    }

    public List<Sitecore.Support.Resources.Media.MediaUploadResult> Upload()
    {
      List<Sitecore.Support.Resources.Media.MediaUploadResult> list = new List<Sitecore.Support.Resources.Media.MediaUploadResult>();
      if (string.Compare(Path.GetExtension(File.FileName), ".zip", StringComparison.InvariantCultureIgnoreCase) == 0 && Unpack)
      {
        UnpackToDatabase(list);
      }
      else
      {
        UploadToDatabase(list);
      }
      return list;
    }

    private void UploadToDatabase(List<Sitecore.Support.Resources.Media.MediaUploadResult> list)
    {
      Assert.ArgumentNotNull(list, "list");
      Sitecore.Support.Resources.Media.MediaUploadResult mediaUploadResult = new Sitecore.Support.Resources.Media.MediaUploadResult();
      list.Add(mediaUploadResult);
      mediaUploadResult.Path = FileUtil.MakePath(Folder, Path.GetFileName(File.FileName), '/');
      mediaUploadResult.ValidMediaPath = MediaPathManager.ProposeValidMediaPath(mediaUploadResult.Path);
      MediaCreatorOptions mediaCreatorOptions = new MediaCreatorOptions
      {
        Versioned = Versioned,
        Language = Language,
        OverwriteExisting = Overwrite,
        Destination = mediaUploadResult.ValidMediaPath,
        FileBased = FileBased,
        AlternateText = AlternateText,
        Database = Database
      };
      mediaCreatorOptions.Build(GetMediaCreatorOptionsArgs.UploadContext);
      mediaUploadResult.Item = MediaManager.Creator.CreateFromStream(File.InputStream, mediaUploadResult.Path, mediaCreatorOptions);
    }

    private void UnpackToDatabase(List<Sitecore.Support.Resources.Media.MediaUploadResult> list)
    {
      Assert.ArgumentNotNull(list, "list");
      string text = FileUtil.MapPath(TempFolder.GetFilename("temp.zip"));
      File.SaveAs(text);
      try
      {
        using (ZipReader zipReader = new ZipReader(text))
        {
          foreach (ZipEntry entry in zipReader.Entries)
          {
            if (!entry.IsDirectory)
            {
              Sitecore.Support.Resources.Media.MediaUploadResult mediaUploadResult = new Sitecore.Support.Resources.Media.MediaUploadResult();
              list.Add(mediaUploadResult);
              mediaUploadResult.Path = FileUtil.MakePath(Folder, entry.Name, '/');
              mediaUploadResult.ValidMediaPath = MediaPathManager.ProposeValidMediaPath(mediaUploadResult.Path);
              MediaCreatorOptions mediaCreatorOptions = new MediaCreatorOptions
              {
                Language = Language,
                Versioned = Versioned,
                OverwriteExisting = Overwrite,
                Destination = mediaUploadResult.ValidMediaPath,
                FileBased = FileBased,
                Database = Database
              };
              mediaCreatorOptions.Build(GetMediaCreatorOptionsArgs.UploadContext);
              Stream stream = entry.GetStream();
              mediaUploadResult.Item = mediaCreator.CreateFromStream(stream, mediaUploadResult.Path, mediaCreatorOptions);
            }
          }
        }
      }
      finally
      {
        FileUtil.Delete(text);
      }
    }
  }
}