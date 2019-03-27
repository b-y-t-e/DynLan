#define NOT_CLASSIC_REFLECTION

using System;
using System.Collections;
using System.Reflection;
//using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;


//

namespace DynLan.Helpers
{
    public static class MyAssemblyHelper
    {
        private static Object lck = new object();

        private static Dictionary<String, Dictionary<String, Type>> cache;

        //////////////////////////////////////////////////////////////////////////////////

        public static Object CreateType(String Name)
        {
            var type = FindType(Name);

            if (type != null)
                return Activator.CreateInstance(type);
            return null;
        }

        public static Type FindType(String Name)
        {
            Init();

            if (cache.ContainsKey(Name))
            {
                var dict = cache[Name];

                if (dict.ContainsKey(Name))
                {
                    return dict[Name];
                }
                else
                {
#if !NET20
                    return dict.First().Value;
#else
                    return Linq2.FirstOrDefault( dict).Value;
#endif
                }
            }

            return null;
        }

        //////////////////////////////////////////////////////////////////////////////////

        private static void Init()
        {
            if (cache == null)
            {
                lock (lck)
                {
                    if (cache == null)
                    {
                        cache = new Dictionary<String, Dictionary<String, Type>>();
#if !PCL
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                if (!((type.IsPublic || MyTypeHelper.IsStatic(type)) && !type.IsInterface))
                                    continue;

                                var typeName = GetShortName(type.FullName);
                                if (typeName == null)
                                    continue;

                                if (!cache.ContainsKey(typeName))
                                    cache[typeName] = new Dictionary<String, Type>();

                                cache[typeName][type.Name] = type;
                            }
                        }
#endif
                    }
                }
            }

        }

        private static string GetShortName(String FullName)
        {
            var i1 = FullName.IndexOf(" ");
            var i2 = FullName.IndexOf(",");

            var s = "";
            if ((i1 > 0 && i2 < 0) || (i1 > 0 && i2 > 0 && i1 < i2))
            {
                s = FullName.Substring(0, i1);
            }
            else if ((i1 < 0 && i2 > 0) || (i1 > 0 && i2 > 0 && i1 > i2))
            {
                s = FullName.Substring(0, i2);
            }
            else if (i1 < 0 && i2 < 0)
            {
                s = FullName;
            }

            if (s != "")
            {
                var i3 = s.LastIndexOf(".");
                if (i3 >= 0)
                    return s.Substring(i3 + 1);
                return s;
            }
            return null;
        }
    }
}
