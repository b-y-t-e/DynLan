using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using DynLan;
using DynLan.Classes;
using DynLan.Helpers;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Internal;

namespace DynLan.OnpEngine.Logic
{
    public static class ObjectValueGetter
    {
        public static Object GetValueFromObject(
            Object Obj,
            String PropertyPath)
        {
            Boolean foundValue = false;
            return GetValueFromObject(Obj, PropertyPath, -1, false, out foundValue);
        }

        public static Object GetValueFromObject(
            Object Obj,
            String PropertyPath,
            Int32 ParametersCount,
            Boolean ExtenderExists,
            out Boolean FoundValue)
        {
            FoundValue = false;

            if (Obj == null)
            {
                FoundValue = true;
                return null;
            }

            if (Obj is EmptyObject)
                throw new NotImplementedException();

            if (Obj is IDictionaryWithGetter)
            {
                IDictionaryWithGetter dict = Obj as IDictionaryWithGetter;
                Boolean contains = dict.Contains(PropertyPath);

                if ((contains || !ExtenderExists) && dict.CanGetValueFromDictionary(PropertyPath))
                {
                    FoundValue = true;
                    return dict.GetValueFromDictionary(PropertyPath);
                }
            }

            else if (Obj is IDictionary)
            {
                IDictionary dict = Obj as IDictionary;
                if (dict.Contains2(PropertyPath))
                {
                    FoundValue = true;
                    return dict.Get2(PropertyPath);
                }
            }

            else if (Obj is IDictionary<string, object>)
            {
                IDictionary<string, object> dict = Obj as IDictionary<string, object>;
                if (dict.Contains2(PropertyPath))
                {
                    FoundValue = true;
                    return dict.Get2(PropertyPath);
                }
            }

            else if (Obj is DynLanObject)
            {
                if (!GlobalSettings.CaseSensitive)
                {
                    PropertyPath = PropertyPath.ToUpperInvariant();
                }
                DynLanObject DynLanObj = Obj as DynLanObject;
                if (DynLanObj.Contains(PropertyPath))
                {
                    FoundValue = true;
                    return DynLanObj[PropertyPath];
                }
            }

            Object val = RefSensitiveHelper.I.GetValueOrMethod(
                Obj,
                PropertyPath,
                ParametersCount,
                out FoundValue);

            return val;
        }

        public static Boolean EvaluateValue(
            String FieldOrMethodName,
            DynContext DynLanContext)
        {
            return EvaluateValueOrMethod(
                null,
                FieldOrMethodName,
                -1,
                DynLanContext);
        }

        public static Boolean EvaluateValueOrMethod(
            Object Obj,
            String FieldOrMethodName,
            Int32 ParametersCount,
            DynContext DynLanContext)
        {
            Boolean seekInObject = (Obj != null && !(Obj is EmptyObject));

            Boolean wasValueFoundFromContext = false;
            Boolean wasValueFoundInObject = false;
            Boolean wasValueFoundInExpressions = false;

            ExpressionValue expressionValue = DynLanContext.GetValue(
                DynLanContext,
                FieldOrMethodName,
                seekInObject,
                !seekInObject,
                !seekInObject);

            if (expressionValue != null)
                wasValueFoundFromContext = true;

            if (seekInObject)
            {
                Boolean foundValue = false;

                Object value = GetValueFromObject(
                    Obj,
                    FieldOrMethodName,
                    ParametersCount,
                    expressionValue != null,
                    out foundValue);

                if (foundValue)
                {
                    wasValueFoundInObject = true;

                    if (value is MethodInfo)
                    {
                        OnpMethodInfo methodInfo = new OnpMethodInfo();
                        methodInfo.Obj = Obj;
                        methodInfo.MethodName = FieldOrMethodName;
                        DynLanContext.CurrentExpressionState.PushValue(methodInfo);
                        return false;
                    }
                    else
                    {
                        DynLanContext.CurrentExpressionState.PushValue(value);
                        return false;
                    }
                }
            }

            Expression expression = DynLanContext.
                CurrentExpressionGroup.
                FindExpression(FieldOrMethodName, DynLanContext);

            if (expression == null)
            {
                Object value = expressionValue == null ?
                    null :
                    expressionValue.Value;

                value = InternalTypeConverter.ToInner(
                    value);

#if !IGNORE_NOT_DECLARED_VARIABLES
                if (!wasValueFoundFromContext && !wasValueFoundInObject)
                {
                    if (seekInObject)
                        throw new DynLanVariableNotFoundException("Variable " + FieldOrMethodName + " not found in object " + Obj + "!") { Variable = FieldOrMethodName };

                    throw new DynLanVariableNotFoundException("Variable " + FieldOrMethodName + " not found!") { Variable = FieldOrMethodName };
                }
#endif

                DynLanContext.CurrentExpressionState.PushValue(value);
                return false;
            }
            else
            {
                wasValueFoundInExpressions = true;

                ExpressionState newExpressionState = new ExpressionState();
                newExpressionState.Expression = expression;

                DynLanContext.CurrentExpressionContext.Stack.Add(newExpressionState);
                return false;
            }
        }

    }


#if !PCL && !NET20
    [Serializable]
#endif
    public class DynLanVariableNotFoundException : Exception
    {
        public String Variable { get; set; }
        public DynLanVariableNotFoundException() { }
        public DynLanVariableNotFoundException(string message) : base(message) { }
        public DynLanVariableNotFoundException(string message, Exception inner) : base(message, inner) { }

#if !PCL && !NET20
        protected DynLanVariableNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
#endif
    }

    public interface IDictionaryWithGetter : IDictionary
    {
        bool CanGetValueFromDictionary(Object Key);

        bool CanSetValueToDictionary(Object Key);

        object GetValueFromDictionary(Object Key);
    }

    public static class DiEx
    {
        public static bool Contains2(this IDictionary dictionary, String key)
        {
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                    return true;
            return false;
        }
        public static Object Get2(this IDictionary dictionary, String key)
        {
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                    return dictionary[k];
            return null;
        }
        public static bool TryGetValue2(this IDictionary dictionary, String key, out Object val)
        {
            val = null;
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                {
                    val = dictionary[k];
                    return true;
                }
            return false;
        }
        public static void Set2(this IDictionary dictionary, String key, Object val)
        {
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                {
                    dictionary.Remove(k);
                    dictionary[key] = val;
                    break;
                }
            dictionary[key] = val;
        }
        public static bool Contains2(this IDictionary<String, Object> dictionary, String key)
        {
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                    return true;
            return false;
        }
        public static Object Get2(this IDictionary<String, Object> dictionary, String key)
        {
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                    return dictionary[k];
            return null;
        }
        public static bool TryGetValue2(this IDictionary<String, Object> dictionary, String key, out Object val)
        {
            val = null;
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                {
                    val = dictionary[k];
                    return true;
                }
            return false;
        }
        public static void Set2(this IDictionary<String, Object> dictionary, String key, Object val)
        {
            key = key.ToUpperInvariant();
            foreach (var k in dictionary.Keys)
                if (UniConvert.ToString(k).ToUpperInvariant() == key)
                {
                    dictionary.Remove(k);
                    dictionary[key] = val;
                    break;
                }
            dictionary[key] = val;
        }
    }
}