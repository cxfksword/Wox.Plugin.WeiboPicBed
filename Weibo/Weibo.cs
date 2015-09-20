using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;

namespace Wox.Plugin.WeiboPicBed
{
    public class Weibo
    {
        const string PicUploadURL = "http://picupload.service.weibo.com/interface/pic_upload.php?app=miniblog&s=json&data=1&url=&markpos=1&logo=0&nick=&marks=1&mime=image%2Fjpeg&ct=0.4171791155822575";
        const string KeepAliveURL = "http://rm.api.weibo.com/2/remind/unread_hint.json?source=3818214747&with_url=1&appkeys=&group_ids=&callback=STK_1442727310789132";


        private string servertime, nonce, rsakv, weibo_rsa_n, prelt;
        private static readonly Regex regCrossDomainUrl = new Regex(@"replace\([\""']([\w\W]+?)[\""']", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex regPicId = new Regex(@"pid"":""(\w+?)""", RegexOptions.IgnoreCase);

        public CookieContainer cookies { set; get; }

        /// <summary>
        /// weibo登录获取Cookies
        /// </summary>
        /// <param name="UserName">用户名</param>
        /// <param name="Password">密码</param>
        /// <returns>Login result</returns>
        public LoginResult Login(string UserName, string Password)
        {
            cookies = new CookieContainer();
            try
            {
                if (GetPreloginStatus(UserName))
                {
                    string login_url = "https://login.sina.com.cn/sso/login.php?client=ssologin.js(v1.4.18)&_=" + Utils.ConvertDateTimeInt(DateTime.Now).ToString();
                    string login_data = "entry=weibo&gateway=1&from=&savestate=7&useticket=1&pagerefer=http%3A%2F%2Fweibo.com%2Fa%2Fdownload&vsnf=1&su=" + get_user(UserName)
                        + "&service=miniblog&servertime=" + servertime + "&nonce=" + nonce + "&pwencode=rsa2&rsakv=" + rsakv + "&sp=" + get_pwa_rsa(Password)
                        + "&encoding=UTF-8&prelt=415&url=http%3A%2F%2Fweibo.com%2Fajaxlogin.php%3Fframelogin%3D1%26callback%3Dparent.sinaSSOController.feedBackUrlCallBack&returntype=META";

                    var postData = new Dictionary<string, string>();
                    foreach(var query in login_data.Split('&'))
                    {
                        var param = query.Split('=');
                        postData.Add(param[0], HttpUtility.UrlDecode(param[1]));
                    }

                    string Content = HttpHelper.GetHttpContent(login_url, postData, cookies);
                    var match = regCrossDomainUrl.Match(Content);
                    if (match.Success)
                    {
                        // http://login.sina.com.cn/crossdomain2.php
                        Content = HttpHelper.GetHttpContent(match.Groups[1].Value, cookies: cookies, referer: login_url);
                        match = regCrossDomainUrl.Match(Content);
                        if (match.Success)
                        {
                            // http://passport.weibo.com/wbsso/login
                            Content = HttpHelper.GetHttpContent(match.Groups[1].Value, cookies: cookies,  referer: login_url);
                        }
                    }
                    BugFix_CookieDomain(cookies);
                    string home_url = "http://weibo.com/p/1005055195947674/info?mod=pedit_more";
                    string result = HttpHelper.GetHttpContent(home_url, cookies: cookies);

                    if (string.IsNullOrEmpty(result) || result.Contains("账号存在异常") || !result.Contains("$CONFIG['islogin']='1'"))
                    {
                        return new LoginResult() {Success = false, Msg = "Fail, Msg: Login fail! Maybe you account is disable or captcha is needed." };
                    }
                }
                else
                    return new LoginResult() { Success = false, Msg = "Error, Msg: The method is out of date, please update!" };
            }
            catch (Exception e)
            {
                return new LoginResult() { Success = false, Msg = "Error, Msg: " + e.ToString() };
            }

            LoginResult loginResult = new LoginResult() { Success = true, Msg = "Success", Cookies = HttpHelper.GetAllCookies(cookies) };

            return loginResult;
        }

        public void KeepAlive()
        {
            var headers = new Dictionary<string, string>();
            headers.Add("X-Requested-With", "XMLHttpRequest");

            HttpHelper.GetHttpContent(
               PicUploadURL,
               cookies: cookies,
               referer: "http://weibo.com/u/3428296462?wvr=5&topnav=1&wvr=5",
               encode: Encoding.GetEncoding("UTF-8"),
               headers: headers);
        }


        public UploadResult PicUpload(string picPath)
        {
            var ext = Path.GetExtension(picPath).ToLower();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif" && ext != ".bmp")
            {
                return new UploadResult() { Success = false, Msg = "只运行.jpg, .png, .gif, .bmp图片格式文件." };
            }

            var headers = new Dictionary<string, string>();
            headers.Add("Origin", "http://js.t.sinajs.cn");

            var content = HttpHelper.GetHttpContent(
                PicUploadURL,
                cookies: cookies,
                headers: headers,
                referer: "http://js.t.sinajs.cn/t5/home/static/swf/MultiFilesUpload.swf?version=559f4bc1f6266504",
                encode: Encoding.GetEncoding("UTF-8"),
                filePath: picPath);

            var match = regPicId.Match(content);
            if (match.Success)
            {
                var result =  new UploadResult() { Success = true };
                var rnd = new Random(Guid.NewGuid().GetHashCode());
                var server = rnd.Next(1, 4);
                result.PicId = match.Groups[1].Value;
                result.PicUrl = string.Format("http://ww{0}.sinaimg.cn/large/{1}.jpg", server, result.PicId);
                result.MiddlePicUrl = string.Format("http://ww{0}.sinaimg.cn/mw690/{1}.jpg", server, result.PicId);
                result.ThumbnailPicUrl = string.Format("http://ww{0}.sinaimg.cn/thumbnail/{1}.jpg", server, result.PicId);
                result.UploadAt = Utils.ConvertDateTimeInt(DateTime.Now);

                return result;
            } else
            {
                Logger.Error(PicUploadURL + "\r\nResult:\r\n" + content);
                return new UploadResult() { Success = false, Msg = "上传失败." };
            }

            
        }

        public bool IsLogin()
        {
            if (cookies == null)
            {
                return false;
            }

            string content = HttpHelper.GetHttpContent(KeepAliveURL, cookies: cookies, referer: "http://weibo.com/u/3428296462?wvr=5&topnav=1&wvr=5");
            var hasLogin = !content.Contains("auth by Null spi");
            if (!hasLogin)
            {
                Logger.Error(KeepAliveURL + "\r\nResult:\r\n" + content);
                cookies = null;
            }

            return hasLogin;
        }

        /// <summary>
        /// 获取登录前状态
        /// </summary>
        /// <param name="UserName">用户名</param>
        /// <returns>是否成功获取</returns>
        private bool GetPreloginStatus(string UserName)
        {
            try
            {
                long timestart = Utils.ConvertDateTimeInt(DateTime.Now);
                string prelogin_url = "http://login.sina.com.cn/sso/prelogin.php?entry=sso&callback=sinaSSOController.preloginCallBack&su=" + get_user(UserName) + "&rsakt=mod&client=ssologin.js(v1.4.18)&_=" + timestart;
                string Content = HttpHelper.GetHttpContent(prelogin_url, cookies: cookies, encode: Encoding.GetEncoding("GB2312"));
                long dateTimeEndPre = Utils.ConvertDateTimeInt(DateTime.Now);

                prelt = Math.Max(dateTimeEndPre - timestart, 50).ToString();
                var prepareJson = JsonConvert.DeserializeObject<PreJsonResult>(Content.Split('(')[1].Split(')')[0]);

                servertime = prepareJson.servertime.ToString();
                nonce = prepareJson.nonce;
                weibo_rsa_n = prepareJson.pubkey;
                rsakv = prepareJson.rsakv;
                return true;
            }
            catch(Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }

        /// <summary>
        /// 获取Base64加密的UserName
        /// </summary>
        /// <param name="UserName"></param>
        /// <returns></returns>
        private string get_user(string UserName)
        {
            UserName = HttpUtility.UrlEncode(UserName, Encoding.UTF8);
            byte[] bytes = Encoding.Default.GetBytes(UserName);
            return HttpUtility.UrlEncode(Convert.ToBase64String(bytes), Encoding.UTF8);
        }

        /// <summary>
        /// 获取RSA加密后的密码密文
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        private string get_pwa_rsa(string password)
        {
            RSAHelper rsa = new RSAHelper();
            rsa.SetPublic(weibo_rsa_n, "10001");
            string data = servertime + "\t" + nonce + "\n" + password;
            return rsa.Encrypt(data).ToLower();
        }

        /// <summary>
        /// 对于根域，CookieContainer只会传递根域cookie，不会传.根域cookie，
        /// 如请求http://weibo.com，只传weibo.com，不传.weibo.com的cookie
        /// 和实际浏览器有差别
        /// </summary>
        /// <param name="cookieContainer"></param>
        private void BugFix_CookieDomain(CookieContainer cookieContainer)
        {
            System.Type _ContainerType = typeof(CookieContainer);
            Hashtable table = (Hashtable)_ContainerType.InvokeMember("m_domainTable",
                                       System.Reflection.BindingFlags.NonPublic |
                                       System.Reflection.BindingFlags.GetField |
                                       System.Reflection.BindingFlags.Instance,
                                       null,
                                       cookieContainer,
                                       new object[] { });
            ArrayList keys = new ArrayList(table.Keys);
            foreach (string keyObj in keys)
            {
                string key = (keyObj as string);
                if (key[0] == '.')
                {
                    string newKey = key.Remove(0, 1);
                    table[newKey] = table[keyObj];
                }
            }
        }

    }

    class PreJsonResult
    {
        public int retcode;
        public int servertime;
        public string pcid;
        public string nonce;
        public string pubkey;
        public string rsakv;
        public int exectime;
    }

}
