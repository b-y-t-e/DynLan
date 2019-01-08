using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Fasterflect;


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

        public void CopyTo(Object Source, Object Dest)
        {
            if (Source == null || Dest == null)
                return;

            foreach (String property in GetProperties(Source))
                SetValue(
                    Dest,
                    property,
                    GetValue(Source, property));
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
                    return getter(Item);
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
                    return getter(Item);
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
                    return (DataType)getter(Item);
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
                    setter(Item, Value);
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
                            setter(Item, newValue);
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

        public Type GetPropertyType(Object Object, String Name)
        {
            return Object != null ?
                GetPropertyType(Object.GetType(), Name) :
                null;
        }

        public Type GetPropertyType(Type Type, String Name)
        {
            if (this.unsensitive) Name = Name.ToUpper();

            if (!_cachePropertiesTypes.ContainsKey(Type))
            {
                lock (lck)
                {
                    if (!_cachePropertiesTypes.ContainsKey(Type))
                    {
                        _cachePropertiesTypes[Type] = Type.GetProperties().ToDictionary(
                            p => this.unsensitive ? p.Name.ToUpper() : p.Name,
                            p => p.PropertyType);
                    }
                }
            }
            return _cachePropertiesTypes.ContainsKey(Type) && _cachePropertiesTypes[Type].ContainsKey(Name) ?
                _cachePropertiesTypes[Type][Name] :
                null;
        }

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
                        _cacheProperties[Type] = Type.GetProperties().Select(p => this.unsensitive ? p.Name.ToUpper() : p.Name).ToArray();
                    }
                }
            }
            return _cacheProperties.ContainsKey(Type) ?
                _cacheProperties[Type] :
                new String[0];
        }


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
                            PropertyInfo property = this.unsensitive ? type.GetProperties().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetProperty(Name);
                            FieldInfo field = this.unsensitive ? type.GetFields().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetField(Name);

                            if (property != null || field != null)
                            {
                                if (property != null)
                                    setter = type.DelegateForSetPropertyValue(property.Name);

                                if (setter == null)
                                    if (field != null)
                                        setter = type.DelegateForSetFieldValue(field.Name);
                            }

                            innerDict[Name] = setter;
                        }
                    }
                }

                return innerDict.ContainsKey(Name) ? innerDict[Name] : null;
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
                            PropertyInfo property = this.unsensitive ? type.GetProperties().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetProperty(Name);
                            FieldInfo field = this.unsensitive ? type.GetFields().FirstOrDefault(p => p.Name.ToUpper().Equals(Name)) : type.GetField(Name);

                            if (property != null || field != null)
                            {
                                if (property != null)
                                    getter = type.DelegateForGetPropertyValue(property.Name);

                                if (getter == null)
                                    if (field != null)
                                        getter = type.DelegateForGetFieldValue(field.Name);
                            }

                            innerDict[Name] = getter;
                        }
                    }
                }

                return innerDict.ContainsKey(Name) ? innerDict[Name] : null;
            }
            return null;
        }
    }
}
