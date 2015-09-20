using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.WeiboPicBed
{
    public class LoginResult
    {
        public bool Success { get; set; }

        public string Msg { get; set; }

        public  IDictionary<string, string> Cookies { get; set; }
    }
}
