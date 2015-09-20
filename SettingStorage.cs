using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure.Storage;
using System.Text;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;

namespace Wox.Plugin.WeiboPicBed
{
    public class SettingStorage : JsonStrorage<SettingStorage>
    {
        private List<UploadResult> historys = new List<UploadResult>();
        [JsonProperty]
        public List<UploadResult> Historys
        {
            get { return this.historys; }
            set { this.historys = value; }
        }
        [JsonProperty]
        public string Account { get; set; }
        [JsonProperty]
        public string Password { get; set; }

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override string ConfigName
        {
            get { return "setting"; }
        }

        public void AddHistory(UploadResult pic)
        {
            if (Historys == null)
                Historys = new List<UploadResult>();

            Historys.Add(pic);
            if (Historys.Count > 20)
            {
                Historys.RemoveAt(0);
            }
        }
    }
}
