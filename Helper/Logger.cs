using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wox.Plugin.WeiboPicBed
{
    class Logger
    {
        public static void Error(string message)
        {
            if (string.IsNullOrEmpty(Main.PluginDirectory))
            {
                return;
            }
            var path = Path.Combine(Main.PluginDirectory, "error.log");
            File.AppendAllText(path, message + "\n");
        }

        public static void Error(Exception ex)
        {
            if (string.IsNullOrEmpty(Main.PluginDirectory))
            {
                return;
            }
            var path = Path.Combine(Main.PluginDirectory, "error.log");
            File.AppendAllText(path, ex.Message + "\n" + ex.StackTrace + "\n");
        }
    }
}
