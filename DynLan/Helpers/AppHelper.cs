using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DynLan.Helpers
{
    public static class AppHelper
    {
        public static String GetPath(String FilePath = null)
        {
            var uri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            var path = Path.GetDirectoryName(uri.LocalPath);

            if (String.IsNullOrEmpty(FilePath))
                return path;

            return Path.Combine(
                path, FilePath);
        }
    }
}