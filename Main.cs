using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Wox.Plugin.WeiboPicBed
{
    class Main : IPlugin, ISettingProvider, IPluginI18n
    {
        private PluginInitContext context;
        private static readonly string url = "http://picupload.service.weibo.com/interface/pic_upload.php?mime=image%2Fjpeg&data=base64&url=0&markpos=1&logo=&nick=0&marks=1&app=miniblog";
        public static string PluginDirectory;
        private static Weibo weibo;
        private static System.Threading.Timer keepLoginTimer;
        private static readonly object syncObj = new object();
        const int TimerInterval = 60000;

        public void Init(PluginInitContext context)
        {
            this.context = context;
            PluginDirectory = context.CurrentPluginMetadata.PluginDirectory;

            weibo = new Weibo();
            keepLoginTimer = new System.Threading.Timer(new TimerCallback(this.KeepLoginCallBack), null, Timeout.Infinite, Timeout.Infinite);

        }

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            return new Settings(context.API);
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_weibopicbed_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_weibopicbed_plugin_description");
        }

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
        }

        public List<Result> Query(Query query)
        {
            var list = new List<Result>();

            if (query.FirstSearch == "upload")
            {
                if (string.IsNullOrEmpty(SettingStorage.Instance.Account) || string.IsNullOrEmpty(SettingStorage.Instance.Password))
                {
                    var msg = context.API.GetTranslation("wox_plugin_weibopicbed_not_input_setttings");
                    this.context.API.ShowMsg(GetTranslatedPluginTitle(), msg);
                    return list;
                }

                var uploadDlg = new Microsoft.Win32.OpenFileDialog();

                uploadDlg.DefaultExt = ".jpeg";
                uploadDlg.Filter = "All Files (*.*)|*.*";

                var result = uploadDlg.ShowDialog();
                if (result.Value)
                {
                    var ext = Path.GetExtension(uploadDlg.FileName).ToLower();
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif" && ext != ".bmp")
                    {
                        this.context.API.ShowMsg(GetTranslatedPluginTitle(), context.API.GetTranslation("wox_plugin_weibopicbed_image_not_support"));
                        return list;
                    }

                    this.context.API.ShowMsg(GetTranslatedPluginTitle(), context.API.GetTranslation("wox_plugin_weibopicbed_start_upload") + " " + Path.GetFileName(uploadDlg.FileName) + ".");
                    this.context.API.ShowApp();
                    this.context.API.StartLoadingBar();

                    var uploadResult = Upload(uploadDlg.FileName);
                    this.context.API.StopLoadingBar();
                    this.context.API.ShowApp();
                    if (!uploadResult.Success)
                    {
                        Logger.Error(uploadResult.Msg);
                        this.context.API.ShowMsg(GetTranslatedPluginTitle(), context.API.GetTranslation("wox_plugin_weibopicbed_upload_failed"));
                        return list;
                    }

                    var filename = uploadDlg.FileName;
                    var icon = GetImageBase64(uploadResult.ThumbnailPicUrl);
                    list.Add(new Result()
                    {
                        IcoPath = icon,
                        Title = uploadResult.PicUrl,
                        SubTitle = "Copy to clipboard",
                        Action = (c) =>
                        {
                            Clipboard.SetText(uploadResult.PicUrl);
                            return true;
                        }
                    });
                    list.Add(new Result()
                    {
                        IcoPath = icon,
                        Title = string.Format("[Markdown] ![]({0})", uploadResult.PicUrl),
                        SubTitle = "Copy to clipboard",
                        Action = (c) =>
                        {
                            Clipboard.SetText(string.Format("![]({0})", uploadResult.PicUrl));
                            return true;
                        }
                    });
                    list.Add(new Result()
                    {
                        IcoPath = icon,
                        Title = string.Format("[HTML] <img src=\"{0}\"/>", uploadResult.PicUrl),
                        SubTitle = "Copy to clipboard",
                        Action = (c) =>
                        {
                            Clipboard.SetText(string.Format("<img src=\"{0}\"/>", uploadResult.PicUrl));
                            return true;
                        }
                    });
                    list.Add(new Result()
                    {
                        IcoPath = icon,
                        Title = string.Format("[UBB] [IMG]{0}[/IMG]", uploadResult.PicUrl),
                        SubTitle = "Copy to clipboard",
                        Action = (c) =>
                        {
                            Clipboard.SetText(string.Format("[IMG]{0}[/IMG]", uploadResult.PicUrl));
                            return true;
                        }
                    });

                    SettingStorage.Instance.AddHistory(uploadResult);
                    SettingStorage.Instance.Save();
                }
            }

            if (query.FirstSearch == "history")
            {
                if (SettingStorage.Instance.Historys == null || SettingStorage.Instance.Historys.Count <= 0)
                {
                    return list;
                }

                foreach (var history in SettingStorage.Instance.Historys.OrderByDescending(x => x.UploadAt))
                {
                    list.Add(new Result()
                    {
                        IcoPath = GetImageBase64(history.ThumbnailPicUrl),
                        Title = history.PicUrl,
                        SubTitle = Utils.ConvertIntDateTime(history.UploadAt).ToString("yyyy-MM-dd HH:mm:ss") + " - Copy to clipboard",
                        Action = (c) =>
                        {
                            Clipboard.SetText(history.PicUrl);
                            return true;
                        }
                    });
                }
            }

            return list;
        }


        private UploadResult Upload(string filePath)
        {
            var uploadResult = new UploadResult() { Success = false , Msg= context.API.GetTranslation("wox_plugin_weibopicbed_upload_busy") };
            if (Monitor.TryEnter(syncObj, 5000))
            {
                try
                {
                    if (!weibo.IsLogin())
                    {
                        keepLoginTimer.Change(Timeout.Infinite, Timeout.Infinite);

                        var loginResult = weibo.Login(SettingStorage.Instance.Account, Utils.Decrypt(SettingStorage.Instance.Password));
                        if (!loginResult.Success)
                        {
                            var msg = context.API.GetTranslation("wox_plugin_weibopicbed_login_failed");
                            this.context.API.ShowMsg(GetTranslatedPluginTitle(), msg);
                            return new UploadResult() { Success = false, Msg = msg };
                        }

                        uploadResult = weibo.PicUpload(filePath);
                        keepLoginTimer.Change(TimerInterval, TimerInterval);
                    } else
                    {
                        uploadResult = weibo.PicUpload(filePath);
                        keepLoginTimer.Change(TimerInterval, TimerInterval);
                    }
                }
                finally
                {
                    Monitor.Exit(syncObj);
                }
            }

            return uploadResult;
        }
    

        private void KeepLoginCallBack(object state)
        {
            if (Monitor.TryEnter(syncObj, 1000))
            {
                try
                {
                    weibo.KeepAlive();
                }
                finally
                {
                    Monitor.Exit(syncObj);
                }
            }
        }

        private string GetImageBase64(string url)
        {
            try
            {
                var client = new WebClient();
                var imageBytes = client.DownloadData(url);
                string base64String = Convert.ToBase64String(imageBytes);
                return "data:image/jpeg;base64," +　base64String;
            }
            catch (Exception e)
            {
                return "Images/app.png";
            }

        }


    }
}
