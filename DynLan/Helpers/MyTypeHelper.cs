using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace DynLan.Helpers
{
    [System.AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class NoCloneAttribute : Attribute
    {
        public NoCloneAttribute() { }
    }

    public static class MyTypeHelper
    {
        private static readonly Type type_timespan = typeof(TimeSpan);

        private static readonly Type type_timespan_nullable = typeof(TimeSpan?);

        private static readonly Type type_string = typeof(String);

        private static readonly Type type_datetime = typeof(DateTime);

        private static readonly Type type_datetime_nullable = typeof(DateTime?);

        //////////////////////////////////////////////////////////

        public static T CloneObject<T>(this T Item)
            where T : new()
        {
            if (Item == null)
                return default(T);

            T newItem = new T();
            RefSensitiveHelper.I.CopyTo(Item, newItem);
            return newItem;
        }

        public static Type GetListType(object someList)
        {
            if (someList == null)
                throw new ArgumentNullException();

            var type = someList.GetType();

            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
                return typeof(Object);

            return type.GetGenericArguments()[0];
        }
        
        public static Type GetType(Object Value1, Object Value2)
        {
            if (SameType(Value1, Value2))
            {
                if (Value1 != null)
                {
                    return MyTypeHelper.GetNonNullableType(Value1.GetType());
                }
            }
            return null;
        }

        private static Boolean SameType(Object Value1, Object Value2)
        {
            if (Value1 != null && Value2 != null)
            {
                Type type1 = MyTypeHelper.
                    GetNonNullableType(Value1.GetType());

                Type type2 = MyTypeHelper.
                    GetNonNullableType(Value2.GetType());

                return type1.Equals(type2);
            }
            else if (Value1 == null && Value2 == null)
            {
                return true;
            }
            return false;
        }

        //////////////////////////////////////////////////////////

        public static bool IsInteger(this Object AnyType)
        {
            if (AnyType != null)
                IsInteger(AnyType.GetType());
            return false;
        }

        public static bool IsInteger(this Type Type)
        {
            if (Type == typeof(Int32) ||
                Type == typeof(Int32?) ||
                Type == typeof(Int16) ||
                Type == typeof(Int16?) ||
                Type == typeof(Int64) ||
                Type == typeof(Int64?) ||
                Type == typeof(Byte) ||
                Type == typeof(Byte?) ||
                Type == typeof(SByte?) ||
                Type == typeof(SByte) ||
                Type == typeof(SByte?) ||
                Type == typeof(Boolean) ||
                Type == typeof(Boolean?))
                return true;
            return false;
        }
        
        public static Boolean IsStatic(this Type Type)
        {
            return Type.IsAbstract && Type.IsSealed;
        }

        public static bool IsPrimitive(this Object AnyType)
        {
            if (AnyType != null)
                return IsPrimitive(AnyType.GetType());
            return false;
        }

        public static Boolean IsPrimitive(this Type Type)
        {
            return Type.IsPrimitive ||
                Type.IsEnum ||
                Type.IsValueType ||
                Type == type_string;
        }

        public static bool IsBoolean(this Object AnyType)
        {
            if (AnyType != null)
                IsBoolean(AnyType.GetType());
            return false;
        }

        public static bool IsBoolean(this Type Type)
        {
            if (Type == typeof(Boolean) ||
                Type == typeof(Boolean?))
                return true;
            return false;
        }

        public static bool IsString(this Object AnyType)
        {
            if (AnyType != null)
                IsString(AnyType.GetType());
            return false;
        }

        public static bool IsString(this Type Type)
        {
            return (Type == type_string);
        }

        public static bool IsDateTime(this Object AnyType)
        {
            if (AnyType != null)
                IsDateTime(AnyType.GetType());
            return false;
        }

        public static bool IsDateTime(this Type Type)
        {
            return (Type == type_datetime ||
                Type == type_datetime_nullable);
        }

        public static bool IsTimeSpan(this Object AnyType)
        {
            if (AnyType != null)
                IsTimeSpan(AnyType.GetType());
            return false;
        }

        public static bool IsTimeSpan(this Type Type)
        {
            return (Type == type_timespan ||
                Type == type_timespan_nullable);
        }

        public static bool IsNumericOrNull(this Type Type)
        {
            return Type == null || IsNumeric(Type);
        }

        public static bool IsFloatNumeric(this Object AnyType)
        {
            if (AnyType != null)
                IsFloatNumeric(AnyType.GetType());
            return false;
        }

        public static bool IsFloatNumeric(this Type Type)
        {
            if (Type == null)
                return false;

            if (Type == typeof(Decimal) ||
                Type == typeof(Decimal?) ||
                Type == typeof(Single) ||
                Type == typeof(Single?) ||
                Type == typeof(Double) ||
                Type == typeof(Double?))
                return true;
            return false;
        }

        public static bool IsNumeric(this Object AnyType)
        {
            if (AnyType != null)
                IsNumeric(AnyType.GetType());
            return false;
        }

        public static bool IsNumeric(this Type Type)
        {
            if (Type == null)
                return false;

            if (Type == typeof(Decimal) ||
                Type == typeof(Decimal?) ||
                Type == typeof(Int32) ||
                Type == typeof(Int32?) ||
                Type == typeof(Int16) ||
                Type == typeof(Int16?) ||
                Type == typeof(Int64) ||
                Type == typeof(Int64?) ||
                Type == typeof(Single) ||
                Type == typeof(Single?) ||
                Type == typeof(Double) ||
                Type == typeof(Double?) ||
                Type == typeof(Byte) ||
                Type == typeof(Byte?) ||
                Type == typeof(SByte?) ||
                Type == typeof(SByte) ||
                Type == typeof(SByte?) ||
                Type == typeof(Boolean) ||
                Type == typeof(Boolean?))
                return true;
            return false;
        }

        ////////////////////

        public static Boolean IsEqualWithNumericConvert(this Object o1, Object o2)
        {
            if (o1 == o2) return true;
            else if (o1 != null && o2 == null) return false;
            else if (o1 == null && o2 != null) return false;
            else if (o1.Equals(o2)) return true;
            else
            {
                var t1 = o1.GetType();
                var t2 = o2.GetType();
                if (IsNumeric(t1) && IsNumeric(t2))
                {
                    Decimal v1 = Convert.ToDecimal(o1, CultureInfo.InvariantCulture);
                    Decimal v2 = Convert.ToDecimal(o2, CultureInfo.InvariantCulture);
                    return v1.Equals(v2);
                }
                else
                {
                    return false;
                }
            }
        }

        public static Boolean IsEqual(this Object Obj1, Object Obj2)
        {
            if (Obj1 == Obj2) return true;
            else if (Obj1 != null && Obj2 == null) return false;
            else if (Obj1 == null && Obj2 != null) return false;
            else return Obj1.Equals(Obj2);
        }

        public static Boolean EqualIn(this Object Object, params Object[] Values)
        {
            if (Values != null)
            {
                foreach (var lValue in Values)
                    if (Object.IsEqual(lValue))
                        return true;
            }
            return false;
        }

        ////////////////////

        public static Boolean Is(this Type Type, String Subclass)
        {
            return PrivIs(Type, Subclass);
        }

        private static Boolean PrivIs(Type Type, String Subclass)
        {
            if (Type.Name == Subclass)
            {
                return true;
            }
            else
            {
                var lBase = Type.BaseType;
                if (lBase != null)
                    return PrivIs(lBase, Subclass);
                return false;
            }
        }

        ////////////////////

        public static Boolean Is(this Type Type, Type Subclass)
        {
            if (Subclass.IsInterface)
            {
                return Type.GetInterfaces().Contains(Subclass);
            }
            else
            {
                return PrivIs(Type, Subclass);
            }
        }

        private static Boolean PrivIs(Type Type, Type Subclass)
        {
            if (Type == Subclass)
                return true;
            else
            {
                var lBase = Type.BaseType;
                if (lBase != null)
                    return PrivIs(lBase, Subclass);
                return false;
            }
        }

        ////////////////////

        public static Boolean IsEnumerable(this Type Type)
        {
            return Type.Is(typeof(IEnumerable));
        }

        public static bool IsNullableType(this Type type)
        {
            if (type == null)
                return false;

            return type != null && ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) || type.IsClass);
        }

        public static bool IsNullAssignable(Type type)
        {
            if (type == null)
                return false;

            return !type.IsValueType || IsNullableType(type);
        }

        ////////////////////

        public static Type GetNonNullableType(this Type type)
        {
            if (type == null)
                return null;

            if (IsNullableType(type) && type != type_string)
            {
                var genericArguments = type.GetGenericArguments();
                if (genericArguments.Length > 0)
                    return genericArguments[0];
            }
            return type;
        }

        ////////////////////

        public static Type GetMemberType(MemberInfo mi)
        {
            if (mi == null)
                return null;

            FieldInfo fi = mi as FieldInfo;
            if (fi != null) return fi.FieldType;
            PropertyInfo pi = mi as PropertyInfo;
            if (pi != null) return pi.PropertyType;
            EventInfo ei = mi as EventInfo;
            if (ei != null) return ei.EventHandlerType;
            return null;
        }

        ////////////////////

        public static Int32 ToInt(this Int64 Val) { return Convert.ToInt32(Val, CultureInfo.InvariantCulture); }

        public static Int32 ToInt(this Double Val) { return Convert.ToInt32(Val, CultureInfo.InvariantCulture); }

        public static Int32 ToInt(this Decimal Val) { return Convert.ToInt32(Val, CultureInfo.InvariantCulture); }

        public static Int32 ToInt(this Single Val) { return Convert.ToInt32(Val, CultureInfo.InvariantCulture); }

        ////////////////////

        public static Double ToDouble(this Int32 Val) { return Convert.ToDouble(Val, CultureInfo.InvariantCulture); }

        public static Double ToDouble(this Int64 Val) { return Convert.ToDouble(Val, CultureInfo.InvariantCulture); }

        public static Double ToDouble(this Decimal Val) { return Convert.ToDouble(Val, CultureInfo.InvariantCulture); }

        public static Double ToDouble(this Single Val) { return Convert.ToDouble(Val, CultureInfo.InvariantCulture); }

        ////////////////////

        public static T ConvertTo<T>(this Object Object)
        {
            Object value = ConvertTo(Object, typeof(T));
            if (value == null)
            {
                if (default(T) == null)
                {
                    return (T)(object)null;
                }
                else
                {
                    return default(T);
                }
            }
            else
            {
                return (T)value;
            }
        }

        public static Object ConvertTo(this Object Value, Type DestinationType)
        {
            if (Value == null)
            {
                return null;
            }
            else
            {
                Type type = Value.GetType();
                if (type == DestinationType || DestinationType == typeof(Object))
                {
                    return Value;
                }
                else
                {
                    if (DestinationType == typeof(UInt16))
                        return CorrectNumericValue(Convert.ToUInt16(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(UInt16?))
                        return CorrectNumericValue(Convert.ToUInt16(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Int16))
                        return CorrectNumericValue(Convert.ToInt16(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Int16?))
                        return CorrectNumericValue(Convert.ToInt16(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Int32))
                        return CorrectNumericValue(Convert.ToInt32(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Int32?))
                        return CorrectNumericValue(Convert.ToInt32(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(UInt32))
                        return CorrectNumericValue(Convert.ToUInt32(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(UInt32?))
                        return CorrectNumericValue(Convert.ToUInt32(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Int64))
                        return CorrectNumericValue(Convert.ToInt64(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Int64?))
                        return CorrectNumericValue(Convert.ToInt64(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(UInt64))
                        return CorrectNumericValue(Convert.ToUInt64(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(UInt64?))
                        return CorrectNumericValue(Convert.ToUInt64(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Single))
                        return CorrectNumericValue(Convert.ToSingle(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Single?))
                        return CorrectNumericValue(Convert.ToSingle(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Double))
                        return CorrectNumericValue(Convert.ToDouble(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Double?))
                        return CorrectNumericValue(Convert.ToDouble(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Decimal))
                        return CorrectNumericValue(Convert.ToDecimal(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Decimal?))
                        return CorrectNumericValue(Convert.ToDecimal(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == type_datetime)
                        return CorrectNumericValue(Convert.ToDateTime(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == type_datetime_nullable)
                        return CorrectNumericValue(Convert.ToDateTime(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == type_timespan && (type == type_datetime || type == type_datetime_nullable))
                        return ((DateTime)Value).TimeOfDay;
                    else if (DestinationType == type_timespan_nullable && (type == type_datetime || type == type_datetime_nullable))
                        return ((DateTime)Value).TimeOfDay;

                    else if (DestinationType == typeof(Byte))
                        return CorrectNumericValue(Convert.ToByte(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Byte?))
                        return CorrectNumericValue(Convert.ToByte(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Boolean))
                        return CorrectNumericValue(Convert.ToBoolean(Value, CultureInfo.InvariantCulture));
                    else if (DestinationType == typeof(Boolean?))
                        return CorrectNumericValue(Convert.ToBoolean(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType == typeof(Guid))
                        return (new Guid(Convert.ToString(Value, CultureInfo.InvariantCulture)));
                    else if (DestinationType == typeof(Guid?))
                        return (new Guid(Convert.ToString(Value, CultureInfo.InvariantCulture)));

                    else if (DestinationType == type_string)
                        return CorrectNumericValue(Convert.ToString(Value, CultureInfo.InvariantCulture));

                    else if (DestinationType.IsEnum)
                        return Enum.ToObject(DestinationType, Value);

                    else
                        return Convert.ChangeType(Value, DestinationType, CultureInfo.InvariantCulture);
                }
            }
        }

        private static Object CorrectNumericValue(Object Value)
        {
            if (Value == null || "".Equals(Value))
            {
                return 0M;
            }
            else
            {
                return Value;
            }
        }
    }
}
