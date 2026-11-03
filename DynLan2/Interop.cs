using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace DynLan2
{
    /// <summary>Method group of a host (.NET) object or type, callable from scripts.</summary>
    internal sealed class HostMethod
    {
        internal readonly object Target;   // null for static
        internal readonly Type Type;
        internal readonly string Name;
        internal readonly MethodInfo[] Overloads;
        internal HostMethod(object target, Type type, string name, MethodInfo[] overloads)
        {
            Target = target; Type = type; Name = name; Overloads = overloads;
        }
    }

    /// <summary>Conversions and reflection-based member access for host objects (cached per type/member).</summary>
    internal static class Interop
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Assembly Own = typeof(Value).Assembly;

        sealed class MemberEntry
        {
            public Func<object, object> Getter;
            public Action<object, object> Setter;
            public Type MemberType;
            public MethodInfo[] Methods;
        }

        static readonly ConcurrentDictionary<(Type, string, bool), MemberEntry> Members = new ConcurrentDictionary<(Type, string, bool), MemberEntry>();

        public static bool IsHostObject(object o) =>
            o != null && !(o is Tag) && !(o is string) && !(o is BigInteger) && (o.GetType().Assembly != Own || o is HostMethod);

        public static bool IsHost(Value v) => IsHostObject(v.O);

        // ------------------------------------------------------------ conversions
        public static Value ToValue(object o)
        {
            switch (o)
            {
                case null: return Value.None;
                case Value v: return v;
                case string s: return Value.Str(s);
                case bool b: return Value.Bool(b);
                case int i: return Value.Int(i);
                case long l: return Value.Int(l);
                case double d: return Value.Float(d);
                case float f: return Value.Float(f);
                case short sh: return Value.Int(sh);
                case byte by: return Value.Int(by);
                case sbyte sb: return Value.Int(sb);
                case ushort us: return Value.Int(us);
                case uint ui: return Value.Int(ui);
                case ulong ul: return ul > long.MaxValue ? Value.BigInt(ul) : Value.Int((long)ul);
                case decimal m: return Value.Float((double)m);
                case char c: return Value.Str(c.ToString());
                case BigInteger bi: return Value.BigInt(bi);
                case Enum e: return Value.Obj(e);
            }
            return Value.Obj(o);
        }

        /// <summary>Default .NET representation of a script value (copies containers).</summary>
        public static object ToObject(Value v)
        {
            object o = v.O;
            if (o == null) return null;
            if (ReferenceEquals(o, Value.TInt)) return v.N;
            if (ReferenceEquals(o, Value.TFloat)) return v.AsFloat;
            if (ReferenceEquals(o, Value.TBool)) return v.N != 0;
            if (ReferenceEquals(o, Value.TNone)) return null;
            switch (o)
            {
                case string s: return s;
                case BigInteger b: return b;
                case PyList l: return l.Items.Select(ToObject).ToList();
                case PyTuple t: return t.Items.Select(ToObject).ToArray();
                case PyDict d:
                    {
                        var r = new Dictionary<object, object>();
                        foreach (var kv in d.Items()) r[ToObject(kv.Key) ?? "None"] = ToObject(kv.Value);
                        return r;
                    }
                case PySet st: return new HashSet<object>(st.Items.Select(ToObject));
            }
            return o is Tag ? null : o;
        }

        public static object ToClr(Value v, Type t)
        {
            if (t == typeof(Value)) return v;
            if (t == typeof(object)) return ToObject(v);
            object o = v.O;
            if (v.IsNone)
            {
                if (!t.IsValueType || Nullable.GetUnderlyingType(t) != null) return null;
                throw Err.Type("cannot convert None to " + t.Name);
            }
            var under = Nullable.GetUnderlyingType(t);
            if (under != null) t = under;
            if (o != null && !(o is Tag) && t.IsInstanceOfType(o)) return o;
            if (v.IsInt || v.IsBool || o is BigInteger)
            {
                if (t == typeof(bool) && v.IsBool) return v.AsBool;
                if (t.IsEnum) return Enum.ToObject(t, v.N);
                if (t == typeof(int)) return checked((int)v.AsBig);
                if (t == typeof(long)) return (long)v.AsBig;
                if (t == typeof(double)) return v.ToDouble();
                if (t == typeof(float)) return (float)v.ToDouble();
                if (t == typeof(decimal)) return (decimal)v.AsBig;
                if (t == typeof(short)) return checked((short)v.N);
                if (t == typeof(byte)) return checked((byte)v.N);
                if (t == typeof(sbyte)) return checked((sbyte)v.N);
                if (t == typeof(ushort)) return checked((ushort)v.N);
                if (t == typeof(uint)) return checked((uint)v.N);
                if (t == typeof(ulong)) return (ulong)v.AsBig;
                if (t == typeof(BigInteger)) return v.AsBig;
                if (t == typeof(bool)) return v.N != 0;
                if (t == typeof(string)) return Ops.Str(v);
            }
            if (v.IsFloat)
            {
                if (t == typeof(double)) return v.AsFloat;
                if (t == typeof(float)) return (float)v.AsFloat;
                if (t == typeof(decimal)) return (decimal)v.AsFloat;
                if (t == typeof(int)) return checked((int)v.AsFloat);
                if (t == typeof(long)) return checked((long)v.AsFloat);
                if (t == typeof(string)) return Ops.Str(v);
            }
            if (o is string s)
            {
                if (t == typeof(char) && s.Length == 1) return s[0];
                if (t.IsEnum) return Enum.Parse(t, s, true);
                if (t == typeof(Guid)) return Guid.Parse(s);
                if (t == typeof(DateTime)) return DateTime.Parse(s, Inv);
            }
            if (t == typeof(string)) return Ops.Str(v);
            if (typeof(Delegate).IsAssignableFrom(t) && IsCallable(v)) return MakeDelegate(v, t);
            if (t.IsArray && (o is PyList || o is PyTuple))
            {
                var et = t.GetElementType();
                var items = Ops.ToList(v);
                var arr = Array.CreateInstance(et, items.Count);
                for (int i = 0; i < items.Count; i++) arr.SetValue(ToClr(items[i], et), i);
                return arr;
            }
            if (t.IsGenericType && (o is PyList || o is PyTuple || o is PySet))
            {
                var gd = t.GetGenericTypeDefinition();
                var args = t.GetGenericArguments();
                if (args.Length == 1 && (gd == typeof(List<>) || gd == typeof(IList<>) || gd == typeof(IEnumerable<>) || gd == typeof(ICollection<>)
                    || gd == typeof(IReadOnlyList<>) || gd == typeof(IReadOnlyCollection<>)))
                {
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(args[0]));
                    foreach (var x in Ops.Iterate(v)) list.Add(ToClr(x, args[0]));
                    return list;
                }
                if (args.Length == 1 && (gd == typeof(HashSet<>) || gd == typeof(ISet<>)))
                {
                    var set = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(args[0]));
                    var add = set.GetType().GetMethod("Add");
                    foreach (var x in Ops.Iterate(v)) add.Invoke(set, new[] { ToClr(x, args[0]) });
                    return set;
                }
            }
            if (t.IsGenericType && o is PyDict pd)
            {
                var gd = t.GetGenericTypeDefinition();
                var args = t.GetGenericArguments();
                if (args.Length == 2 && (gd == typeof(Dictionary<,>) || gd == typeof(IDictionary<,>) || gd == typeof(IReadOnlyDictionary<,>)))
                {
                    var dict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args));
                    foreach (var kv in pd.Items()) dict[ToClr(kv.Key, args[0])] = ToClr(kv.Value, args[1]);
                    return dict;
                }
            }
            if (t == typeof(IEnumerable) && (o is PyList || o is PyTuple)) return Ops.ToList(v).Select(ToObject).ToList();
            var obj = ToObject(v);
            if (obj != null && t.IsInstanceOfType(obj)) return obj;
            if (obj is IConvertible && typeof(IConvertible).IsAssignableFrom(t)) return Convert.ChangeType(obj, t, Inv);
            throw Err.Type("cannot convert '" + Ops.TypeName(v) + "' to " + t.Name);
        }

        static bool IsCallable(Value v) =>
            v.O is PyFunction || v.O is BoundMethod || v.O is BuiltinFunction || v.O is BuiltinMethod || v.O is PyClass;

        /// <summary>Wraps a script callable into a typed .NET delegate.</summary>
        static Delegate MakeDelegate(Value fn, Type delegateType)
        {
            var vm = VM.Current;
            var invoke = delegateType.GetMethod("Invoke");
            var ps = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var argsArr = Expression.NewArrayInit(typeof(object), ps.Select(p => Expression.Convert(p, typeof(object))));
            var call = Expression.Call(typeof(Interop).GetMethod(nameof(InvokeScript), BindingFlags.NonPublic | BindingFlags.Static),
                Expression.Constant(vm, typeof(VM)), Expression.Constant(fn, typeof(Value)), argsArr, Expression.Constant(invoke.ReturnType, typeof(Type)));
            Expression body = invoke.ReturnType == typeof(void) ? (Expression)call : Expression.Convert(call, invoke.ReturnType);
            return Expression.Lambda(delegateType, body, ps).Compile();
        }

        static object InvokeScript(VM vm, Value fn, object[] args, Type ret)
        {
            var vals = args.Select(ToValue).ToArray();
            var r = vm.CallValue(fn, vals, null);
            return ret == typeof(void) ? null : ToClr(r, ret);
        }

        public static string HostToString(object o)
        {
            if (o is IFormattable f) return f.ToString(null, Inv);
            return o?.ToString() ?? "None";
        }

        // ------------------------------------------------------------ members
        static MemberEntry Lookup(Type t, string name, bool isStatic)
        {
            return Members.GetOrAdd((t, name, isStatic), key =>
            {
                var (type, n, st) = key;
                if (ReflectionGuard.IsForbiddenType(type)) return null;
                var flags = BindingFlags.Public | (st ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.FlattenHierarchy;
                foreach (var ignoreCase in new[] { false, true })
                {
                    var f2 = ignoreCase ? flags | BindingFlags.IgnoreCase : flags;
                    string nn = ignoreCase ? n.Replace("_", "") : n;
                    var prop = type.GetProperties(f2).FirstOrDefault(p => string.Equals(p.Name, nn, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && p.GetIndexParameters().Length == 0 && !ReflectionGuard.IsForbidden(p, p.PropertyType));
                    if (prop != null)
                    {
                        var e = new MemberEntry { MemberType = prop.PropertyType };
                        if (prop.CanRead && prop.GetMethod.IsPublic) e.Getter = CompileGetter(prop, st);
                        if (prop.CanWrite && prop.SetMethod.IsPublic) e.Setter = (obj, val) => prop.SetValue(obj, val);
                        return e;
                    }
                    var field = type.GetFields(f2).FirstOrDefault(fi => string.Equals(fi.Name, nn, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && !ReflectionGuard.IsForbidden(fi, fi.FieldType));
                    if (field != null)
                    {
                        var e = new MemberEntry { MemberType = field.FieldType, Getter = obj => field.GetValue(obj) };
                        if (!field.IsInitOnly && !field.IsLiteral) e.Setter = (obj, val) => field.SetValue(obj, val);
                        return e;
                    }
                    var methods = type.GetMethods(f2).Where(m => string.Equals(m.Name, nn, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && !m.IsSpecialName && !ReflectionGuard.IsForbidden(m, m.ReturnType)).ToArray();
                    if (methods.Length > 0) return new MemberEntry { Methods = methods };
                    if (st)
                    {
                        var nested = type.GetNestedType(n, BindingFlags.Public);
                        if (nested != null && !ReflectionGuard.IsForbiddenType(nested)) return new MemberEntry { Getter = _ => nested };
                    }
                }
                return null;
            });
        }

        static Func<object, object> CompileGetter(PropertyInfo prop, bool isStatic)
        {
            try
            {
                var p = Expression.Parameter(typeof(object), "o");
                Expression inst = isStatic ? null : Expression.Convert(p, prop.DeclaringType);
                var body = Expression.Convert(Expression.Property(inst, prop), typeof(object));
                return Expression.Lambda<Func<object, object>>(body, p).Compile();
            }
            catch
            {
                return o => prop.GetValue(o);
            }
        }

        /// <summary>True when the host explicitly gave this type to the running script.</summary>
        static bool TypeAllowed(Type t)
        {
            var ctx = VM.Current?.Context;
            return ctx != null && ctx.AllowedTypes.Contains(t);
        }

        public static bool TryGetMember(object o, string name, out Value v)
        {
            if (o is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue(name, out var dv)) { v = ToValue(dv); return true; }
            }
            bool isStatic = o is Type;
            if (isStatic && !TypeAllowed((Type)o)) { v = default; return false; }
            Type t = isStatic ? (Type)o : o.GetType();
            var e = Lookup(t, name, isStatic);
            if (e == null)
            {
                v = default;
                return false;
            }
            if (e.Methods != null)
            {
                v = Value.Obj(new HostMethod(isStatic ? null : o, t, name, e.Methods));
                return true;
            }
            if (e.Getter == null) throw new ScriptException(Builtins.AttributeError, "property '" + name + "' is write-only");
            var got = e.Getter(isStatic ? null : o);
            // a nested type reached through an allowed type is allowed as well
            if (isStatic && got is Type nested && nested.DeclaringType == t) VM.Current.Context.AllowedTypes.Add(nested);
            v = ToValue(got);
            return true;
        }

        public static void SetMember(object o, string name, Value v)
        {
            if (o is IDictionary<string, object> dict) { dict[name] = ToObject(v); return; }
            bool isStatic = o is Type;
            Type t = isStatic ? (Type)o : o.GetType();
            var e = isStatic && !TypeAllowed(t) ? null : Lookup(t, name, isStatic);
            if (e == null || e.Setter == null)
                throw new ScriptException(Builtins.AttributeError, "'" + t.Name + "' object attribute '" + name + "' is read-only or missing");
            e.Setter(isStatic ? null : o, ToClr(v, e.MemberType));
        }

        // ------------------------------------------------------------ indexing
        public static Value GetIndex(object o, Value k)
        {
            switch (o)
            {
                case Array a when k.IsInt: return ToValue(a.GetValue(NormIdx(k.N, a.Length)));
                case IList l when k.IsInt: return ToValue(l[NormIdx(k.N, l.Count)]);
                case IDictionary d:
                    {
                        var key = DictKey(d, k);
                        if (!d.Contains(key)) throw Err.Key(k);
                        return ToValue(d[key]);
                    }
            }
            var idx = o.GetType().GetProperties().FirstOrDefault(p => p.GetIndexParameters().Length == 1);
            if (idx != null) return ToValue(idx.GetValue(o, new[] { ToClr(k, idx.GetIndexParameters()[0].ParameterType) }));
            throw Err.Type("'" + o.GetType().Name + "' object is not subscriptable");
        }

        public static void SetIndex(object o, Value k, Value v)
        {
            switch (o)
            {
                case Array a when k.IsInt: a.SetValue(ToClr(v, a.GetType().GetElementType()), NormIdx(k.N, a.Length)); return;
                case IList l when k.IsInt:
                    {
                        var et = l.GetType().IsGenericType ? l.GetType().GetGenericArguments()[0] : typeof(object);
                        l[NormIdx(k.N, l.Count)] = ToClr(v, et);
                        return;
                    }
                case IDictionary d:
                    {
                        var ga = d.GetType().IsGenericType ? d.GetType().GetGenericArguments() : null;
                        var key = ga != null && ga.Length == 2 ? ToClr(k, ga[0]) : ToObject(k);
                        d[key] = ga != null && ga.Length == 2 ? ToClr(v, ga[1]) : ToObject(v);
                        return;
                    }
            }
            var idx = o.GetType().GetProperties().FirstOrDefault(p => p.GetIndexParameters().Length == 1 && p.CanWrite);
            if (idx != null)
            {
                idx.SetValue(o, ToClr(v, idx.PropertyType), new[] { ToClr(k, idx.GetIndexParameters()[0].ParameterType) });
                return;
            }
            throw Err.Type("'" + o.GetType().Name + "' object does not support item assignment");
        }

        static int NormIdx(long i, int len)
        {
            if (i < 0) i += len;
            if (i < 0 || i >= len) throw Err.Index("index out of range");
            return (int)i;
        }

        static object DictKey(IDictionary d, Value k)
        {
            var ga = d.GetType().IsGenericType ? d.GetType().GetGenericArguments() : null;
            return ga != null && ga.Length == 2 ? ToClr(k, ga[0]) : ToObject(k);
        }

        // ------------------------------------------------------------ invocation
        public static Value Invoke(VM vm, object callable, ReadOnlySpan<Value> args, string[] kwn)
        {
            if (kwn != null && kwn.Length > 0) throw Err.Type("keyword arguments are not supported for .NET methods");
            switch (callable)
            {
                case HostMethod hm: return InvokeOverload(hm.Overloads, hm.Target, args, hm.Name);
                case Type t:
                    {
                        if (!TypeAllowed(t)) throw Err.Type("'" + t.Name + "' was not provided by the host and cannot be instantiated");
                        var ctors = t.GetConstructors();
                        if (args.Length == 0 && t.IsValueType) return ToValue(Activator.CreateInstance(t));
                        var best = Pick(ctors, args, out var conv);
                        if (best == null) throw Err.Type("no matching constructor for " + t.Name + " with " + args.Length + " argument(s)");
                        return ToValue(((ConstructorInfo)best).Invoke(conv));
                    }
                case Delegate d:
                    {
                        var ps = d.Method.GetParameters();
                        if (ps.Length != args.Length) throw Err.Type("delegate expects " + ps.Length + " argument(s), got " + args.Length);
                        var conv = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++) conv[i] = ToClr(args[i], ps[i].ParameterType);
                        return ToValue(d.DynamicInvoke(conv));
                    }
            }
            throw Err.Type("'" + callable.GetType().Name + "' object is not callable");
        }

        static Value InvokeOverload(MethodInfo[] overloads, object target, ReadOnlySpan<Value> args, string name)
        {
            var best = Pick(overloads, args, out var conv);
            if (best == null) throw Err.Type("no overload of " + name + "() accepts " + args.Length + " argument(s) of the given types");
            var mi = (MethodInfo)best;
            if (mi.ContainsGenericParameters) throw Err.Type("generic method " + name + "() cannot be called without type arguments");
            object r = mi.Invoke(target, conv);
            return mi.ReturnType == typeof(void) ? Value.None : ToValue(r);
        }

        static MethodBase Pick(IEnumerable<MethodBase> candidates, ReadOnlySpan<Value> args, out object[] converted)
        {
            MethodBase best = null;
            object[] bestArgs = null;
            int bestScore = int.MinValue;
            foreach (var m in candidates)
            {
                var ps = m.GetParameters();
                bool hasParams = ps.Length > 0 && ps[ps.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);
                int required = ps.Count(p => !p.HasDefaultValue && !p.IsDefined(typeof(ParamArrayAttribute), false));
                if (args.Length < required) continue;
                if (!hasParams && args.Length > ps.Length) continue;
                var conv = new object[ps.Length];
                int score = 0;
                bool ok = true;
                int fixedCount = hasParams ? ps.Length - 1 : ps.Length;
                for (int i = 0; i < fixedCount; i++)
                {
                    if (i >= args.Length) { conv[i] = ps[i].DefaultValue; continue; }
                    int s = Score(args[i], ps[i].ParameterType);
                    if (s < 0) { ok = false; break; }
                    score += s;
                    try { conv[i] = ToClr(args[i], ps[i].ParameterType); }
                    catch { ok = false; break; }
                }
                if (!ok) continue;
                if (hasParams)
                {
                    var et = ps[ps.Length - 1].ParameterType.GetElementType();
                    int n = Math.Max(0, args.Length - fixedCount);
                    var arr = Array.CreateInstance(et, n);
                    for (int i = 0; i < n; i++)
                    {
                        try { arr.SetValue(ToClr(args[fixedCount + i], et), i); }
                        catch { ok = false; break; }
                    }
                    if (!ok) continue;
                    conv[ps.Length - 1] = arr;
                    score -= 5;
                }
                score -= (ps.Length - args.Length) * 2;
                if (score > bestScore) { best = m; bestArgs = conv; bestScore = score; }
            }
            converted = bestArgs;
            return best;
        }

        static int Score(Value v, Type t)
        {
            if (t == typeof(Value)) return 10;
            var u = Nullable.GetUnderlyingType(t) ?? t;
            if (v.IsNone) return !t.IsValueType || u != t ? 5 : -1;
            if (v.IsBool) return u == typeof(bool) ? 10 : u == typeof(object) ? 2 : -1;
            if (v.IsInt) return u == typeof(long) ? 10 : u == typeof(int) ? 9 : u == typeof(double) ? 6 : u == typeof(decimal) ? 6 : u == typeof(float) ? 5
                : u == typeof(short) || u == typeof(byte) || u == typeof(uint) || u == typeof(ulong) ? 4 : u.IsEnum ? 3 : u == typeof(object) ? 2 : -1;
            if (v.IsFloat) return u == typeof(double) ? 10 : u == typeof(float) ? 8 : u == typeof(decimal) ? 7 : u == typeof(object) ? 2 : -1;
            if (v.O is string) return u == typeof(string) ? 10 : u == typeof(char) ? 6 : u.IsEnum ? 3 : u == typeof(object) ? 2 : -1;
            if (v.O != null && !(v.O is Tag) && u.IsInstanceOfType(v.O)) return u == typeof(object) ? 2 : 9;
            if (typeof(Delegate).IsAssignableFrom(u) && IsCallable(v)) return 7;
            if ((v.O is PyList || v.O is PyTuple || v.O is PyDict || v.O is PySet) && (u.IsArray || u.IsGenericType || u == typeof(IEnumerable) || u == typeof(object))) return 3;
            return -1;
        }

        // ------------------------------------------------------------ operators on host values
        public static Value HostBinary(string op, Value a, Value b)
        {
            string name;
            switch (op)
            {
                case "+": name = "op_Addition"; break;
                case "-": name = "op_Subtraction"; break;
                case "*": name = "op_Multiply"; break;
                case "/": name = "op_Division"; break;
                case "%": name = "op_Modulus"; break;
                default: throw Err.Unsupported(op, a, b);
            }
            foreach (var side in new[] { a, b })
            {
                if (!IsHost(side)) continue;
                var ms = side.O.GetType().GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == name && m.GetParameters().Length == 2);
                var best = Pick(ms, new[] { a, b }, out var conv);
                if (best != null) return ToValue(((MethodInfo)best).Invoke(null, conv));
            }
            throw Err.Unsupported(op, a, b);
        }

        public static Value HostUnary(string op, Value a)
        {
            var m = a.O.GetType().GetMethod("op_UnaryNegation", BindingFlags.Public | BindingFlags.Static);
            if (m != null) return ToValue(m.Invoke(null, new[] { a.O }));
            throw Err.Type("bad operand type for unary " + op + ": '" + Ops.TypeName(a) + "'");
        }
    }
}
