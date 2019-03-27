using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using DynLan.Exceptions;
using DynLan.OnpEngine.Symbols;
using DynLan.Helpers;
using DynLan.Classes;

namespace DynLan.OnpEngine.Logic
{
    public static class OperationHelper
    {
        private static readonly Int64 ticks_one_day = TimeSpan.FromDays(1).Ticks;

        ////////////////////////////////////////////////

        public static Object Do(
            OperatorType OperationType,
            Object Value1,
            Object Value2)
        {
            Object result = null;

            Type type1 = (Value1 == null ?
                null : MyTypeHelper.GetNonNullableType(Value1.GetType()));

            Type type2 = (Value2 == null ?
                null : MyTypeHelper.GetNonNullableType(Value2.GetType()));

            if (Value1 is Undefined || Value2 is Undefined)
            {
                if (OperationType == OperatorType.EQUAL)
                {
                    return Value1 is Undefined && Value2 is Undefined;
                }
                else if (OperationType == OperatorType.NOT_EQUAL)
                {
                    return !(Value1 is Undefined && Value2 is Undefined);
                }
                return null;
            }

            if (type1 == null || type2 == null)
            {
                if (OperationType == OperatorType.EQUAL)
                {
                    return type1 == null && type2 == null;
                }
                else if (OperationType == OperatorType.NOT_EQUAL)
                {
                    return !(type1 == null && type2 == null);
                }

                return null;
            }

            if (OperationType == OperatorType.EQUAL)
            {
                Boolean r = MyTypeHelper.IsEqualWithNumericConvert(Value1, Value2);
                return r;
            }
            else if (OperationType == OperatorType.NOT_EQUAL)
            {
                Boolean r = !MyTypeHelper.IsEqualWithNumericConvert(Value1, Value2);
                return r;
            }
            else if (OperationType == OperatorType.PROPERTY)
            {                
                Object outValue = MyReflectionHelper.GetValue(InternalTypeConverter.ToOuter(Value1), UniConvert.ToString(Value2));
                return InternalTypeConverter.ToInner(outValue);
            }
            else if (MyTypeHelper.IsNumeric(type1) && MyTypeHelper.IsNumeric(type2))
            {
                result = ExecuteOperatorOnNumericValues(Value1, Value2, OperationType);
            }
            else
            {
                if (type1 == typeof(TimeSpan) || type2 == typeof(TimeSpan))
                {
                    throw new DynLanInvalidOperationException("Invalid operation type " + OperationType + " (TimeSpan & TimeSpan)");
                }
                else if (OperationType == OperatorType.MULTIPLY && MyTypeHelper.IsNumeric(type1) && type2 == typeof(String))
                {
                    StringBuilder outString = new StringBuilder();
                    for (var i = 0; i < UniConvert.ToInt32(Value1); i++)
                        outString.Append(Value2);
                    return outString.ToString();
                }
                else if (OperationType == OperatorType.MULTIPLY && MyTypeHelper.IsNumeric(type2) && type1 == typeof(String))
                {
                    StringBuilder outString = new StringBuilder();
                    for (var i = 0; i < UniConvert.ToInt32(Value2); i++)
                        outString.Append(Value1);
                    return outString.ToString();
                }
                else if (type1 == typeof(String) || type2 == typeof(String))
                {
                    String str1 = (UniConvert.ToUniString(InternalTypeConverter.ToOuter(Value1)) ?? "");
                    String str2 = (UniConvert.ToUniString(InternalTypeConverter.ToOuter(Value2)) ?? "");
                    Int32 compareResult = str1.CompareTo(str2);

                    if (OperationType == OperatorType.GREATER)
                        result = compareResult > 0;
                    else if (OperationType == OperatorType.SMALLER)
                        result = compareResult < 0;
                    else if (OperationType == OperatorType.GREATER_OR_EQUAL)
                        result = compareResult >= 0;
                    else if (OperationType == OperatorType.SMALLER_OR_EQUAL)
                        result = compareResult <= 0;
                    else if (OperationType == OperatorType.EQUAL)
                        result = compareResult == 0;
                    else if (OperationType == OperatorType.NOT_EQUAL)
                        result = compareResult != 0;
                    else return str1 + str2;
                }
                else if (type1 == typeof(InternalDateTime) && type2 == typeof(InternalDateTime))
                {
                    var lN1 = (InternalDateTime)Value1;
                    var lN2 = (InternalDateTime)Value2;
                    if (OperationType == OperatorType.PLUS)
                        result = new InternalDateTime(lN1.Ticks + lN2.Ticks);
                    else if (OperationType == OperatorType.SUBTRACT)
                        result = new InternalDateTime(lN1.Ticks - lN2.Ticks);
                    else if (OperationType == OperatorType.GREATER)
                        result = lN1.Ticks > lN2.Ticks;
                    else if (OperationType == OperatorType.SMALLER)
                        result = lN1.Ticks < lN2.Ticks;
                    else if (OperationType == OperatorType.GREATER_OR_EQUAL)
                        result = lN1.Ticks >= lN2.Ticks;
                    else if (OperationType == OperatorType.SMALLER_OR_EQUAL)
                        result = lN1.Ticks <= lN2.Ticks;
                    else if (OperationType == OperatorType.EQUAL)
                        result = lN1.Ticks == lN2.Ticks;
                    else if (OperationType == OperatorType.NOT_EQUAL)
                        result = lN1.Ticks != lN2.Ticks;
                    else throw new DynLanInvalidOperationException();
                }
                else if (MyTypeHelper.IsNumeric(type1) && type2 == typeof(InternalDateTime))
                {
                    var lN1 = UniConvert.ToDecimal(Value1);
                    var lN2 = (InternalDateTime)Value2;
                    if (OperationType == OperatorType.PLUS)
                        result = new InternalDateTime(lN2.Ticks + (Int64)(ticks_one_day * lN1));
                    else if (OperationType == OperatorType.SUBTRACT)
                        result = new InternalDateTime((Int64)(lN1 * ticks_one_day) - lN2.Ticks);
                    else if (OperationType == OperatorType.MULTIPLY)
                        result = new InternalDateTime((Int64)(lN1 * lN2.Ticks));
                    else if (OperationType == OperatorType.DIVIDE)
                        result = new InternalDateTime((Int64)(lN1 / lN2.Ticks));
                    else throw new DynLanInvalidOperationException("Invalid operation type " + OperationType + " (numeric & DateTime)");
                }
                else if (type1 == typeof(InternalDateTime) && MyTypeHelper.IsNumeric(type2))
                {
                    var lN1 = (InternalDateTime)Value1;
                    var lN2 = UniConvert.ToDecimal(Value2);
                    if (OperationType == OperatorType.PLUS)
                        result = new InternalDateTime(lN1.Ticks + (Int64)(ticks_one_day * lN2));
                    else if (OperationType == OperatorType.SUBTRACT)
                        result = new InternalDateTime(lN1.Ticks - (Int64)(ticks_one_day * lN2));
                    else if (OperationType == OperatorType.MULTIPLY)
                        result = new InternalDateTime((Int64)(lN1.Ticks * lN2));
                    else if (OperationType == OperatorType.DIVIDE)
                        result = new InternalDateTime((Int64)(lN1.Ticks / lN2));
                    else throw new DynLanInvalidOperationException("Invalid operation type " + OperationType + " (DateTime & numeric)");
                }
                else
                {
                    throw new DynLanInvalidOperationException("Invalid operation type " + OperationType + " (" + (type1 == null ? "null" : type1.Name) + " & " + (type2 == null ? "null" : type2.Name) + ")");
                }
            }

            return result;
        }

        private static object ExecuteOperatorOnNumericValues(Object Value1, Object Value2, OperatorType OperationType)
        {
            if (MyTypeHelper.IsFloatNumeric(Value1) || MyTypeHelper.IsFloatNumeric(Value2))
            {
                return ExecuteOperatorOnDecimals(Value1, Value2, OperationType);
            }
            else
            {
                return ExecuteOperatorOnIntegers(Value1, Value2, OperationType);
            }
        }

        private static object ExecuteOperatorOnIntegers(Object Value1, Object Value2, OperatorType OperationType)
        {
            Object result = null;

            Int64 value1 = UniConvert.
                ToInt64(Value1);

            Int64 value2 = UniConvert.
                ToInt64(Value2);

            if (OperationType == OperatorType.PLUS)
                result = value1 + value2;
            else if (OperationType == OperatorType.DIVIDE)
                result = value1 / value2;
            else if (OperationType == OperatorType.MULTIPLY)
                result = value1 * value2;
            else if (OperationType == OperatorType.SUBTRACT)
                result = value1 - value2;
            else if (OperationType == OperatorType.GREATER)
                result = value1 > value2;
            else if (OperationType == OperatorType.SMALLER)
                result = value1 < value2;
            else if (OperationType == OperatorType.GREATER_OR_EQUAL)
                result = value1 >= value2;
            else if (OperationType == OperatorType.SMALLER_OR_EQUAL)
                result = value1 <= value2;
            else if (OperationType == OperatorType.EQUAL)
                result = value1 == value2;
            else if (OperationType == OperatorType.NOT_EQUAL)
                result = value1 != value2;
            else if (OperationType == OperatorType.AND)
                result = (value1 != 0) && (value2 != 0);
            else if (OperationType == OperatorType.OR)
                result = (value1 != 0) || (value2 != 0);
            else
                throw new DynLanInvalidOperationException();

            return result;
        }

        private static object ExecuteOperatorOnDecimals(Object Value1, Object Value2, OperatorType OperationType)
        {
            Object result = null;

            Decimal value1 = UniConvert.
                ToDecimal(Value1);

            Decimal value2 = UniConvert.
                ToDecimal(Value2);

            if (OperationType == OperatorType.PLUS)
                result = value1 + value2;
            else if (OperationType == OperatorType.DIVIDE)
                result = value1 / value2;
            else if (OperationType == OperatorType.MULTIPLY)
                result = value1 * value2;
            else if (OperationType == OperatorType.SUBTRACT)
                result = value1 - value2;
            else if (OperationType == OperatorType.GREATER)
                result = value1 > value2;
            else if (OperationType == OperatorType.SMALLER)
                result = value1 < value2;
            else if (OperationType == OperatorType.GREATER_OR_EQUAL)
                result = value1 >= value2;
            else if (OperationType == OperatorType.SMALLER_OR_EQUAL)
                result = value1 <= value2;
            else if (OperationType == OperatorType.EQUAL)
                result = value1 == value2;
            else if (OperationType == OperatorType.NOT_EQUAL)
                result = value1 != value2;
            else if (OperationType == OperatorType.AND)
                result = (value1 != 0) && (value2 != 0);
            else if (OperationType == OperatorType.OR)
                result = (value1 != 0) || (value2 != 0);
            else
                throw new DynLanInvalidOperationException();

            return result;
        }
    }
}