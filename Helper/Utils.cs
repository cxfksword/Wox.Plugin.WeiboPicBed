using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Wox.Plugin.WeiboPicBed
{
    public class Utils
    {

        /// <summary>
		/// change Unix timestamp to csharp DateTime
        /// </summary>
		/// <param name="d">double timestamp</param>
        /// <returns>DateTime</returns>
        public static DateTime ConvertIntDateTime(double d)
        {
            DateTime time = DateTime.MinValue;
            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            time = startTime.AddMilliseconds(d);
            return time;
        }

        /// <summary>
		/// change csharp DateTime to Unix timestamp
        /// </summary>
		/// <param name="time">DateTime</param>
        /// <returns>13位时间戳</returns>
        public static long ConvertDateTimeInt(DateTime time)
        {
            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1, 0, 0, 0, 0));
            long t = (time.Ticks - startTime.Ticks) / 10000;  //除10000调整为13位
            return t;
        }

        public static  string Encrypt(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var encodeData = ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encodeData);
            }
            else
            {
                return "";
            }
        }

        public static string Decrypt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var decodedData = ProtectedData.Unprotect(Convert.FromBase64String(text), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decodedData);
        }

    }
}
