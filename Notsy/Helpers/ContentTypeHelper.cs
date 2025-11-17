using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notsy.Helpers
{
    public class ContentTypeHelper
    {
        public string GetFileExtension(string contentType)
        {
            if (contentType.Contains("png")) return ".png";
            if (contentType.Contains("gif")) return ".gif";
            if (contentType.Contains("bmp")) return ".bmp";
            if (contentType.Contains("webp")) return ".webp";
            return ".jpg";
        }
    }
}
