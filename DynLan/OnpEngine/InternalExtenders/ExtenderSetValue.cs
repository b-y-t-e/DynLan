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
    public static class ExtenderSetValue
    {
        public static readonly String Name = "__EXT_SET";

        public static readonly Char[] NameChars = Name.ToCharArray();

        ////////////////////////////////////////////////////////////////////////

        public static Object Execute(DynContext EvaluateContext, Object obj, IList<Object> Parameters)
        {
            Boolean isValueSet = false;

            String propertyPath = UniConvert.ToUniString(Parameters != null && Parameters.Count > 0 ? Parameters[0] : null);
            Object value = (Parameters != null && Parameters.Count > 1 ? Parameters[1] : null);

            if (obj is IDictionaryWithGetter)
            {
                IDictionaryWithGetter dict = obj as IDictionaryWithGetter;
                if (dict.CanSetValueToDictionary(propertyPath))
                {
                    dict[propertyPath] = value;
                    isValueSet = true;
                }
            }

            else if (obj is IDictionary)
            {
                IDictionary dict = obj as IDictionary;
                dict[propertyPath] = value;
                isValueSet = true;
            }

            else if (obj is IDictionary<string, object>)
            {
                IDictionary<string, object> dict = obj as IDictionary<string, object>;
                dict[propertyPath] = value;
                isValueSet = true;
            }

            if (obj is DynLanObject)
            {
                if (!GlobalSettings.CaseSensitive)
                {
                    propertyPath = propertyPath.ToUpperInvariant();
                }
                DynLanObject DynLanObj = obj as DynLanObject;
                DynLanObj[propertyPath] = value;
                return null;
            }

            if (!isValueSet)
                isValueSet = RefSensitiveHelper.I.SetValue(obj, propertyPath, value);

            //if (!isValueSet)
            //    isValueSet = RefUnsensitiveHelper.I.SetValue(obj, propertyPath, value);

            if (isValueSet)
                return value;

            return null;
        }
    }
}