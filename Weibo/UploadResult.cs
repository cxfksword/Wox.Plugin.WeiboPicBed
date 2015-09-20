using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.WeiboPicBed
{
    public class UploadResult
    {
        public bool Success { get; set; }
        public string Msg { get; set; }
        public string PicId { get; set; }
        public string PicUrl { get; set; }
        public string MiddlePicUrl { get; set; }
        public string ThumbnailPicUrl { get; set; }
        public long UploadAt { get; set; }
    }
}
