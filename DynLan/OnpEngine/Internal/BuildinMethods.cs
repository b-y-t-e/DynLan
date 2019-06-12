using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Threading;
using System.Collections;
using DynLan.OnpEngine.Models;
using DynLan.Helpers;
using DynLan.OnpEngine.InternalMethods;
using DynLan.OnpEngine.Logic;
using DynLan.Classes;

namespace DynLan.OnpEngine.Internal
{
    public static class BuildinMethods
    {
        private static Dictionary<String, DynMethod> methodsByNames;

        private static Dictionary<Guid, DynMethod> methodsByIds;

        private static Object lck = new Object();

        ////////////////////////////////////////////////////////////////////


        public static ExpressionMethodInfo FindByName(String Name)
        {
            Init();
            DynMethod expressionMethod = null;
            if (methodsByNames.TryGetValue(Name, out expressionMethod))
                return new ExpressionMethodInfo() { ID = expressionMethod.ID };
            return null;
        }

        public static void Add(DynMethod Method)
        {
            Init();
            lock (lck)
            {
                if (!methodsByIds.ContainsKey(Method.ID))
                {
                    foreach (String name in Method.Names)
#if CASE_INSENSITIVE
                        methodsByNames[name.ToUpper()] = Method;
#else
                        methodsByNames[name] = Method;
#endif
                    methodsByIds[Method.ID] = Method;
                }
            }
        }

        public static DynMethod GetByID(Guid ID)
        {
            Init();
            DynMethod expressionMethod = null;
            if (methodsByIds.TryGetValue(ID, out expressionMethod))
                return expressionMethod;
            return null;
        }

        public static void Init()
        {
            if (methodsByIds == null)
                lock (lck)
                    if (methodsByIds == null)
                    {
                        methodsByIds = new Dictionary<Guid, DynMethod>();
                        methodsByNames = new Dictionary<String, DynMethod>();
                        foreach (DynMethod operation in BuildMethods())
                        {
                            foreach (String name in operation.Names)
#if CASE_INSENSITIVE
                                methodsByNames[name.ToUpper()] = operation;
#else
                                methodsByNames[name] = operation;
#endif
                            methodsByIds[operation.ID] = operation;
                        }
                    }
        }

        ////////////////////////////////////////////////////////////////////

        private static IEnumerable<DynMethod> BuildMethods()
        {
            yield return new DynMethod()
            {
                Names = new[] { MethodSetValue.Name },
                Body = MethodSetValue.Execute
            };
            /*yield return new ExpressionMethod()
            {
                OperationNames = new[] { "check" },
                CalculateValueDelegate = (DynLanContext, Parameters) =>
                {
                    Object condition = Parameters != null && Parameters.Count > 0 ? Parameters[0] : null;
                    Object ifTrue = Parameters != null && Parameters.Count > 1 ? Parameters[1] : null;
                    Object ifFalse = Parameters != null && Parameters.Count > 2 ? Parameters[2] : null;

                    if (condition != null)
                    {
                        Type conditionType = condition.GetType();

                        if ((MyTypeHelper.IsNumeric(conditionType) && Convert.ToDecimal(condition) > 0) ||
                            (conditionType == typeof(String) && Convert.ToString(condition).Length > 0))
                        {
                            return new ExpressionMethodResult(ifTrue);
                        }
                        else
                        {
                            return new ExpressionMethodResult(ifFalse);
                        }
                    }

                    return new ExpressionMethodResult(null);
                }
            };*/
            yield return new DynMethod()
            {
                Names = new[] { "getdatetime" },
                Body = (DynLanContext, Parameters) =>
                {
                    return new DynMethodResult(DateTime.Now);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "getdatetimeastext" },
                Body = (DynLanContext, Parameters) =>
                {
                    DateTime now = DateTime.Now.Date;
                    String nowText = StringHelper.FormatDate(now, "yyyymmddThhmiss");
                    return new DynMethodResult(nowText);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "getdate" },
                Body = (DynLanContext, Parameters) =>
                {
                    return new DynMethodResult(DateTime.Now.Date);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "getdateastext" },
                Body = (DynLanContext, Parameters) =>
                {
                    DateTime now = DateTime.Now.Date;
                    String nowText = StringHelper.FormatDate(now, "yyyymmdd");
                    return new DynMethodResult(nowText);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "return" },
                Body = (DynLanContext, Parameters) =>
                {
#if !NET20
                    return new DynMethodResult(Parameters.FirstOrDefault());
#else
                    return new DynMethodResult(Linq2.FirstOrDefault(Parameters));
#endif
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "substring" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        String str = UniConvert.ToUniString(Parameters[0] ?? "");
                        if (Parameters.Count > 2)
                        {
                            Int32 start = Convert.ToInt32(Parameters[1]);
                            Int32 len = Convert.ToInt32(Parameters[2]);
                            if (start < str.Length)
                            {
                                if (start + len <= str.Length)
                                {
                                    return new DynMethodResult(str.Substring(start, len));
                                }
                                else
                                {
                                    return new DynMethodResult(str.Substring(start));
                                }
                            }
                            return new DynMethodResult("");
                        }
                        else if (Parameters.Count == 2)
                        {
                            Int32 start = Convert.ToInt32(Parameters[1]);
                            if (start < str.Length)
                            {
                                return new DynMethodResult(str.Substring(start));
                            }
                            return new DynMethodResult("");
                        }
                        return new DynMethodResult(str);
                    }
                    return new DynMethodResult("");
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "split" },
                Body = (DynLanContext, Parameters) =>
                {
#if NETCE
                    throw new NotImplementedException();
#else
                    if (Parameters.Count > 0)
                    {
                        List<String> list = new List<String>();
                        String str = UniConvert.ToUniString(Parameters[0] ?? "");
                        if (Parameters.Count > 1)
                        {
                            List<String> splitParams = new List<String>();
                            for (Int32 i = 1; i < Parameters.Count; i++)
                                splitParams.Add(UniConvert.ToString(Parameters[i] ?? ""));

#if NET20
                            foreach (String item in str.Split(splitParams.ToArray(), StringSplitOptions.None))
#else
                            foreach (String item in str.Split(splitParams.ToArray(), StringSplitOptions.None))
#endif
                                list.Add(item);
                        }
                        else
                        {
                            list.Add(str);
                        }
                        return new DynMethodResult(list);
                    }
                    return new DynMethodResult("");
#endif
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "str" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        return new DynMethodResult(
                            UniConvert.ToString(Parameters[0]));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "lower" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        if (Parameters[0] == null)
                            return null;
                        return new DynMethodResult(
                            UniConvert.ToString(Parameters[0]).ToLower());
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "upper" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        if (Parameters[0] == null)
                            return null;
                        return new DynMethodResult(
                            UniConvert.ToString(Parameters[0]).ToUpper());
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "ceil" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        return new DynMethodResult(
                            UniConvert.ToDecimal(Math.Ceiling(UniConvert.ToDouble(Parameters[0]))));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "eval" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
#if !NET20
                        DynLanProgram program = new Compiler().Compile(
                            UniConvert.ToString(Parameters.First()));
#else
                        DynLanProgram program = new Compiler().Compile(
                            UniConvert.ToString(Linq2.FirstOrDefault(Parameters)));
#endif

                        EvaluatorForMethods.EvaluateMethod(
                            null,
                            program,
                            null,
                            DynLanContext);

                        return new DynMethodResult()
                        {
                            NewContextCreated = true
                        };
                    }
                    return new DynMethodResult(null);
                }
            };
            /*yield return new ExpressionMethod()
            {
                OperationNames = new[] { "bindtoevent" },
                CalculateValueDelegate = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object obj = Parameters != null && Parameters.Count > 0 ? Parameters[0] : null;
                        String eventName = ((Parameters != null && Parameters.Count > 1 ? Parameters[1] : null) ?? "").ToString().Trim();
                        Object method = Parameters != null && Parameters.Count > 2 ? Parameters[2] : null;

                        throw new NotImplementedException();

                        MyReflectionHelper.BindToEvent(
                            obj,
                            eventName,
                            (s, o) =>
                            {

                            });
                    }
                    return new ExpressionMethodResult(null);
                }
            };*/
            /*yield return new ExpressionMethod()
            {
                OperationNames = new[] { "create" },
                CalculateValueDelegate = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count <= 0)
                        return new ExpressionMethodResult(null);

                    String typeName = UniConvert.ToString(Parameters[0]) ?? "";

                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var type = assembly.GetType(typeName);
                        if (type == null)
                            continue;

                        if (type.IsStatic())
                        {
                            return null;
                        }
                        else
                        {
                            var r = Activator.CreateInstance(type);
                            return new ExpressionMethodResult(r);
                        }
                    }

                    return new ExpressionMethodResult(null);
                }
            };*/
            yield return new DynMethod()
            {
                Names = new[] { "type" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count <= 0)
                        return new DynMethodResult(null);

                    String typeName = UniConvert.ToString(Parameters[0]) ?? "";
                    var type = MyAssemblyHelper.FindType(typeName);
                    return new DynMethodResult(type);
                }
            };
#if !PCL
            yield return new DynMethod()
            {
                Names = new[] { "sleep" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Int32 milliseconds = UniConvert.ToInt32(Parameters[0]);
                        Thread.Sleep(milliseconds);
                        return new DynMethodResult(milliseconds);
                    }
                    return new DynMethodResult(0);
                }
            };
#endif
            yield return new DynMethod()
            {
                Names = new[] { "new" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count <= 0)
                        return new DynMethodResult(null);

                    String typeName = UniConvert.ToString(Parameters[0]) ?? "";
                    var obj = MyAssemblyHelper.CreateType(typeName);
                    return new DynMethodResult(obj);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "list" },
                Body = (DynLanContext, Parameters) =>
                {
                    return new DynMethodResult(new List<Object>());
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "dictionary" },
                Body = (DynLanContext, Parameters) =>
                {
                    return new DynMethodResult(new Dictionary<String, Object>());
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "typeof" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
#if !NET20
                        if (Parameters.First() != null)
                            return new DynMethodResult(
                                Parameters.First().GetType());
#else
                        if (Linq2.FirstOrDefault(Parameters) != null)
                            return new DynMethodResult(
                                Linq2.FirstOrDefault(Parameters).GetType());
#endif
                    }
                    return new DynMethodResult(null);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "istypeof" },
                Body = (DynLanContext, Parameters) =>
                {
                    Boolean result = false;
                    if (Parameters.Count > 1)
                    {
                        Object obj = Parameters[0];
                        Object type = Parameters[1];

                        if (obj != null && type != null)
                        {
                            if (type is Type)
                            {
                                result = MyTypeHelper.Is(obj.GetType(), (Type)type);
                            }
                            else
                            {
                                String typeName = UniConvert.ToString(type);
                                result = MyTypeHelper.Is(obj.GetType(), typeName);
                            }
                        }
                    }
                    return new DynMethodResult(result);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "isnottypeof" },
                Body = (DynLanContext, Parameters) =>
                {
                    Boolean result = true;
                    if (Parameters.Count > 1)
                    {
                        Object obj = Parameters[0];
                        Object type = Parameters[1];

                        if (obj != null && type != null)
                        {
                            if (type is Type)
                            {
                                result = !MyTypeHelper.Is(obj.GetType(), (Type)type);
                            }
                            else
                            {
                                String typeName = UniConvert.ToString(type);
                                result = !MyTypeHelper.Is(obj.GetType(), typeName);
                            }
                        }
                    }
                    return new DynMethodResult(result);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "mod" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 1)
                    {
                        return new DynMethodResult(
                            UniConvert.ToDecimal(
                                UniConvert.ToDecimal(Parameters[0]) % UniConvert.ToDecimal(Parameters[1])));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "newguid" },
                Body = (DynLanContext, Parameters) =>
                {
                    return new DynMethodResult(Guid.NewGuid());
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "trim" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        object val = Parameters[0];
                        if (val == null)
                            return null;

                        return new DynMethodResult(
                            UniConvert.ToString(val).Trim());
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "float", "double", "tofloat", "todouble", "decimal", "todecimal" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        return new DynMethodResult(
                            UniConvert.ToDecimal(Parameters[0]));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "isfloat", "isdouble", "isdecimal", "isnumeric" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null)
                        {
                            if (MyTypeHelper.IsNumeric(val))
                            {
                                return new DynMethodResult(
                                    UniConvert.ToDecimal(true));
                            }
                            String strval = UniConvert.ToString(val);
                            Boolean result = false;

#if NETCE
                            try
                            {
                                Decimal.Parse(strval);
                                result = true;
                            }
                            catch { }
#else
                            Decimal numval = 0;
                            if (Decimal.TryParse(strval, out numval))
                                result = true;
#endif

                            return new DynMethodResult(
                                UniConvert.ToDecimal(result));
                        }
                    }
                    return new DynMethodResult(false);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "int", "toint" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        return new DynMethodResult(
                            UniConvert.ToInt64(Parameters[0]));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "floor" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        return new DynMethodResult(
                            UniConvert.ToDecimal(
                                Math.Floor(
                                    UniConvert.ToDouble(Parameters[0]))));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "round" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        if (Parameters.Count == 1)
                        {
                            return new DynMethodResult(
                                UniConvert.ToDecimal(
                                    Math.Round(
                                        UniConvert.ToDouble(Parameters[0]))));
                        }
                        else
                        {
                            return new DynMethodResult(
                                UniConvert.ToDecimal(
                                    Math.Round(
                                        UniConvert.ToDouble(Parameters[0]),
                                        Convert.ToInt32(Parameters[1]))));
                        }
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "abs" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        if (Parameters.Count == 1)
                        {
                            return new DynMethodResult(
                                UniConvert.ToDecimal(
                                    Math.Abs(
                                        UniConvert.ToDouble(Parameters[0]))));
                        }
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "coalesce" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        var value = Parameters[0];
                        if (value == null || value is Undefined)
                        {
                            for (var i = 1; i < Parameters.Count; i++)
                            {
                                var nextVal = Parameters[i];
                                if (nextVal != null && !(nextVal is Undefined))
                                    return new DynMethodResult(nextVal);
                            }
                        }
                        return new DynMethodResult(value);
                    }
                    return new DynMethodResult(null);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "not" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = (Parameters[0] ?? "");
                        if (val != null && !(val is Undefined))
                        {
                            Type type = val.GetType();
                            if (MyTypeHelper.IsNumeric(type))
                            {
                                Decimal numVal = UniConvert.ToDecimal(val);
                                return new DynMethodResult(Math.Sign(numVal) == 0 ? true : false);
                            }
                            else if (type.Name == "String")
                            {
                                return new DynMethodResult(
                                    String.IsNullOrEmpty(
                                        (String)val));
                            }
                            else
                            {
                                return new DynMethodResult(false);
                            }
                        }
                        else
                        {
                            return new DynMethodResult(true);
                        }
                    }
                    return new DynMethodResult(null);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "getyear" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(((DateTime)val).Year);
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "len", "length" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        String val = Convert.ToString(Parameters[0] ?? "");
                        return new DynMethodResult(val.Length);
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "getmonth" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(((DateTime)val).Month);
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "getday" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(((DateTime)val).Day);
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "hours" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(TimeSpan) || val.GetType() == typeof(TimeSpan?)))
                            return new DynMethodResult(Convert.ToDecimal(((TimeSpan)val).TotalHours));
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(Convert.ToDecimal(TimeSpan.FromTicks(((DateTime)val).Ticks).TotalHours));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "days" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(TimeSpan) || val.GetType() == typeof(TimeSpan?)))
                            return new DynMethodResult(Convert.ToDecimal(((TimeSpan)val).TotalDays));
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(Convert.ToDecimal(TimeSpan.FromTicks(((DateTime)val).Ticks).TotalDays));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "minutes" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(TimeSpan) || val.GetType() == typeof(TimeSpan?)))
                            return new DynMethodResult(Convert.ToDecimal(((TimeSpan)val).TotalMinutes));
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(Convert.ToDecimal(TimeSpan.FromTicks(((DateTime)val).Ticks).TotalMinutes));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "seconds" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(TimeSpan) || val.GetType() == typeof(TimeSpan?)))
                            return new DynMethodResult(Convert.ToDecimal(((TimeSpan)val).TotalSeconds));
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(Convert.ToDecimal(TimeSpan.FromTicks(((DateTime)val).Ticks).TotalSeconds));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "milliseconds" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        if (val != null && (val.GetType() == typeof(TimeSpan) || val.GetType() == typeof(TimeSpan?)))
                            return new DynMethodResult(Convert.ToDecimal(((TimeSpan)val).TotalMilliseconds));
                        if (val != null && (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?)))
                            return new DynMethodResult(Convert.ToDecimal(TimeSpan.FromTicks(((DateTime)val).Ticks).TotalMilliseconds));
                    }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "weekofyear" },
                Body = (DynLanContext, Parameters) =>
                {
                    Int32? val = null;
                    if (Parameters.Count > 0)
                    {
                        try
                        {
                            DateTime? val2 = UniConvert.ToDateTimeN(Parameters[0]);
                            if (val2 != null)
                                return new DynMethodResult(
                                    (Int32)Math.Floor(val2.Value.DayOfYear / 7.0) + 1);
                        }
                        catch { }
                    }
                    return new DynMethodResult(val);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "todatetime" },
                Body = (DynLanContext, Parameters) =>
                {
                    DateTime date = new DateTime();
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        String str = UniConvert.ToUniString(val ?? "");
                        if (UniConvert.TryParseUniDateTime(str, out date)) { }
                    }
                    return new DynMethodResult(date);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "todate" },
                Body = (DynLanContext, Parameters) =>
                {
                    DateTime date = new DateTime();
                    if (Parameters.Count > 0)
                    {
                        Object val = Parameters[0];
                        String str = UniConvert.ToUniString(val ?? "");
                        if (UniConvert.TryParseUniDateTime(str, out date)) { }
                    }
                    return new DynMethodResult(date.Date);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "format" },
                Body = (DynLanContext, Parameters) =>
                {
                    String r = "";
                    if (Parameters.Count > 1)
                    {
                        Object val = Parameters[0];
                        String format = Convert.ToString(Parameters[1] ?? "");
                        if (val != null && (val is DateTime || val is DateTime?))
                        {
                            r = StringHelper.FormatDate((DateTime)val, format);
                        }
                    }
                    return new DynMethodResult(r);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "tostring" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        var result = UniConvert.ToUniString(Parameters[0] ?? "");
                        if (Parameters.Count > 1)
                        {
                            var len = Convert.ToInt32(Parameters[1]);
                            var txt = (Convert.ToString(Parameters[2]) ?? "");
                            txt = txt.Replace("\"", "").Replace("'", "");

                            while (result.Length < len)
                            {
                                result = txt + result;
                            }
                        }
                        return new DynMethodResult(result);
                    }
                    return new DynMethodResult("");
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "sqrt" },
                Body = (DynLanContext, Parameters) =>
                {
                    try
                    {
                        if (Parameters.Count > 0)
                        {
                            Object val = Parameters[0];
                            Decimal result = UniConvert.ToDecimal(val);
                            return new DynMethodResult((decimal)Math.Sqrt((double)result));
                        }
                    }
                    catch { }
                    return new DynMethodResult(0);
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "adddays" },
                Body = (DynLanContext, Parameters) =>
                {
                    try
                    {
                        if (Parameters.Count > 0)
                        {
                            Object v = Parameters[0];
                            if (v != null && (v.GetType() == typeof(DateTime) || v.GetType() == typeof(DateTime?)))
                            {
                                DateTime date = (DateTime)v;
                                if (Parameters.Count > 0)
                                {
                                    var days = UniConvert.ToInt32(Parameters[1]);
                                    date = date.AddDays(days);
                                }
                                return new DynMethodResult(date);
                            }
                        }
                        return new DynMethodResult(
                            new DateTime());
                    }
                    catch
                    {
                        return new DynMethodResult(0);
                    }
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "addyears" },
                Body = (DynLanContext, Parameters) =>
                {
                    try
                    {
                        if (Parameters.Count > 0)
                        {
                            Object v = Parameters[0];
                            if (v != null && (v.GetType() == typeof(DateTime) || v.GetType() == typeof(DateTime?)))
                            {
                                DateTime date = (DateTime)v;
                                if (Parameters.Count > 0)
                                {
                                    var years = UniConvert.ToInt32(Parameters[1]);
                                    date = date.AddYears(years);
                                }
                                return new DynMethodResult(date);
                            }
                        }
                        return new DynMethodResult(new DateTime());
                    }
                    catch
                    {
                        return new DynMethodResult(0);
                    }
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "addmonths" },
                Body = (DynLanContext, Parameters) =>
                {
                    try
                    {
                        if (Parameters.Count > 0)
                        {
                            Object v = Parameters[0];
                            if (v != null && (v.GetType() == typeof(DateTime) || v.GetType() == typeof(DateTime?)))
                            {
                                DateTime date = (DateTime)v;
                                if (Parameters.Count > 0)
                                {
                                    var months = UniConvert.ToInt32(Parameters[1]);
                                    date = date.AddMonths(months);
                                }
                                return new DynMethodResult(date);
                            }
                        }
                        return new DynMethodResult(new DateTime());
                    }
                    catch
                    {
                        return new DynMethodResult(0);
                    }
                }
            };
            yield return new DynMethod()
            {
                Names = new[] { "rand" },
                Body = (DynLanContext, Parameters) =>
                {
                    return new DynMethodResult(
                        Convert.ToDecimal(
                            new Random(DateTime.Now.Millisecond).NextDouble()));
                }
            };
        }
    }
}