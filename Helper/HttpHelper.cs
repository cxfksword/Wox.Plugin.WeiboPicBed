using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Web;

namespace Wox.Plugin.WeiboPicBed
{
    public class HttpHelper
    {
        public const string CharsetReg = @"(meta.*?charset=""?(?<Charset>[^\s""'>]+)""?)|(xml.*?encoding=""?(?<Charset>[^\s"">]+)""?)";

        /// <summary>
        /// 使用Http Request获取网页信息
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="postData">Post的信息</param>
        /// <param name="cookies">Cookies</param>
        /// <param name="userAgent">浏览器标识</param>
        /// <param name="referer">来源页</param>
        /// <param name="cookiesDomain">Cookies的Domian参数，配合cookies使用；为空则取url的Host</param>
        /// <param name="encode">编码方式，用于解析html</param>
        /// <returns></returns>
        public static HttpResponse HttpRequest(string url, Dictionary<string, string> postData = null, CookieContainer cookies = null, string userAgent = "", string referer = "", string cookiesDomain = "", Encoding encode = null, string filePath = null, Dictionary<string, string> headers = null)
        {
            HttpResponse httpResponse = new HttpResponse();

            try
            {
                HttpWebResponse httpWebResponse = null;
                if (postData != null || !string.IsNullOrEmpty(filePath))
                    httpWebResponse = CreatePostHttpResponse(url, postData, cookies: cookies, userAgent: userAgent, referer: referer, filePath: filePath, headers:headers);
                else
                    httpWebResponse = CreateGetHttpResponse(url, cookies: cookies, userAgent: userAgent, referer: referer, headers: headers);

                httpResponse.Url = httpWebResponse.ResponseUri.ToString();
                httpResponse.HttpCode = (int)httpWebResponse.StatusCode;
                httpResponse.LastModified = Utils.ConvertDateTimeInt(httpWebResponse.LastModified);

                #region 根据Html头判断
                string Content = null;
                //缓冲区长度
                const int N_CacheLength = 10000;
                //头部预读取缓冲区，字节形式
                var bytes = new List<byte>();
                int count = 0;
                //头部预读取缓冲区，字符串
                String cache = string.Empty;

                //创建流对象并解码
                Stream ResponseStream;
                switch (httpWebResponse.ContentEncoding.ToUpperInvariant())
                {
                    case "GZIP":
                        ResponseStream = new GZipStream(
                            httpWebResponse.GetResponseStream(), CompressionMode.Decompress);
                        break;
                    case "DEFLATE":
                        ResponseStream = new DeflateStream(
                            httpWebResponse.GetResponseStream(), CompressionMode.Decompress);
                        break;
                    default:
                        ResponseStream = httpWebResponse.GetResponseStream();
                        break;
                }

                try
                {

                    if (encode == null)
                    {
                        try
                        {
                            if (httpWebResponse.CharacterSet == "ISO-8859-1" || httpWebResponse.CharacterSet == "zh-cn")
                            {
                                Match match = Regex.Match(cache, CharsetReg, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                                if (match.Success)
                                {
                                    try
                                    {
                                        string charset = match.Groups["Charset"].Value;
                                        encode = Encoding.GetEncoding(charset);
                                    }
                                    catch { }
                                }
                                else
                                    encode = Encoding.GetEncoding("GB2312");
                            }
                            else if (httpWebResponse.CharacterSet != null)
                                encode = Encoding.GetEncoding(httpWebResponse.CharacterSet);
                            else
                                encode = Encoding.GetEncoding("GB2312");
                        }
                        catch
                        {
                            encode = Encoding.GetEncoding("GB2312");
                        }
                    }


                    using (var reader = new System.IO.StreamReader(ResponseStream, encode))
                    {
                        Content = reader.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    httpResponse.Content = ex.ToString();
                    return httpResponse;
                }
                finally
                {
                    httpWebResponse.Close();
                }
                #endregion 根据Html头判断
                //获取返回的Cookies，支持httponly
                if (string.IsNullOrEmpty(cookiesDomain))
                    cookiesDomain = httpWebResponse.ResponseUri.Host;

                cookies = new CookieContainer();
                CookieCollection httpHeaderCookies = SetCookie(httpWebResponse, cookiesDomain);
                cookies.Add(httpHeaderCookies ?? httpWebResponse.Cookies);

                httpResponse.Content = Content;
            }
            catch(Exception e)
            {
                httpResponse.Content = string.Empty;
            }
            return httpResponse;
        }


        /// <summary>
        /// 获取网页的内容
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="postData">Post的信息</param>
        /// <param name="cookies">Cookies</param>
        /// <param name="userAgent">浏览器标识</param>
        /// <param name="referer">来源页</param>
        /// <param name="cookiesDomain">Cookies的Domian参数，配合cookies使用；为空则取url的Host</param>
        /// <param name="encode">编码方式，用于解析html</param>
        /// <returns></returns>
        public static string GetHttpContent(string url, Dictionary<string, string> postData = null, CookieContainer cookies = null, string userAgent = "", string referer = "", string cookiesDomain = "", Encoding encode = null, string filePath = null, Dictionary<string, string> headers = null)
        {
            return HttpHelper.HttpRequest(url, postData, cookies, userAgent, referer, cookiesDomain, encode, filePath, headers).Content;
        }

        /// <summary>
        /// 创建GET方式的HTTP请求 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="timeout"></param>
        /// <param name="userAgent"></param>
        /// <param name="cookies"></param>
        /// <param name="referer"></param>
        /// <returns></returns>
        public static HttpWebResponse CreateGetHttpResponse(string url, int timeout = 60000, string userAgent = "", CookieContainer cookies = null, string referer = "",  Dictionary<string, string> headers = null)
        {
            HttpWebRequest request = null;
            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                //对服务端证书进行有效性校验（非第三方权威机构颁发的证书，如自己生成的，不进行验证，这里返回true）
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                request = WebRequest.Create(url) as HttpWebRequest;
                //request.ProtocolVersion = HttpVersion.Version10;    //http版本，默认是1.1,这里设置为1.0
            }
            else
            {
                request = WebRequest.Create(url) as HttpWebRequest;
            }

            request.Referer = referer;
            request.Method = "GET";

            //设置代理UserAgent和超时
            if (string.IsNullOrEmpty(userAgent))
                userAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/34.0.1847.116 Safari/537.36";

            request.UserAgent = userAgent;
            request.Timeout = timeout;
            request.KeepAlive = true;
            request.AllowAutoRedirect = true;
            request.Accept = "*/*";
            request.Headers.Add("Accept-Charset", "UTF-8,*;q=0.5");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.8");

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (cookies == null)
                cookies = new CookieContainer();
            request.CookieContainer = cookies;

            return request.GetResponse() as HttpWebResponse;
        }

        /// <summary>
        /// 创建POST方式的HTTP请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="timeout"></param>
        /// <param name="userAgent"></param>
        /// <param name="cookies"></param>
        /// <param name="referer"></param>
        /// <returns></returns>
        public static HttpWebResponse CreatePostHttpResponse(string url, Dictionary<string, string> postData, int timeout = 60000, string userAgent = "", CookieContainer cookies = null, string referer = "", string filePath = null, Dictionary<string, string> headers = null)
        {
            HttpWebRequest request = null;

            //如果是发送HTTPS请求  
            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                request = WebRequest.Create(url) as HttpWebRequest;
                //request.ProtocolVersion = HttpVersion.Version10;
            }
            else
            {
                request = WebRequest.Create(url) as HttpWebRequest;
            }
            request.Referer = referer;
            request.Method = "POST";

            //设置代理UserAgent和超时
            if (string.IsNullOrEmpty(userAgent))
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/36.0.1985.125 Safari/537.36";
            else
                request.UserAgent = userAgent;
            request.Timeout = timeout;
            request.KeepAlive = true;
            request.AllowAutoRedirect = true;
            request.Accept = "*/*";
            request.Headers.Add("Accept-Charset", "UTF-8,*;q=0.5");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.8");

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (cookies == null)
                cookies = new CookieContainer();
            request.CookieContainer = cookies;

            

            if (!string.IsNullOrEmpty(filePath))
            {
                if (postData == null || postData.Count <= 0)
                {
                    request.ContentType = "application/octet-stream";
                    var fileData = File.ReadAllBytes(filePath);
                    request.ContentLength = fileData.Length;

                    var requestStream = request.GetRequestStream();
                    requestStream.Write(fileData, 0, fileData.Length);
                    requestStream.Close();
                }
                else
                {
                    var boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");
                    request.ContentType = "multipart/form-data; boundary=" + boundary;

                    var tempBuffer = GetMultipartFormData(postData, filePath, boundary);

                    request.ContentLength = tempBuffer.Length;

                    var requestStream = request.GetRequestStream();
                    requestStream.Write(tempBuffer, 0, tempBuffer.Length);
                    requestStream.Close();
                }
            } else
            {
                request.ContentType = "application/x-www-form-urlencoded";

                //发送POST数据  
                if (postData != null && postData.Count > 0)
                {
                    var querystring = new List<string>();
                    foreach (var param in postData)
                    {
                        querystring.Add(param.Key + "=" + HttpUtility.UrlEncode(param.Value));
                    }

                    byte[] data = Encoding.UTF8.GetBytes(string.Join("&", querystring.ToArray()));
                    request.ContentLength = data.Length;
                    using (Stream stream = request.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }
            }

            //string[] values = request.Headers.GetValues("Content-Type");
            return request.GetResponse() as HttpWebResponse;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, string> postParameters, string filePath, string boundary)
        {
            var encoding = Encoding.UTF8;
            var formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;
            

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                    boundary,
                    param.Key,
                    param.Value);
                formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var contentType = "application/octet-stream";
                    switch(Path.GetExtension(filePath).ToLower())
                    {
                        case ".png":
                            contentType = "image/png";
                            break;
                        case ".jpeg":
                        case ".jpg":
                            contentType = "image/jpeg";
                            break;
                        case ".gif":
                            contentType = "image/gif";
                            break;
                        case ".bmp":
                            contentType = "image/bitmap";
                            break;
                        default:
                            contentType = "application/octet-stream";
                            break;
                    }

                    string filePartHeader = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                       boundary,
                        Path.GetFileNameWithoutExtension(filePath),
                       filePath,
                       contentType);

                    formDataStream.Write(encoding.GetBytes(filePartHeader), 0, encoding.GetByteCount(filePartHeader));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    var buffer = new byte[1024];
                    int bytesRead; // =0
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        formDataStream.Write(buffer, 0, bytesRead);
                    }
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }

        /// <summary>
        /// 验证证书
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="errors"></param>
        /// <returns>是否验证通过</returns>
        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;
            return false;
        }

        /// <summary>
        /// 根据response中头部的set-cookie对request中的cookie进行设置
        /// </summary>
        /// <param name="setCookie">The set cookie.</param>
        /// <param name="defaultDomain">The default domain.</param>
        /// <returns></returns>
        private static CookieCollection SetCookie(HttpWebResponse response, string defaultDomain)
        {
            try
            {
                string[] setCookie = response.Headers.GetValues("Set-Cookie");

                // there is bug in it,the datetime in "set-cookie" will be sepreated in two pieces.
                List<string> a = new List<string>(setCookie);
                for (int i = setCookie.Length - 1; i > 0; i--)
                {
                    if (a[i].Substring(a[i].Length - 3) == "GMT")
                    {
                        a[i - 1] = a[i - 1] + ", " + a[i];
                        a.RemoveAt(i);
                        i--;
                    }
                }
                setCookie = a.ToArray<string>();
                CookieCollection cookies = new CookieCollection();
                foreach (string str in setCookie)
                {
                    NameValueCollection hs = new NameValueCollection();
                    foreach (string i in str.Split(';'))
                    {
                        int index = i.IndexOf("=");
                        if (index > 0)
                            hs.Add(i.Substring(0, index).Trim(), i.Substring(index + 1).Trim());
                        else
                            switch (i)
                            {
                                case "HttpOnly":
                                    hs.Add("HttpOnly", "True");
                                    break;
                                case "Secure":
                                    hs.Add("Secure", "True");
                                    break;
                            }
                    }
                    Cookie ck = new Cookie();
                    foreach (string Key in hs.AllKeys)
                    {
                        switch (Key.ToLower().Trim())
                        {
                            case "path":
                                ck.Path = hs[Key];
                                break;
                            case "expires":
                                ck.Expires = DateTime.Parse(hs[Key]);
                                break;
                            case "domain":
                                ck.Domain = hs[Key];
                                break;
                            case "httpOnly":
                                ck.HttpOnly = true;
                                break;
                            case "secure":
                                ck.Secure = true;
                                break;
                            default:
                                ck.Name = Key;
                                ck.Value = hs[Key];
                                break;
                        }
                    }
                    if (ck.Domain == "") ck.Domain = defaultDomain;
                    //// fix CookieContainer domain bug
                    //// http://stackoverflow.com/questions/1047669/cookiecontainer-bug
                    //if (ck.Domain[0] == '.')
                    //{
                    //    ck.Domain = ck.Domain.Remove(0, 1);
                    //}
                    if (ck.Name != "") cookies.Add(ck);
                }
                return cookies;
            }
            catch(Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// 遍历CookieContainer
        /// </summary>
        /// <param name="cookieContainer"></param>
        /// <returns>List of cookie</returns>
        public static Dictionary<string, string> GetAllCookies(CookieContainer cookieContainer)
        {
            Dictionary<string, string> cookies = new Dictionary<string, string>();

            Hashtable table = (Hashtable)cookieContainer.GetType().InvokeMember("m_domainTable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField |
                System.Reflection.BindingFlags.Instance, null, cookieContainer, new object[] { });

            foreach (string pathList in table.Keys)
            {
                StringBuilder _cookie = new StringBuilder();
                SortedList cookieColList = (SortedList)table[pathList].GetType().InvokeMember("m_list",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField
                    | System.Reflection.BindingFlags.Instance, null, table[pathList], new object[] { });
                foreach (CookieCollection colCookies in cookieColList.Values)
                    foreach (Cookie c in colCookies)
                        _cookie.Append(c.Name + "=" + c.Value + ";");

                cookies.Add(pathList, _cookie.ToString().TrimEnd(';'));
            }
            return cookies;
        }

        /// <summary>
        /// convert cookies string to CookieContainer
        /// </summary>
        /// <param name="cookies"></param>
        /// <returns></returns>
        public static CookieContainer ConvertToCookieContainer(Dictionary<string, string> cookies)
        {
            CookieContainer cookieContainer = new CookieContainer();

            foreach (var cookie in cookies)
            {
                string[] strEachCookParts = cookie.Value.Split(';');
                int intEachCookPartsCount = strEachCookParts.Length;

                foreach (string strCNameAndCValue in strEachCookParts)
                {
                    if (!string.IsNullOrEmpty(strCNameAndCValue))
                    {
                        Cookie cookTemp = new Cookie();
                        int firstEqual = strCNameAndCValue.IndexOf("=");
                        string firstName = strCNameAndCValue.Substring(0, firstEqual);
                        string allValue = strCNameAndCValue.Substring(firstEqual + 1, strCNameAndCValue.Length - (firstEqual + 1));
                        cookTemp.Name = firstName;
                        cookTemp.Value = allValue;
                        cookTemp.Path = "/";
                        cookTemp.Domain = cookie.Key;
                        cookieContainer.Add(cookTemp);
                    }
                }
            }
            return cookieContainer;
        }
    }
}
