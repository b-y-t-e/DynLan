using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using DynLan;
using DynLan.Classes;
using DynLan.Helpers;
using DynLan.OnpEngine.Logic;

namespace DynLan.OnpEngine.InternalExtenders
{
    public static class ExtenderCollectionGetter
    {
        public static readonly String Name = "__COL_GETTER";

        public static readonly Char[] NameChars = Name.ToCharArray();

        ////////////////////////////////////////////////////////////////////////

        public static Object Execute(DynLanContext EvaluateContext, Object obj, IList<Object> Parameters)
        {
            Object Collection = obj;
            Object Key = Parameters == null ? null : Parameters.FirstOrDefault();

            if (Collection == null)
                return null;

            if (Collection is String)
            {
                Int32? index = UniConvert.ToInt32N(Key);
                if (index == null || index < 0)
                    return null;

                String str = (String)Collection;
                if (index >= str.Length)
                    return null;

                return str[index.Value];
            }

            if (Collection is DynLanObject)
            {
                DynLanObject DynLanObj = Collection as DynLanObject;

                if (DynLanObj.TotalCount == 0)
                    return null;

                /*IDictionary<String, Object> dict = ((DynLanObject)Collection).Values;
                if (dict.Count == 0)
                    return null;*/

                String finalKey = ((String)(Key.GetType() == typeof(String) ? Key :
                    Convert.ChangeType(Key, typeof(String), System.Globalization.CultureInfo.InvariantCulture)));

                return DynLanObj[finalKey];
            }

            if (Collection is IDictionaryWithGetter)
            {
                IDictionaryWithGetter dict = Collection as IDictionaryWithGetter;
                if (dict.CanGetValueFromDictionary(Key))
                    return dict.GetValueFromDictionary(Key);
            }
            else if (Collection is IDictionary)
            {
                IDictionary dict = (IDictionary)Collection;
                if (dict.Count == 0)
                    return null;

                Type[] arguments = dict.GetType().GetGenericArguments();
                Type keyType = arguments[0];

                Object finalKey = Key.GetType() == keyType ? Key :
                    Convert.ChangeType(Key, keyType, System.Globalization.CultureInfo.InvariantCulture);

                return dict[finalKey];
            }

            if (Collection is IList)
            {
                Int32? index = UniConvert.ToInt32N(Key);
                if (index == null || index < 0)
                    return null;

                IList list = (IList)Collection;
                if (index >= list.Count)
                    return null;

                return list[index.Value];
            }

            if (Collection is IEnumerable)
            {
                Int32? index = UniConvert.ToInt32N(Key);
                if (index == null || index < 0)
                    return null;

                Int32 i = -1;
                foreach (Object item in ((IEnumerable)Collection))
                {
                    i++;
                    if (i == index.Value)
                    {
                        return item;
                    }
                }
            }

            return null;
        }
    }
}