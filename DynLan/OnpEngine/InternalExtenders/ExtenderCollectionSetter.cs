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
    public static class ExtenderCollectionSetter
    {
        public static readonly String Name = "__COL_SETTER";

        public static readonly Char[] NameChars = Name.ToCharArray();

        ////////////////////////////////////////////////////////////////////////

        public static Object Execute(DynLanContext EvaluateContext, Object obj, IList<Object> Parameters)
        {
            Object Collection = obj;
            Object value = Parameters != null && Parameters.Count > 0 ? Parameters[0] : null;
            Object Key = Parameters != null && Parameters.Count > 1 ? Parameters[1] : null;

            if (Collection == null)
                return null;

            if (Collection is DynLanObject)
            {
                DynLanObject DynLanObj = Collection as DynLanObject;

                String finalKey = (String)(Key.GetType() == typeof(String) ? Key :
                    Convert.ChangeType(Key, typeof(String), System.Globalization.CultureInfo.InvariantCulture));

                Object finValue = value == null ? null : (value.GetType() == typeof(Object) ? value :
                    Convert.ChangeType(value, typeof(Object), System.Globalization.CultureInfo.InvariantCulture));

                DynLanObj[finalKey] = finValue;

                return value;
            }

            if (Collection is IDictionaryWithGetter)
            {
                IDictionaryWithGetter dict = (IDictionaryWithGetter)Collection;

                Type[] arguments = dict.GetType().GetGenericArguments();
                Type keyType = arguments[0];
                Type valueType = arguments[1];

                Object finalKey = Key.GetType() == keyType ? Key :
                    Convert.ChangeType(Key, keyType, System.Globalization.CultureInfo.InvariantCulture);

                Object finValue = value == null ? null : (value.GetType() == valueType ? value :
                    Convert.ChangeType(value, valueType, System.Globalization.CultureInfo.InvariantCulture));

                if (dict.CanSetValueToDictionary(finalKey))
                {
                    lock (dict)
                    {
                        dict.Remove(finalKey);
                        dict.Add(finalKey, finValue);
                    }
                }

                return value;
            }
            
            else if (Collection is IDictionary)
            {
                IDictionary dict = (IDictionary)Collection;

                Type[] arguments = dict.GetType().GetGenericArguments();
                Type keyType = arguments[0];
                Type valueType = arguments[1];

                Object finalKey = Key.GetType() == keyType ? Key :
                    Convert.ChangeType(Key, keyType, System.Globalization.CultureInfo.InvariantCulture);

                Object finValue = value == null ? null : (value.GetType() == valueType ? value :
                    Convert.ChangeType(value, valueType, System.Globalization.CultureInfo.InvariantCulture));

                lock (dict)
                {
                    dict.Remove(finalKey);
                    dict.Add(finalKey, finValue);
                }

                return value;
            }

            else if (Collection is IDictionary<string, object>)
            {
                IDictionary<string, object> dict = (IDictionary<string, object>)Collection;

                lock (dict)
                {
                    string finalKey = UniConvert.ToString(Key);
                    dict.Remove(finalKey);
                    dict.Add(finalKey, value);
                }

                return value;
            }

            if (Collection is IList)
            {
                Int32? index = UniConvert.ToInt32N(Key);
                if (index == null || index < 0)
                    return null;

                IList list = (IList)Collection;
                if (index >= list.Count)
                    return null;

                Type listType = MyTypeHelper.GetListType(list);

                Object finValue = value == null ? null : (value.GetType() == listType ? value :
                    Convert.ChangeType(value, listType, System.Globalization.CultureInfo.InvariantCulture));

                list[index.Value] = finValue;

                return value;
            }

            return null;
        }
    }
}