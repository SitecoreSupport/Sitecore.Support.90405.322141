using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.IO;
using Sitecore.Pipelines.Upload;
using Sitecore.Support.Resources.Media;
using Sitecore.SecurityModel;
using Sitecore.Web;
using Sitecore.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;


namespace Sitecore.Support.Pipelines.Upload
{


  public class Save : UploadProcessor
  {
    public void Process(UploadArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      for (int i = 0; i < args.Files.Count; i++)
      {
        HttpPostedFile httpPostedFile = args.Files[i];
        if (!string.IsNullOrEmpty(httpPostedFile.FileName))
        {
          try
          {
            bool flag = UploadProcessor.IsUnpack(args, httpPostedFile);
            if (args.FileOnly)
            {
              if (flag)
              {
                UnpackToFile(args, httpPostedFile);
              }
              else
              {
                string filename = UploadToFile(args, httpPostedFile);
                if (i == 0)
                {
                  args.Properties["filename"] = FileHandle.GetFileHandle(filename);
                }
              }
            }
            else
            {
              MediaUploader mediaUploader = new MediaUploader
              {
                File = httpPostedFile,
                Unpack = flag,
                Folder = args.Folder,
                Versioned = args.Versioned,
                Language = args.Language,
                AlternateText = args.GetFileParameter(httpPostedFile.FileName, "alt"),
                Overwrite = args.Overwrite,
                FileBased = (args.Destination == UploadDestination.File)
              };
              List<MediaUploadResult> list;
              using (new SecurityDisabler())
              {
                list = mediaUploader.Upload();
              }
              Log.Audit(this, "Upload: {0}", httpPostedFile.FileName);
              foreach (MediaUploadResult item in list)
              {
                if (mediaUploader.Overwrite)
                {
                  string value = item.Item.Fields[FieldIDs.DefaultWorkflow].Value;
                  if (!string.IsNullOrEmpty(value))
                  {
                    Context.ContentDatabase.WorkflowProvider.GetWorkflow(value).Start(item.Item);
                  }
                }
                ProcessItem(args, item.Item, item.Path);
              }
            }
          }
          catch (Exception exception)
          {
            Log.Error("Could not save posted file: " + httpPostedFile.FileName, exception, this);
            throw;
          }
        }
      }
    }

    private void ProcessItem(UploadArgs args, MediaItem mediaItem, string path)
    {
      Assert.ArgumentNotNull(args, "args");
      Assert.ArgumentNotNull(mediaItem, "mediaItem");
      Assert.ArgumentNotNull(path, "path");
      if (args.Destination == UploadDestination.Database)
      {
        Log.Info("Media Item has been uploaded to database: " + path, this);
      }
      else
      {
        Log.Info("Media Item has been uploaded to file system: " + path, this);
      }
      args.UploadedItems.Add(mediaItem.InnerItem);
    }

    private static void UnpackToFile(UploadArgs args, HttpPostedFile file)
    {
      Assert.ArgumentNotNull(args, "args");
      Assert.ArgumentNotNull(file, "file");
      string filename = FileUtil.MapPath(TempFolder.GetFilename("temp.zip"));
      file.SaveAs(filename);
      using (ZipReader zipReader = new ZipReader(filename))
      {
        foreach (ZipEntry entry in zipReader.Entries)
        {
          if (Path.GetInvalidFileNameChars().Any((char ch) => entry.Name.Contains(ch)))
          {
            string text = string.Format("The \"{0}\" file was not uploaded because it contains malicious file: \"{1}\"", file.FileName, entry.Name);
            Log.Warn(text, typeof(Sitecore.Support.Pipelines.Upload.Save));
            args.UiResponseHandlerEx.MaliciousFile(StringUtil.EscapeJavascriptString(file.FileName));
            args.ErrorText = text;
            args.AbortPipeline();
            return;
          }
        }
        foreach (ZipEntry entry2 in zipReader.Entries)
        {
          string text2 = FileUtil.MakePath(args.Folder, entry2.Name, '\\');
          if (entry2.IsDirectory)
          {
            Directory.CreateDirectory(text2);
          }
          else
          {
            if (!args.Overwrite)
            {
              text2 = FileUtil.GetUniqueFilename(text2);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(text2));
            lock (FileUtil.GetFileLock(text2))
            {
              FileUtil.CreateFile(text2, entry2.GetStream(), true);
            }
          }
        }
      }
    }

    private string UploadToFile(UploadArgs args, HttpPostedFile file)
    {
      Assert.ArgumentNotNull(args, "args");
      Assert.ArgumentNotNull(file, "file");
      string text = FileUtil.MakePath(args.Folder, Path.GetFileName(file.FileName), '\\');
      if (!args.Overwrite)
      {
        text = FileUtil.GetUniqueFilename(text);
      }
      file.SaveAs(text);
      Log.Info("File has been uploaded: " + text, this);
      return Assert.ResultNotNull(text);
    }
  }

}