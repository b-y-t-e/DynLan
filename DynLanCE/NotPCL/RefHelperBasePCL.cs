using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DynLan.Helpers;

namespace DynLan.Helpers
{
    public class RefHelperBase
    {
        private Dictionary<Type, Dictionary<String, MemberSetter>> _cacheSetter =
            new Dictionary<Type, Dictionary<String, MemberSetter>>();

        private Dictionary<Type, Dictionary<String, MemberGetter>> _cacheGetter =
            new Dictionary<Type, Dictionary<String, MemberGetter>>();

        private Dictionary<Type, String[]> _cacheProperties =
            new Dictionary<Type, String[]>();

        private Dictionary<Type, PropertyInfo[]> _cacheProperties2 =
            new Dictionary<Type, PropertyInfo[]>();

        private Dictionary<Type, Dictionary<String, PropertyInfo>> _cachePropertyInfos =
            new Dictionary<Type, Dictionary<String, PropertyInfo>>();

        private Dictionary<Type, Dictionary<String, Type>> _cachePropertiesTypes =
            new Dictionary<Type, Dictionary<String, Type>>();

        private object lck = new object();

        private bool unsensitive;

        //////////////////////////////////////////////////

        public RefHelperBase(bool Unsensitive)
        {
            this.unsensitive = Unsensitive;
        }

        //////////////////////////////////////////////////

        public void CopyTo(Object Source, Object Dest, String[] PropertiesToOmit)
        {
            if (Source == null || Dest == null)
                return;

            if (unsensitive)
            {
#if !NET20                
                PropertiesToOmit = PropertiesToOmit.Select(i => i.ToUpper()).ToArray();
#else
                PropertiesToOmit = Linq2.From(PropertiesToOmit).Select(i => i.ToUpper()).ToArray();
#endif
            }

            foreach (var property in GetPropertyinfos(Source))
            {
                if (PropertiesToOmit != null && PropertiesToOmit.Length > 0)
                    if (Linq2.Contains(PropertiesToOmit, unsensitive ? property.Name.ToUpper() : property.Name))
                        continue;

                if (Attribute.IsDefined(property, typeof(NoCloneAttribute)))
                    continue;

                try
                {
                    SetValue(
                        Dest,
                        property.Name,
                        GetValue(Source, property.Name));
                }
                catch
                {
                    throw;
                }
            }
        }
        
        public void CopyTo(Object Source, Object Dest)
        {
            CopyTo(Source, Dest, true);
        }

        public void CopyTo(Object Source, Object Dest, Boolean CopyCollections)
        {
            if (Source == null || Dest == null)
                return;

            foreach (var property in GetPropertyinfos(Source))
            {
                if (!CopyCollections)
                {
                    Type propertyType = property.PropertyType;
                    if (propertyType != null && propertyType != typeof(String) && MyTypeHelper.IsEnumerable(propertyType))
                        continue;
                }

                if (Attribute.IsDefined(property, typeof(NoCloneAttribute)))
                    continue;

                try
                {
                    SetValue(
                        Dest,
                        property.Name,
                        GetValue(Source, property.Name));
                }
                catch
                {
                    throw;
                }
            }
        }

        //////////////////////////////////////////////////

        public Object GetValueOrMethod(Object Item, String PropertyName, Int32 ParametersCount, out Boolean FoundValue)
        {
            FoundValue = false;
            var getter = GetGetter(Item, PropertyName);
            if (getter != null)
            {
                FoundValue = true;
                try
                {
                    return getter.Get(Item);
                }
                catch
                {
                    return MyReflectionHelper.GetValue(
                        Item,
                        PropertyName);
                }
            }
            else
            {
                var method = MyReflectionHelper.GetMethod(Item, PropertyName, ParametersCount);
                if (method != null)
                    FoundValue = true;
                return method;
            }
            return null;
        }

        public Object GetValue(Object Item, String PropertyName)
        {
            var getter = GetGetter(Item, PropertyName);
            if (getter != null)
            {
                try
                {
                    return getter.Get(Item);
                }
                catch
                {
                    return MyReflectionHelper.GetValue(
                        Item,
                        PropertyName);
                }
            }
            return null;
        }

        public bool SetValue(Object Item, String PropertyName, Object Value)
        {
            return SetValue<Object>(Item, PropertyName, Value);
        }

        public DataType GetValue<DataType>(Object Item, String PropertyName)
        {
            var getter = GetGetter(Item, PropertyName);
            if (getter != null)
            {
                try
                {
                    return (DataType)getter.Get(Item);
                }
                catch
                {
                    return (DataType)MyReflectionHelper.GetValue(
                        Item,
                        PropertyName);
                }
            }
            return default(DataType);
        }

        public bool SetValue<DataType>(Object Item, String PropertyName, DataType Value)
        {
            MemberSetter setter = GetSetter(Item, PropertyName);
            if (setter != null)
            {
                try
                {
                    setter.Set(Item, Value);
                }
                catch
                {
                    Type t1 = GetPropertyType(Item, PropertyName); // Item.GetType().GetProperty(PropertyName);
                    Type t2 = Value == null ? null : Value.GetType(); // typeof(DataType);

                    if (t2 == null || t1.Equals(t2))
                    {
                        MyReflectionHelper.SetValue(Item, PropertyName, Value);
                    }
                    else
                    {
                        Object newValue = MyTypeHelper.ConvertTo(Value, t1);
                        try
                        {
                            setter.Set(Item, newValue);
                        }
                        catch
                        {
                            MyReflectionHelper.SetValue(Item, PropertyName, newValue);
                        }
                    }

                    throw;
                }
                return true;
            }
            else
            {

            }
            return false;

        }

        ////////////////////////////////////////////

        public PropertyInfo GetProperty(Object Object, String Name)
        {
            return Object != null ?
                GetProperty(Object.GetType(), Name) :
                null;
        }

        public PropertyInfo GetPropertyByPath(Object Obj, String PropertyPath)
        {
            Object currentObj = Obj;
            if (currentObj == null)
                return null;

            if (String.IsNullOrEmpty(PropertyPath))
                return null;

            String[] paths = PropertyPath.Split('.');
            for (var i = 0; i < paths.Length; i++)
            {
                Boolean isLast = i == (paths.Length - 1);
                String propertyName = paths[i];

                if (isLast)
                {
                    return GetProperty(currentObj, propertyName);
                }
                else
                {
                    currentObj = GetValue(currentObj, propertyName);
                }
            }

            return null;
        }

        public PropertyInfo GetProperty(Type Type, String Name)
        {
            if (this.unsensitive) Name = Name.ToUpper();

            if (!_cachePropertyInfos.ContainsKey(Type))
            {
                lock (lck)
                {
                    if (!_cachePropertyInfos.ContainsKey(Type))
                    {
#if !NET20
                        _cachePropertyInfos[Type] = Type.GetProperties().ToDictionary(
                            p => this.unsensitive ? p.Name.ToUpper() : p.Name,
                            p => p);
#else
                        _cachePropertyInfos[Type] = Linq2.ToDictionary(Type.GetProperties(),
                            p => this.unsensitive ? p.Name.ToUpper() : p.Name,
                            p => p);
#endif
                    }
                }
            }
            return _cachePropertyInfos.ContainsKey(Type) && _cachePropertyInfos[Type].ContainsKey(Name) ?
                _cachePropertyInfos[Type][Name] :
                null;
        }

        public Type GetPropertyType(Object Object, String Name)
        {
            return Object != null ?
                GetPropertyType(Object.GetType(), Name) :
                null;
        }

        ////////////////////////////////////////////

        public Type GetPropertyType(Type Type, String Name)
        {
            if (this.unsensitive) Name = Name.ToUpper();

            if (!_cachePropertiesTypes.ContainsKey(Type))
            {
                lock (lck)
                {
                    if (!_cachePropertiesTypes.ContainsKey(Type))
                    {
#if !NET20
                        _cachePropertiesTypes[Type] = Type.GetProperties().ToDictionary(
                            p => this.unsensitive ? p.Name.ToUpper() : p.Name,
                            p => p.PropertyType);
#else
                        _cachePropertiesTypes[Type] = Linq2.ToDictionary(Type.GetProperties(),
                            p => this.unsensitive ? p.Name.ToUpper() : p.Name,
                            p => p.PropertyType);
#endif
                    }
                }
            }
            return _cachePropertiesTypes.ContainsKey(Type) && _cachePropertiesTypes[Type].ContainsKey(Name) ?
                _cachePropertiesTypes[Type][Name] :
                null;
        }

        ////////////////////////////////////////////

        public String[] GetProperties(Object Object)
        {
            return Object != null ?
                GetProperties(Object.GetType()) :
                new String[0];
        }

        public String[] GetProperties(Type Type)
        {
            if (!_cacheProperties.ContainsKey(Type))
            {
                lock (lck)
                {
                    if (!_cacheProperties.ContainsKey(Type))
                    {
#if !NET20
                        _cacheProperties[Type] = Type.GetProperties().Select(p => this.unsensitive ? p.Name.ToUpper() : p.Name).ToArray();
#else
                        _cacheProperties[Type] = Linq2.From(Type.GetProperties()).Select(p => this.unsensitive ? p.Name.ToUpper() : p.Name).ToArray();
#endif

                    }
                }
            }
            return _cacheProperties.ContainsKey(Type) ?
                _cacheProperties[Type] :
                new String[0];
        }

        ////////////////////////////////////////////

        public PropertyInfo[] GetPropertyinfos(Object Object)
        {
            return Object != null ?
                GetPropertyinfos(Object.GetType()) :
                new PropertyInfo[0];
        }

        public PropertyInfo[] GetPropertyinfos(Type Type)
        {
            if (!_cacheProperties2.ContainsKey(Type))
            {
                lock (lck)
                {
                    if (!_cacheProperties2.ContainsKey(Type))
                    {

                        _cacheProperties2[Type] = Type.GetProperties();
                    }
                }
            }
            return _cacheProperties2.ContainsKey(Type) ?
                _cacheProperties2[Type] :
                new PropertyInfo[0];
        }

        ////////////////////////////////////////////

        public MemberSetter GetSetter(Object Object, String Name)
        {
            if (Object != null)
                return GetSetter(Object.GetType(), Name);
            return null;
        }

        public MemberSetter GetSetter(Type type, String Name)
        {
            if (type != null && !String.IsNullOrEmpty(Name))
            {
                if (this.unsensitive) Name = Name.ToUpper();

                Dictionary<String, MemberSetter> innerDict = null;

                if (!_cacheSetter.ContainsKey(type))
                {
                    lock (lck)
                    {
                        if (!_cacheSetter.ContainsKey(type))
                        {
                            _cacheSetter[type] = innerDict = new Dictionary<String, MemberSetter>();
                        }
                    }
                }
                innerDict = _cacheSetter[type];

                if (!innerDict.ContainsKey(Name))
                {
                    lock (lck)
                    {
                        if (!innerDict.ContainsKey(Name))
                        {
                            MemberSetter setter = null;
#if !NET20
                            PropertyInfo property = this.unsensitive ? type.GetProperties().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetProperty(Name);
                            FieldInfo field = this.unsensitive ? type.GetFields().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetField(Name);
#else
                            PropertyInfo property = this.unsensitive ? Linq2.FirstOrDefault(type.GetProperties(), p => p.Name.ToUpper().Equals(Name)) : type.GetProperty(Name);
                            FieldInfo field = this.unsensitive ? Linq2.FirstOrDefault(type.GetFields(), p => p.Name.ToUpper().Equals(Name)) : type.GetField(Name);
#endif

                            if (property != null || field != null)
                            {
                                if (property != null)
                                    setter = new MemberSetter() { P = property.GetSetMethod() ?? property.GetSetMethod(false) }; // type.DelegateForSetPropertyValue(property.Name);

                                if (setter == null)
                                    if (field != null)
                                        setter = new MemberSetter() { F = field };// type.DelegateForSetFieldValue(field.Name);
                            }

                            innerDict[Name] = setter;
                        }
                    }
                }

                MemberSetter outSetter = null;
                innerDict.TryGetValue(Name, out outSetter);
                return outSetter != null && outSetter.Exists() ? outSetter : null;
            }
            return null;
        }

        public MemberGetter GetGetter(Object Object, String Name)
        {
            if (Object != null)
                return GetGetter(Object.GetType(), Name);
            return null;
        }

        public MemberGetter GetGetter(Type type, String Name)
        {
            if (type != null && !String.IsNullOrEmpty(Name))
            {
                if (this.unsensitive) Name = Name.ToUpper();

                Dictionary<String, MemberGetter> innerDict = null;

                if (!_cacheGetter.ContainsKey(type))
                {
                    lock (lck)
                    {
                        if (!_cacheGetter.ContainsKey(type))
                        {
                            _cacheGetter[type] = innerDict = new Dictionary<String, MemberGetter>();
                        }
                    }
                }
                innerDict = _cacheGetter[type];

                if (!innerDict.ContainsKey(Name))
                {
                    lock (lck)
                    {
                        if (!innerDict.ContainsKey(Name))
                        {
                            MemberGetter getter = null;
#if !NET20
                            PropertyInfo property = this.unsensitive ? type.GetProperties().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetProperty(Name);
                            FieldInfo field = this.unsensitive ? type.GetFields().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetField(Name);
#else
                            PropertyInfo property = this.unsensitive ? Linq2.FirstOrDefault(type.GetProperties(), p => p.Name.ToUpper().Equals(Name)) : type.GetProperty(Name);
                            FieldInfo field = this.unsensitive ? Linq2.FirstOrDefault(type.GetFields(), p => p.Name.ToUpper().Equals(Name)) : type.GetField(Name);
#endif
                            if (property != null || field != null)
                            {
                                if (property != null)
                                    getter = new MemberGetter() { P = property.GetGetMethod() ?? property.GetGetMethod(false) }; // type.DelegateForGetPropertyValue(property.Name);

                                if (getter == null)
                                    if (field != null)
                                        getter = new MemberGetter() { F = field }; // type.DelegateForGetFieldValue(field.Name);
                            }

                            innerDict[Name] = getter;
                        }
                    }
                }

                MemberGetter outGetter = null;
                innerDict.TryGetValue(Name, out outGetter);
                return outGetter != null && outGetter.Exists() ? outGetter : null;
            }
            return null;
        }
    }

    public class MemberGetter
    {
        public FieldInfo F;

        public MethodInfo P;

        public Object Get(Object O)
        {
            if (F != null)
                return F.GetValue(O);
            else
                return P.Invoke(O, null);
        }

        public Boolean Exists()
        {
            return F != null || P != null;
        }
    }

    public class MemberSetter
    {
        public FieldInfo F;

        public MethodInfo P;

        public void Set(Object O, Object V)
        {
            if (F != null)
                F.SetValue(O, V);
            else
                P.Invoke(O, new[] { V });
        }

        public Boolean Exists()
        {
            return F != null || P != null;
        }
    }
}
