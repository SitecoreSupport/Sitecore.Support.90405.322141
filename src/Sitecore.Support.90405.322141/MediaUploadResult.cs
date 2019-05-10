using Sitecore.Data.Items;
using Sitecore.Diagnostics;

namespace Sitecore.Support.Resources.Media
{
  public class MediaUploadResult
  {
    private Item _item;

    private string _path;

    private string _validMediaPath;

    public Item Item
    {
      get
      {
        return _item;
      }
      internal set
      {
        Assert.ArgumentNotNull(value, "value");
        _item = value;
      }
    }

    public string Path
    {
      get
      {
        return _path;
      }
      internal set
      {
        Assert.ArgumentNotNull(value, "value");
        _path = value;
      }
    }

    public string ValidMediaPath
    {
      get
      {
        return _validMediaPath;
      }
      internal set
      {
        Assert.ArgumentNotNull(value, "value");
        _validMediaPath = value;
      }
    }
  }
}