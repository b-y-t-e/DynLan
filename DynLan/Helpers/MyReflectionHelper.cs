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
    public static class MyReflectionHelper
    {
        private static object _lck = new object();

        //private static Dictionary<Type, Dictionary<String, PropertyInfo>> _propertyUppercaseCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        private static Dictionary<Type, Dictionary<String, PropertyInfo>> _propertyCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        private static Dictionary<Type, Dictionary<String, Dictionary<Int32, MethodInfo>>> _methodsCache = new Dictionary<Type, Dictionary<string, Dictionary<Int32, MethodInfo>>>();

        //private static Dictionary<Type, Dictionary<String, Dictionary<Int32, MethodInfo>>> _methodsUppercaseCache = new Dictionary<Type, Dictionary<string, Dictionary<Int32, MethodInfo>>>();

        ////////////////////////////////////////

        public static Object GetValue(
#if !NET20
            this 
#endif
             Object Obj, String PropertyName)
        {
            if (Obj != null)
            {
                Type type = Obj.GetType();
                CacheProperties(type);

                // szukanie property
                PropertyInfo propertyInfo = null;

                if (_propertyCache[type].ContainsKey(PropertyName))
                {
                    propertyInfo = _propertyCache[type][PropertyName];
                }
                /*else if (_propertyUppercaseCache[type].ContainsKey(PropertyName.ToUpper()))
                {
                    propertyInfo = _propertyUppercaseCache[type][PropertyName.ToUpper()];
                }*/

                if (propertyInfo != null)
                {
                    return propertyInfo.GetValue(Obj, null);
                }
            }
            return null;
        }

        public static void BindToEvent(Object Object, String EventName, Action<object, object> Handler)
        {
            EventInfo eventInfo = Object.GetType().GetEvent(EventName);
            Delegate convertedHandler = ConvertDelegate(Handler, eventInfo.EventHandlerType);
            eventInfo.AddEventHandler(Object, convertedHandler);
        }

        public static Delegate ConvertDelegate(Delegate originalDelegate, Type targetDelegateType)
        {
            return Delegate.CreateDelegate(
                targetDelegateType,
                originalDelegate.Target,
                originalDelegate.Method);
        }

        public static Object GetValueByPath(
#if !NET20
            this 
#endif
             Object Obj, String PropertyPath)
        {
            Object currentObj = Obj;
            if (currentObj == null)
                return null;

            if (String.IsNullOrEmpty(PropertyPath))
            {
                return currentObj;
            }

            String[] paths = PropertyPath.Split('.');
            for (var i = 0; i < paths.Length; i++)
            {
                Boolean isLast = i == (paths.Length - 1);
                String propertyName = paths[i];

                if (isLast)
                {
                    return GetValue(currentObj, propertyName);
                }
                else
                {
                    currentObj = GetValue(currentObj, propertyName);
                }
            }

            return null;
        }

        public static void SetValueByPath(
#if !NET20
            this 
#endif
             Object Obj, String PropertyPath, Object Value)
        {
            Object currentObj = Obj;
            if (currentObj == null)
                return;

            if (String.IsNullOrEmpty(PropertyPath))
            {
                return;
            }

            String[] paths = PropertyPath.Split('.');
            for (var i = 0; i < paths.Length; i++)
            {
                Boolean isLast = i == (paths.Length - 1);
                String propertyName = paths[i];

                if (isLast)
                {
                    SetValue(currentObj, propertyName, Value);
                }
                else
                {
                    currentObj = GetValue(currentObj, propertyName);
                }
            }
        }

        public static void SetValue(
#if !NET20
            this 
#endif
             Object Obj, String PropertyName, Object Value)
        {
            if (Obj != null)
            {
                Object value = Value;
                Type type = Obj.GetType();
                CacheProperties(type);

                // szukanie property
                PropertyInfo propertyInfo = null;
                if (_propertyCache[type].ContainsKey(PropertyName))
                {
                    propertyInfo = _propertyCache[type][PropertyName];
                }
                /*else if (_propertyUppercaseCache[type].ContainsKey(PropertyName.ToUpper()))
                {
                    propertyInfo = _propertyUppercaseCache[type][PropertyName.ToUpper()];
                }*/

                if (propertyInfo != null)
                {
                    if (Value != null && propertyInfo.PropertyType != Value.GetType())
                    {
                        value = MyTypeHelper.ConvertTo(value, propertyInfo.PropertyType);
                    }
                    propertyInfo.SetValue(Obj, value, null);
                }
            }
        }

        ////////////////////////////////////////

        public static DynamicCallResult CallMethod(
#if !NET20
            this 
#endif
             Object Obj, String MethodName, IList<Object> Parameters)
        {
            MethodInfo methodInfo = MyReflectionHelper.GetMethod(
                Obj,
                MethodName,
                Parameters == null ? 0 : Parameters.Count);

            if (methodInfo != null)
            {
                IList<ParameterInfo> parametersInfo = methodInfo.GetParameters();
                List<Object> finalParameters = new List<Object>();

                if (parametersInfo.Count == 1 &&
                    parametersInfo[0].ParameterType == typeof(DynLanMethodParameters))
                {
                    foreach (Object parameter in Parameters)
                        finalParameters.Add(parameter);

                    finalParameters = new List<object>()
                    {
                        new DynLanMethodParameters()
                        {
                            Parameters = finalParameters.ToArray()
                        }
                    };
                }
                else
                {
                    Int32 index = -1;
                    foreach (ParameterInfo parameterInfo in parametersInfo)
                    {
                        index++;

                        Object val = MyTypeHelper.ConvertTo(
                            Parameters[index],
                            parameterInfo.ParameterType);

                        finalParameters.Add(val);
                    }
                }

                return new DynamicCallResult()
                {
                    Value = Obj is Type ?
                        methodInfo.Invoke(null, finalParameters.ToArray()) :
                        methodInfo.Invoke(Obj, finalParameters.ToArray())
                };
            }
            return null;
        }

        public static DynamicCallResult CallMethod(Object Obj, MethodInfo methodInfo, IList<Object> Parameters)
        {
            if (methodInfo != null)
            {
                IList<ParameterInfo> parametersInfo = methodInfo.GetParameters();

                List<Object> finalParameters = new List<Object>();
                Int32 index = -1;

                if (parametersInfo.Count == 1 &&
                    parametersInfo[0].ParameterType == typeof(DynLanMethodParameters))
                {
                    foreach (Object parameter in Parameters)
                        finalParameters.Add(parameter);

                    finalParameters = new List<object>()
                    {
                        new DynLanMethodParameters()
                        {
                            Parameters = finalParameters.ToArray()
                        }
                    };
                }
                else
                {
                    foreach (ParameterInfo parameterInfo in parametersInfo)
                    {
                        index++;

                        Object val = MyTypeHelper.ConvertTo(
                            Parameters[index],
                            parameterInfo.ParameterType);

                        finalParameters.Add(val);
                    }
                }

                return new DynamicCallResult()
                {
                    Value = methodInfo.Invoke(Obj, finalParameters.ToArray())
                };
            }
            return null;
        }

        public static MethodInfo GetMethod(
#if !NET20
            this 
#endif
             Object Item, String MethodName, Int32 ParameterCount = -1)
        {
            if (Item != null)
            {
                return GetMethod(
                    Item is Type ? (Type)Item : (Type)Item.GetType(),
                    MethodName,
                    ParameterCount);
            }
            return null;
        }

        public static MethodInfo GetMethod(
#if !NET20
            this 
#endif
             Type Type, String MethodName, Int32 ParameterCount = -1)
        {
            if (ParameterCount < 0)
                ParameterCount = -1;

            if (Type == null)
                return null;

            lock (_methodsCache)
                if (!_methodsCache.ContainsKey(Type))
                // lock (_methodsUppercaseCache)
                //   if (!_methodsUppercaseCache.ContainsKey(Type))
                {
                    _methodsCache[Type] = new Dictionary<string, Dictionary<int, MethodInfo>>();
                    //_methodsUppercaseCache[Type] = new Dictionary<string, Dictionary<int, MethodInfo>>();

                    IList<MethodInfo> methods = Type.
                        GetMethods();

                    Int32 index = -1;
#if !NET20
            foreach (MethodInfo method in methods.OrderByDescending(m => m.GetParameters().Length > 0 ? m.GetParameters()[0].ParameterType == typeof(string) ? 10 : 5 : 0).OrderBy(m => m.Name).OrderBy(m => m.GetParameters().Length))
                    
#else
                    foreach (MethodInfo method in Linq.OrderBy(Linq.OrderBy(Linq.OrderByDescending(methods, m => m.GetParameters().Length > 0 ? m.GetParameters()[0].ParameterType == typeof(string) ? 10 : 5 : 0), m => m.Name), m => m.GetParameters().Length))

#endif
                    {
                        index++;

                        if (!_methodsCache[Type].ContainsKey(method.Name))
                            _methodsCache[Type][method.Name] = new Dictionary<int, MethodInfo>();

                        //if (!_methodsUppercaseCache[Type].ContainsKey(method.Name.ToUpper()))
                        //    _methodsUppercaseCache[Type][method.Name.ToUpper()] = new Dictionary<int, MethodInfo>();

                        if (index == 0)
                        {
                            if (!_methodsCache[Type][method.Name].ContainsKey(-1))
                            {
                                _methodsCache[Type][method.Name][-1] = method;
                                //_methodsUppercaseCache[Type][method.Name.ToUpper()][-1] = method;
                            }
                        }

                        Int32 parametersCount = method.GetParameters().Length;
                        if (!_methodsCache[Type][method.Name].ContainsKey(parametersCount))
                        {
                            _methodsCache[Type][method.Name][parametersCount] = method;
                            //_methodsUppercaseCache[Type][method.Name.ToUpper()][parametersCount] = method;
                        }
                    }
                }

            Dictionary<string, Dictionary<int, MethodInfo>> dict = null;
            Dictionary<int, MethodInfo> innerDict = null;
            MethodInfo result = null;

            ////////////////////////////////////////////

            _methodsCache.TryGetValue(Type, out dict);

            if (dict != null)
                dict.TryGetValue(MethodName, out innerDict);

            if (innerDict != null)
            {
                if (ParameterCount == -2 && innerDict.Values.Count > 0)

#if !NET20
            return innerDict.Values.FirstOrDefault();
#else
                    return Linq.FirstOrDefault(innerDict.Values);
#endif
                innerDict.TryGetValue(ParameterCount, out result);
            }

            if (result != null)
                return result;

            ////////////////////////////////////////////

            /*_methodsUppercaseCache.TryGetValue(Type, out dict);

            if (dict != null)
                dict.TryGetValue(MethodName.ToUpper(), out innerDict);

            if (innerDict != null)
            {
                if (ParameterCount == -2 && innerDict.Values.Count > 0)
                    return innerDict.Values.FirstOrDefault();

                innerDict.TryGetValue(ParameterCount, out result);
            }

            if (result != null)
                return result;*/

            return null;
        }

        ////////////////////////////////////////

        /*public static Object Invoke(this Object Item, String MethodName, params Object[] Params)
        {
            if (Item != null)
            {
                var lMethod = Item.GetType().GetMethod(MethodName);
                if (lMethod != null)
                {
                    return lMethod.Invoke(Item, Params);
                }
            }
            return null;
        }*/

        public static Boolean ContainsProperty(Object Item, String PropertyName)
        {
            if (RefSensitiveHelper.I.GetGetter(Item, PropertyName) != null)
                return true;
            return false; // RefUnsensitiveHelper.I.GetGetter(Item, PropertyName) != null;
        }

        public static void CopyTo(Object Source, Object Dest)
        {
            if (Source == null || Dest == null)
                return;

            foreach (String property in RefSensitiveHelper.I.GetProperties(Source))
                RefSensitiveHelper.I.SetValue(
                    Dest,
                    property,
                    RefSensitiveHelper.I.GetValue(Source, property));
        }

        ////////////////////////////////////////

        private static void CacheProperties(Type Type)
        {
            if (!_propertyCache.ContainsKey(Type))
            {
                lock (_lck)
                {
                    if (!_propertyCache.ContainsKey(Type))
                    {
                        var dict1 = new Dictionary<string, PropertyInfo>();
                        foreach (var prop in Type.GetProperties())
                            dict1[prop.Name] = prop;

                        _propertyCache[Type] = dict1; //  Type.GetProperties().ToDictionary(i => i.Name, i => i);

                        var dict2 = new Dictionary<string, PropertyInfo>();
                        foreach (var prop in Type.GetProperties())
                            dict2[prop.Name.ToUpper()] = prop;

                        //_propertyUppercaseCache[Type] = dict2; //  Type.GetProperties().ToDictionary(i => i.Name.ToUpper(), i => i);
                    }
                }
            }
        }
    }

    public class DynamicCallResult
    {
        public Object Value;
    }

    public class DynLanMethodParameters
    {
        public Object[] Parameters;

        public DynLanMethodParameters()
        {
            Parameters = new Object[0];
        }
    }
}
