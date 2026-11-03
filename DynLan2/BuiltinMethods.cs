using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace DynLan2
{
    internal static partial class Builtins
    {
        static readonly Dictionary<string, MethodFn> StrM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> ListM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> DictM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> SetM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> TupleM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> IntM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> FloatM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> GenM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> RangeM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);
        static readonly Dictionary<string, MethodFn> IterM = new Dictionary<string, MethodFn>(StringComparer.Ordinal);

        internal static MethodFn FindMethod(Value self, string name)
        {
            Dictionary<string, MethodFn> t;
            switch (self.O)
            {
                case string _: t = StrM; break;
                case PyList _: t = ListM; break;
                case PyDict _: t = DictM; break;
                case PySet _: t = SetM; break;
                case PyTuple _: t = TupleM; break;
                case BigInteger _: t = IntM; break;
                case PyGenerator _: t = GenM; break;
                case PyRange _: t = RangeM; break;
                case PyIterator _: t = IterM; break;
                case PyProperty p:
                    return TryPropertyAttr(self, p, name, out var pv) && pv.O is BuiltinMethod bm ? bm.Fn : null;
                default:
                    if (self.IsInt || self.IsBool) t = IntM;
                    else if (self.IsFloat) t = FloatM;
                    else return null;
                    break;
            }
            return t.TryGetValue(name, out var fn) ? fn : null;
        }

        static Dictionary<string, MethodFn> MethodsOf(PyType t)
        {
            if (t == StrType) return StrM;
            if (t == ListType) return ListM;
            if (t == DictType) return DictM;
            if (t == SetType || t == FrozenSetType) return SetM;
            if (t == TupleType) return TupleM;
            if (t == IntType || t == BoolType) return IntM;
            if (t == FloatType) return FloatM;
            return null;
        }

        internal static bool TryGetBuiltinAttr(Value o, string name, out Value v)
        {
            var fn = FindMethod(o, name);
            if (fn != null)
            {
                v = Value.Obj(new BuiltinMethod(o, name, fn));
                return true;
            }
            switch (o.O)
            {
                case PyType t:
                    if (name == "__name__") { v = Value.Str(t.Name); return true; }
                    if (t == DictType && name == "fromkeys")
                    {
                        v = Value.Obj(Registry["dict.fromkeys"]);
                        return true;
                    }
                    if (t == StrType && name == "maketrans") { v = Value.Obj(Registry["str.maketrans"]); return true; }
                    var tbl = MethodsOf(t);
                    if (tbl != null && tbl.TryGetValue(name, out var mf))
                    {
                        string key = "unbound:" + t.Name + "." + name;
                        lock (Registry)
                        {
                            if (!Registry.TryGetValue(key, out var bf))
                                bf = Fn(key, name, (vm, a, kw) =>
                                {
                                    if (a.Length == 0) throw Err.Type("descriptor '" + name + "' of '" + t.Name + "' object needs an argument");
                                    return mf(vm, a[0], a.Slice(1), kw);
                                });
                            v = Value.Obj(bf);
                        }
                        return true;
                    }
                    break;
                case PyRange r:
                    if (name == "start") { v = Value.Int(r.Start); return true; }
                    if (name == "stop") { v = Value.Int(r.Stop); return true; }
                    if (name == "step") { v = Value.Int(r.Step); return true; }
                    break;
                case PySlice s:
                    if (name == "start") { v = s.Start; return true; }
                    if (name == "stop") { v = s.Stop; return true; }
                    if (name == "step") { v = s.Step; return true; }
                    break;
                case BuiltinFunction bf2:
                    if (name == "__name__") { v = Value.Str(bf2.Name); return true; }
                    break;
                case BoundMethod bm:
                    if (name == "__self__") { v = bm.Self; return true; }
                    if (name == "__func__") { v = bm.Func; return true; }
                    if (name == "__name__") return Ops.TryGetAttr(bm.Func, name, out v);
                    break;
            }
            if (o.IsInt || o.IsBool || o.O is BigInteger)
            {
                if (name == "real" || name == "numerator") { v = o.IsBool ? Value.Int(o.N) : o; return true; }
                if (name == "imag") { v = Value.Int(0); return true; }
                if (name == "denominator") { v = Value.Int(1); return true; }
            }
            if (o.IsFloat)
            {
                if (name == "real") { v = o; return true; }
                if (name == "imag") { v = Value.Float(0); return true; }
            }
            v = default;
            return false;
        }

        static string S(Value v) => (string)v.O;
        static PyList L(Value v) => (PyList)v.O;
        static PyDict D(Value v) => (PyDict)v.O;
        static PySet St(Value v) => (PySet)v.O;

        static void InitMethods()
        {
            // ---------------------------------------------------- str
            StrM["upper"] = (vm, s, a, kw) => Value.Str(S(s).ToUpperInvariant());
            StrM["lower"] = (vm, s, a, kw) => Value.Str(S(s).ToLowerInvariant());
            StrM["casefold"] = (vm, s, a, kw) => Value.Str(S(s).ToLowerInvariant());
            StrM["swapcase"] = (vm, s, a, kw) =>
            {
                var cs = S(s).ToCharArray();
                for (int i = 0; i < cs.Length; i++) cs[i] = char.IsUpper(cs[i]) ? char.ToLowerInvariant(cs[i]) : char.ToUpperInvariant(cs[i]);
                return Value.Str(new string(cs));
            };
            StrM["capitalize"] = (vm, s, a, kw) =>
            {
                var x = S(s);
                return Value.Str(x.Length == 0 ? x : char.ToUpperInvariant(x[0]) + x.Substring(1).ToLowerInvariant());
            };
            StrM["title"] = (vm, s, a, kw) =>
            {
                var sb = new StringBuilder();
                bool prevLetter = false;
                foreach (char c in S(s))
                {
                    sb.Append(prevLetter ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c));
                    prevLetter = char.IsLetter(c);
                }
                return Value.Str(sb.ToString());
            };
            StrM["strip"] = (vm, s, a, kw) => Value.Str(a.Length > 0 && !a[0].IsNone ? S(s).Trim(S(a[0]).ToCharArray()) : S(s).Trim());
            StrM["lstrip"] = (vm, s, a, kw) => Value.Str(a.Length > 0 && !a[0].IsNone ? S(s).TrimStart(S(a[0]).ToCharArray()) : S(s).TrimStart());
            StrM["rstrip"] = (vm, s, a, kw) => Value.Str(a.Length > 0 && !a[0].IsNone ? S(s).TrimEnd(S(a[0]).ToCharArray()) : S(s).TrimEnd());
            StrM["split"] = (vm, s, a, kw) => Split(S(s), a.Length > 0 ? a[0] : kw?.Get("sep", Value.None) ?? Value.None,
                a.Length > 1 ? (int)ToLong(a[1], "maxsplit") : kw != null ? (int)ToLong(kw.Get("maxsplit", Value.Int(-1)), "maxsplit") : -1, false);
            StrM["rsplit"] = (vm, s, a, kw) => Split(S(s), a.Length > 0 ? a[0] : kw?.Get("sep", Value.None) ?? Value.None,
                a.Length > 1 ? (int)ToLong(a[1], "maxsplit") : kw != null ? (int)ToLong(kw.Get("maxsplit", Value.Int(-1)), "maxsplit") : -1, true);
            StrM["splitlines"] = (vm, s, a, kw) =>
            {
                bool keep = a.Length > 0 && Ops.Truthy(a[0]);
                var res = new List<Value>();
                string x = S(s);
                int start = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    if (x[i] == '\n' || x[i] == '\r')
                    {
                        int end = i;
                        if (x[i] == '\r' && i + 1 < x.Length && x[i + 1] == '\n') i++;
                        res.Add(Value.Str(x.Substring(start, (keep ? i + 1 : end) - start)));
                        start = i + 1;
                    }
                }
                if (start < x.Length) res.Add(Value.Str(x.Substring(start)));
                return Value.Obj(new PyList(res));
            };
            StrM["join"] = (vm, s, a, kw) =>
            {
                Arity("join", a, 1, 1);
                var sb = new StringBuilder();
                bool first = true;
                string sep = S(s);
                foreach (var x in Ops.Iterate(a[0]))
                {
                    if (!first) sb.Append(sep);
                    first = false;
                    if (!(x.O is string xs)) throw Err.Type("sequence item: expected str instance, " + Ops.TypeName(x) + " found");
                    sb.Append(xs);
                }
                return Value.Str(sb.ToString());
            };
            StrM["replace"] = (vm, s, a, kw) =>
            {
                Arity("replace", a, 2, 3);
                string x = S(s), old = ToStr(a[0], "replace() argument 1"), nw = ToStr(a[1], "replace() argument 2");
                long count = a.Length > 2 ? ToLong(a[2], "count") : -1;
                if (count < 0)
                {
                    if (old.Length == 0) return Value.Str(nw + string.Join(nw, x.Select(c => c.ToString())) + (x.Length > 0 ? nw : ""));
                    return Value.Str(x.Replace(old, nw, StringComparison.Ordinal));
                }
                var sb = new StringBuilder();
                int pos = 0;
                for (long n = 0; n < count; n++)
                {
                    int i = old.Length == 0 ? pos : x.IndexOf(old, pos, StringComparison.Ordinal);
                    if (i < 0 || (old.Length == 0 && i > x.Length)) break;
                    sb.Append(x, pos, i - pos).Append(nw);
                    if (old.Length == 0) { if (i < x.Length) sb.Append(x[i]); pos = i + 1; }
                    else pos = i + old.Length;
                }
                if (pos <= x.Length) sb.Append(x, pos, x.Length - pos);
                return Value.Str(sb.ToString());
            };
            StrM["startswith"] = (vm, s, a, kw) => Value.Bool(AffixTest(S(s), a, true));
            StrM["endswith"] = (vm, s, a, kw) => Value.Bool(AffixTest(S(s), a, false));
            StrM["find"] = (vm, s, a, kw) => Value.Int(Find(S(s), a, false));
            StrM["rfind"] = (vm, s, a, kw) => Value.Int(Find(S(s), a, true));
            StrM["index"] = (vm, s, a, kw) => { long i = Find(S(s), a, false); if (i < 0) throw Err.Value("substring not found"); return Value.Int(i); };
            StrM["rindex"] = (vm, s, a, kw) => { long i = Find(S(s), a, true); if (i < 0) throw Err.Value("substring not found"); return Value.Int(i); };
            StrM["count"] = (vm, s, a, kw) =>
            {
                Arity("count", a, 1, 3);
                string x = S(s), sub = ToStr(a[0], "count() argument");
                var (st, en) = Bounds(x.Length, a, 1);
                if (sub.Length == 0) return Value.Int(Math.Max(0, en - st) + 1);
                int n = 0, p = st;
                while (true)
                {
                    int i = x.IndexOf(sub, p, Math.Max(0, en - p), StringComparison.Ordinal);
                    if (i < 0) break;
                    n++;
                    p = i + sub.Length;
                }
                return Value.Int(n);
            };
            StrM["format"] = (vm, s, a, kw) => Value.Str(Format.StrFormat(S(s), a, kw));
            StrM["format_map"] = (vm, s, a, kw) =>
            {
                var d = D(a[0]);
                var names = d.Items().Select(kv => Ops.Str(kv.Key)).ToArray();
                var vals = d.Items().Select(kv => kv.Value).ToArray();
                return Value.Str(Format.StrFormat(S(s), ReadOnlySpan<Value>.Empty, new KwArgs(names, vals)));
            };
            StrM["isdigit"] = (vm, s, a, kw) => Value.Bool(S(s).Length > 0 && S(s).All(char.IsDigit));
            StrM["isdecimal"] = StrM["isdigit"];
            StrM["isnumeric"] = (vm, s, a, kw) => Value.Bool(S(s).Length > 0 && S(s).All(char.IsNumber));
            StrM["isalpha"] = (vm, s, a, kw) => Value.Bool(S(s).Length > 0 && S(s).All(char.IsLetter));
            StrM["isalnum"] = (vm, s, a, kw) => Value.Bool(S(s).Length > 0 && S(s).All(char.IsLetterOrDigit));
            StrM["isspace"] = (vm, s, a, kw) => Value.Bool(S(s).Length > 0 && S(s).All(char.IsWhiteSpace));
            StrM["isupper"] = (vm, s, a, kw) => Value.Bool(S(s).Any(char.IsLetter) && !S(s).Any(char.IsLower));
            StrM["islower"] = (vm, s, a, kw) => Value.Bool(S(s).Any(char.IsLetter) && !S(s).Any(char.IsUpper));
            StrM["isidentifier"] = (vm, s, a, kw) =>
                Value.Bool(S(s).Length > 0 && (char.IsLetter(S(s)[0]) || S(s)[0] == '_') && S(s).All(c => char.IsLetterOrDigit(c) || c == '_'));
            StrM["istitle"] = (vm, s, a, kw) =>
            {
                string x = S(s);
                bool prev = false, any = false;
                foreach (char c in x)
                {
                    if (char.IsUpper(c)) { if (prev) return Value.False; prev = true; any = true; }
                    else if (char.IsLower(c)) { if (!prev) return Value.False; prev = true; }
                    else prev = false;
                }
                return Value.Bool(any);
            };
            StrM["zfill"] = (vm, s, a, kw) =>
            {
                string x = S(s);
                int w = ToInt(a[0], "width");
                if (x.Length >= w) return s;
                int signLen = x.Length > 0 && (x[0] == '-' || x[0] == '+') ? 1 : 0;
                return Value.Str(x.Substring(0, signLen) + new string('0', w - x.Length) + x.Substring(signLen));
            };
            StrM["center"] = (vm, s, a, kw) =>
            {
                string x = S(s);
                int w = ToInt(a[0], "width");
                char fill = a.Length > 1 ? S(a[1])[0] : ' ';
                if (x.Length >= w) return s;
                int total = w - x.Length;
                int left = total / 2 + (total & w & 1);
                return Value.Str(new string(fill, left) + x + new string(fill, total - left));
            };
            StrM["ljust"] = (vm, s, a, kw) => Value.Str(S(s).PadRight(ToInt(a[0], "width"), a.Length > 1 ? S(a[1])[0] : ' '));
            StrM["rjust"] = (vm, s, a, kw) => Value.Str(S(s).PadLeft(ToInt(a[0], "width"), a.Length > 1 ? S(a[1])[0] : ' '));
            StrM["partition"] = (vm, s, a, kw) =>
            {
                string x = S(s), sep = ToStr(a[0], "sep");
                int i = x.IndexOf(sep, StringComparison.Ordinal);
                if (i < 0) return Tuple(s, Value.Str(""), Value.Str(""));
                return Tuple(Value.Str(x.Substring(0, i)), a[0], Value.Str(x.Substring(i + sep.Length)));
            };
            StrM["rpartition"] = (vm, s, a, kw) =>
            {
                string x = S(s), sep = ToStr(a[0], "sep");
                int i = x.LastIndexOf(sep, StringComparison.Ordinal);
                if (i < 0) return Tuple(Value.Str(""), Value.Str(""), s);
                return Tuple(Value.Str(x.Substring(0, i)), a[0], Value.Str(x.Substring(i + sep.Length)));
            };
            StrM["removeprefix"] = (vm, s, a, kw) => Value.Str(S(s).StartsWith(S(a[0]), StringComparison.Ordinal) ? S(s).Substring(S(a[0]).Length) : S(s));
            StrM["removesuffix"] = (vm, s, a, kw) =>
                Value.Str(S(a[0]).Length > 0 && S(s).EndsWith(S(a[0]), StringComparison.Ordinal) ? S(s).Substring(0, S(s).Length - S(a[0]).Length) : S(s));
            StrM["expandtabs"] = (vm, s, a, kw) => Value.Str(S(s).Replace("\t", new string(' ', a.Length > 0 ? ToInt(a[0], "tabsize") : 8)));
            StrM["encode"] = (vm, s, a, kw) => List(Encoding.UTF8.GetBytes(S(s)).Select(b => Value.Int(b)));
            StrM["translate"] = (vm, s, a, kw) =>
            {
                var map = D(a[0]);
                var sb = new StringBuilder();
                foreach (char c in S(s))
                {
                    if (map.TryGet(Value.Int(c), out var rep))
                    {
                        if (rep.IsNone) continue;
                        if (rep.IsInt) sb.Append((char)rep.N); else sb.Append(Ops.Str(rep));
                    }
                    else sb.Append(c);
                }
                return Value.Str(sb.ToString());
            };
            Fn("str.maketrans", "maketrans", (vm, a, kw) =>
            {
                var d = new PyDict();
                if (a.Length == 1) { foreach (var kv in D(a[0]).Items()) d.Set(kv.Key.IsStr ? Value.Int(S(kv.Key)[0]) : kv.Key, kv.Value); }
                else
                {
                    string x = S(a[0]), y = S(a[1]);
                    for (int i = 0; i < x.Length; i++) d.Set(Value.Int(x[i]), Value.Int(y[i]));
                    if (a.Length > 2) foreach (char c in S(a[2])) d.Set(Value.Int(c), Value.None);
                }
                return Value.Obj(d);
            });

            // ---------------------------------------------------- list
            ListM["append"] = (vm, s, a, kw) => { Arity("append", a, 1, 1); L(s).Items.Add(a[0]); return Value.None; };
            ListM["extend"] = (vm, s, a, kw) => { Arity("extend", a, 1, 1); L(s).Items.AddRange(Ops.ToList(a[0])); return Value.None; };
            ListM["insert"] = (vm, s, a, kw) =>
            {
                Arity("insert", a, 2, 2);
                var l = L(s).Items;
                long i = ToLong(a[0], "index");
                if (i < 0) i = Math.Max(0, i + l.Count);
                l.Insert((int)Math.Min(i, l.Count), a[1]);
                return Value.None;
            };
            ListM["pop"] = (vm, s, a, kw) =>
            {
                Arity("pop", a, 0, 1);
                var l = L(s).Items;
                if (l.Count == 0) throw Err.Index("pop from empty list");
                long i = a.Length > 0 ? ToLong(a[0], "index") : l.Count - 1;
                if (i < 0) i += l.Count;
                if (i < 0 || i >= l.Count) throw Err.Index("pop index out of range");
                var v = l[(int)i];
                l.RemoveAt((int)i);
                return v;
            };
            ListM["remove"] = (vm, s, a, kw) =>
            {
                Arity("remove", a, 1, 1);
                var l = L(s).Items;
                for (int i = 0; i < l.Count; i++)
                    if (Ops.EqValues(l[i], a[0])) { l.RemoveAt(i); return Value.None; }
                throw Err.Value("list.remove(x): x not in list");
            };
            ListM["index"] = (vm, s, a, kw) =>
            {
                Arity("index", a, 1, 3);
                var l = L(s).Items;
                var (st, en) = Bounds(l.Count, a, 1);
                for (int i = st; i < en; i++) if (Ops.EqValues(l[i], a[0])) return Value.Int(i);
                throw Err.Value(Ops.Repr(a[0]) + " is not in list");
            };
            ListM["count"] = (vm, s, a, kw) => { Arity("count", a, 1, 1); var x0 = a[0]; return Value.Int(L(s).Items.Count(x => Ops.EqValues(x, x0))); };
            ListM["sort"] = (vm, s, a, kw) =>
            {
                if (a.Length > 0) throw Err.Type("sort() takes no positional arguments");
                kw?.CheckOnly("sort", "key", "reverse");
                SortList(vm, L(s).Items, kw?.Get("key", Value.None) ?? Value.None, kw != null && Ops.Truthy(kw.Get("reverse", Value.False)));
                return Value.None;
            };
            ListM["reverse"] = (vm, s, a, kw) => { L(s).Items.Reverse(); return Value.None; };
            ListM["clear"] = (vm, s, a, kw) => { L(s).Items.Clear(); return Value.None; };
            ListM["copy"] = (vm, s, a, kw) => Value.Obj(new PyList(new List<Value>(L(s).Items)));

            // ---------------------------------------------------- tuple
            TupleM["index"] = (vm, s, a, kw) =>
            {
                var t = ((PyTuple)s.O).Items;
                for (int i = 0; i < t.Length; i++) if (Ops.EqValues(t[i], a[0])) return Value.Int(i);
                throw Err.Value("tuple.index(x): x not in tuple");
            };
            TupleM["count"] = (vm, s, a, kw) => { var x0 = a[0]; return Value.Int(((PyTuple)s.O).Items.Count(x => Ops.EqValues(x, x0))); };

            // ---------------------------------------------------- dict
            DictM["get"] = (vm, s, a, kw) =>
            {
                Arity("get", a, 1, 2);
                return D(s).TryGet(a[0], out var v) ? v : a.Length > 1 ? a[1] : Value.None;
            };
            DictM["keys"] = (vm, s, a, kw) => Value.Obj(new DictView(D(s), 0));
            DictM["values"] = (vm, s, a, kw) => Value.Obj(new DictView(D(s), 1));
            DictM["items"] = (vm, s, a, kw) => Value.Obj(new DictView(D(s), 2));
            DictM["pop"] = (vm, s, a, kw) =>
            {
                Arity("pop", a, 1, 2);
                if (D(s).Remove(a[0], out var v)) return v;
                if (a.Length > 1) return a[1];
                throw Err.Key(a[0]);
            };
            DictM["popitem"] = (vm, s, a, kw) =>
            {
                var d = D(s);
                for (int i = d.Used - 1; i >= 0; i--)
                {
                    if (d.Keys[i].IsUnbound) continue;
                    var k = d.Keys[i];
                    d.Remove(k, out var v);
                    return Tuple(k, v);
                }
                throw Err.Key(Value.Str("popitem(): dictionary is empty"));
            };
            DictM["setdefault"] = (vm, s, a, kw) =>
            {
                Arity("setdefault", a, 1, 2);
                var d = D(s);
                if (d.TryGet(a[0], out var v)) return v;
                var dv = a.Length > 1 ? a[1] : Value.None;
                d.Set(a[0], dv);
                return dv;
            };
            DictM["update"] = (vm, s, a, kw) =>
            {
                var d = D(s);
                if (a.Length > 0) DictUpdate(d, a[0]);
                if (kw != null) for (int i = 0; i < kw.Count; i++) d.Set(Value.Str(kw.Names[i]), kw.Values[i]);
                return Value.None;
            };
            DictM["clear"] = (vm, s, a, kw) => { D(s).Clear(); return Value.None; };
            DictM["copy"] = (vm, s, a, kw) => Value.Obj(D(s).Copy());
            Fn("dict.fromkeys", "fromkeys", (vm, a, kw) =>
            {
                var d = new PyDict();
                var v = a.Length > 1 ? a[1] : Value.None;
                foreach (var k in Ops.Iterate(a[0])) d.Set(k, v);
                return Value.Obj(d);
            });

            // ---------------------------------------------------- set
            SetM["add"] = (vm, s, a, kw) => { Mut(s); St(s).Items.Add(a[0]); return Value.None; };
            SetM["remove"] = (vm, s, a, kw) => { Mut(s); if (!St(s).Items.Remove(a[0])) throw Err.Key(a[0]); return Value.None; };
            SetM["discard"] = (vm, s, a, kw) => { Mut(s); St(s).Items.Remove(a[0]); return Value.None; };
            SetM["pop"] = (vm, s, a, kw) =>
            {
                Mut(s);
                var set = St(s).Items;
                if (set.Count == 0) throw Err.Key(Value.Str("pop from an empty set"));
                var v = set.First();
                set.Remove(v);
                return v;
            };
            SetM["clear"] = (vm, s, a, kw) => { Mut(s); St(s).Items.Clear(); return Value.None; };
            SetM["copy"] = (vm, s, a, kw) => Value.Obj(new PySet(St(s).Items) { Frozen = St(s).Frozen });
            SetM["union"] = (vm, s, a, kw) => { var r = new PySet(St(s).Items) { Frozen = St(s).Frozen }; foreach (var x in a) r.Items.UnionWith(Ops.Iterate(x)); return Value.Obj(r); };
            SetM["intersection"] = (vm, s, a, kw) => { var r = new PySet(St(s).Items) { Frozen = St(s).Frozen }; foreach (var x in a) r.Items.IntersectWith(Ops.Iterate(x)); return Value.Obj(r); };
            SetM["difference"] = (vm, s, a, kw) => { var r = new PySet(St(s).Items) { Frozen = St(s).Frozen }; foreach (var x in a) r.Items.ExceptWith(Ops.Iterate(x)); return Value.Obj(r); };
            SetM["symmetric_difference"] = (vm, s, a, kw) => { var r = new PySet(St(s).Items) { Frozen = St(s).Frozen }; r.Items.SymmetricExceptWith(Ops.Iterate(a[0])); return Value.Obj(r); };
            SetM["update"] = (vm, s, a, kw) => { Mut(s); foreach (var x in a) St(s).Items.UnionWith(Ops.Iterate(x)); return Value.None; };
            SetM["intersection_update"] = (vm, s, a, kw) => { Mut(s); foreach (var x in a) St(s).Items.IntersectWith(Ops.Iterate(x)); return Value.None; };
            SetM["difference_update"] = (vm, s, a, kw) => { Mut(s); foreach (var x in a) St(s).Items.ExceptWith(Ops.Iterate(x)); return Value.None; };
            SetM["issubset"] = (vm, s, a, kw) => Value.Bool(St(s).Items.IsSubsetOf(Ops.Iterate(a[0])));
            SetM["issuperset"] = (vm, s, a, kw) => Value.Bool(St(s).Items.IsSupersetOf(Ops.Iterate(a[0])));
            SetM["isdisjoint"] = (vm, s, a, kw) => Value.Bool(!St(s).Items.Overlaps(Ops.Iterate(a[0])));

            // ---------------------------------------------------- numbers
            IntM["bit_length"] = (vm, s, a, kw) =>
            {
                var b = BigInteger.Abs(s.AsBig);
                return Value.Int(b.IsZero ? 0 : (long)b.GetBitLength());
            };
            IntM["conjugate"] = (vm, s, a, kw) => s;
            IntM["is_integer"] = (vm, s, a, kw) => Value.True;
            FloatM["is_integer"] = (vm, s, a, kw) => Value.Bool(s.AsFloat == Math.Floor(s.AsFloat) && !double.IsInfinity(s.AsFloat));
            FloatM["conjugate"] = (vm, s, a, kw) => s;
            FloatM["as_integer_ratio"] = (vm, s, a, kw) =>
            {
                double d = s.AsFloat;
                if (double.IsInfinity(d) || double.IsNaN(d)) throw Err.Value("cannot convert to integer ratio");
                BigInteger den = 1;
                while (d != Math.Floor(d)) { d *= 2; den *= 2; }
                var num = new BigInteger(d);
                var g = BigInteger.GreatestCommonDivisor(num, den);
                return Tuple(Value.BigInt(num / g), Value.BigInt(den / g));
            };

            // ---------------------------------------------------- generators / iterators / range
            GenM["send"] = (vm, s, a, kw) =>
            {
                var g = (PyGenerator)s.O;
                if (vm.GenStepNested(g, a.Length > 0 ? a[0] : Value.None, out var v)) return v;
                throw Err.StopIter(Value.None);
            };
            GenM["__next__"] = (vm, s, a, kw) =>
            {
                var g = (PyGenerator)s.O;
                if (vm.GenStepNested(g, Value.None, out var v)) return v;
                throw Err.StopIter(Value.None);
            };
            GenM["close"] = (vm, s, a, kw) => { ((PyGenerator)s.O).Done = true; return Value.None; };
            IterM["__next__"] = (vm, s, a, kw) =>
            {
                if (((PyIterator)s.O).Next(vm, out var v)) return v;
                throw Err.StopIter(Value.None);
            };
            RangeM["index"] = (vm, s, a, kw) =>
            {
                var r = (PyRange)s.O;
                if ((a[0].IsInt || a[0].IsBool) && r.Contains(a[0].N)) return Value.Int((a[0].N - r.Start) / r.Step);
                throw Err.Value(Ops.Repr(a[0]) + " is not in range");
            };
            RangeM["count"] = (vm, s, a, kw) => Value.Int((a[0].IsInt || a[0].IsBool) && ((PyRange)s.O).Contains(a[0].N) ? 1 : 0);
        }

        static void Mut(Value s)
        {
            if (St(s).Frozen) throw new ScriptException(AttributeError, "'frozenset' object does not support mutation");
        }

        static (int, int) Bounds(int len, ReadOnlySpan<Value> a, int from)
        {
            long st = a.Length > from && !a[from].IsNone ? ToLong(a[from], "start") : 0;
            long en = a.Length > from + 1 && !a[from + 1].IsNone ? ToLong(a[from + 1], "end") : len;
            if (st < 0) st = Math.Max(0, st + len);
            if (en < 0) en = Math.Max(0, en + len);
            return ((int)Math.Min(st, len), (int)Math.Min(en, len));
        }

        static long Find(string x, ReadOnlySpan<Value> a, bool rev)
        {
            Arity(rev ? "rfind" : "find", a, 1, 3);
            string sub = ToStr(a[0], "find() argument");
            var (st, en) = Bounds(x.Length, a, 1);
            if (en < st) return -1;
            if (rev)
            {
                if (sub.Length == 0) return en;
                int i = x.LastIndexOf(sub, en - 1 < 0 ? 0 : en - 1, en - st, StringComparison.Ordinal);
                if (en - st == 0) return -1;
                return i >= st && i + sub.Length <= en ? i : -1;
            }
            int j = x.IndexOf(sub, st, en - st, StringComparison.Ordinal);
            return j;
        }

        static bool AffixTest(string x, ReadOnlySpan<Value> a, bool prefix)
        {
            Arity(prefix ? "startswith" : "endswith", a, 1, 3);
            var (st, en) = Bounds(x.Length, a, 1);
            if (en < st) return false;
            string seg = x.Substring(st, en - st);
            if (a[0].O is PyTuple t)
            {
                foreach (var p in t.Items)
                    if (prefix ? seg.StartsWith(ToStr(p, "prefix"), StringComparison.Ordinal) : seg.EndsWith(ToStr(p, "suffix"), StringComparison.Ordinal)) return true;
                return false;
            }
            string s = ToStr(a[0], prefix ? "startswith arg" : "endswith arg");
            return prefix ? seg.StartsWith(s, StringComparison.Ordinal) : seg.EndsWith(s, StringComparison.Ordinal);
        }

        static Value Split(string x, Value sep, int max, bool fromRight)
        {
            var res = new List<Value>();
            if (sep.IsNone)
            {
                var parts = new List<string>();
                int i = 0, n = x.Length;
                if (!fromRight)
                {
                    while (i < n)
                    {
                        while (i < n && char.IsWhiteSpace(x[i])) i++;
                        if (i >= n) break;
                        if (max >= 0 && parts.Count == max) { parts.Add(x.Substring(i).TrimEnd()); break; }
                        int st = i;
                        while (i < n && !char.IsWhiteSpace(x[i])) i++;
                        parts.Add(x.Substring(st, i - st));
                    }
                }
                else
                {
                    i = n - 1;
                    while (i >= 0)
                    {
                        while (i >= 0 && char.IsWhiteSpace(x[i])) i--;
                        if (i < 0) break;
                        if (max >= 0 && parts.Count == max) { parts.Add(x.Substring(0, i + 1).TrimStart()); break; }
                        int en = i;
                        while (i >= 0 && !char.IsWhiteSpace(x[i])) i--;
                        parts.Add(x.Substring(i + 1, en - i));
                    }
                    parts.Reverse();
                }
                foreach (var p in parts) res.Add(Value.Str(p));
                return Value.Obj(new PyList(res));
            }
            string sp = ToStr(sep, "sep");
            if (sp.Length == 0) throw Err.Value("empty separator");
            if (!fromRight)
            {
                int pos = 0, count = 0;
                while (max < 0 || count < max)
                {
                    int i = x.IndexOf(sp, pos, StringComparison.Ordinal);
                    if (i < 0) break;
                    res.Add(Value.Str(x.Substring(pos, i - pos)));
                    pos = i + sp.Length;
                    count++;
                }
                res.Add(Value.Str(x.Substring(pos)));
            }
            else
            {
                int end = x.Length, count = 0;
                while (max < 0 || count < max)
                {
                    int i = end - sp.Length < 0 ? -1 : x.LastIndexOf(sp, end - 1, end, StringComparison.Ordinal);
                    if (i < 0) break;
                    res.Add(Value.Str(x.Substring(i + sp.Length, end - i - sp.Length)));
                    end = i;
                    count++;
                }
                res.Add(Value.Str(x.Substring(0, end)));
                res.Reverse();
            }
            return Value.Obj(new PyList(res));
        }

        // ------------------------------------------------------------ modules
        static PyModule Mod(string name)
        {
            var m = new PyModule(name);
            Modules[name] = m;
            return m;
        }

        static void MFn(PyModule m, string name, BuiltinFn fn) => m.Dict[name] = Value.Obj(Fn(m.Name + "." + name, name, fn));

        static void M1(PyModule m, string name, Func<double, double> f) =>
            MFn(m, name, (vm, a, kw) => { Arity(name, a, 1, 1); return Value.Float(f(a[0].ToDouble())); });

        static Value FloatToInt(double d)
        {
            if (double.IsNaN(d)) throw Err.Value("cannot convert float NaN to integer");
            if (double.IsInfinity(d)) throw Err.Overflow("cannot convert float infinity to integer");
            return Math.Abs(d) < 9e18 ? Value.Int((long)d) : Value.BigInt(new BigInteger(d));
        }

        static void InitModules()
        {
            var math = Mod("math");
            math.Dict["pi"] = Value.Float(Math.PI);
            math.Dict["e"] = Value.Float(Math.E);
            math.Dict["tau"] = Value.Float(2 * Math.PI);
            math.Dict["inf"] = Value.Float(double.PositiveInfinity);
            math.Dict["nan"] = Value.Float(double.NaN);
            MFn(math, "sqrt", (vm, a, kw) =>
            {
                double d = a[0].ToDouble();
                if (d < 0) throw Err.Value("math domain error");
                return Value.Float(Math.Sqrt(d));
            });
            MFn(math, "floor", (vm, a, kw) => a[0].IsIntegral ? Value.BigInt(a[0].AsBig) : FloatToInt(Math.Floor(a[0].ToDouble())));
            MFn(math, "ceil", (vm, a, kw) => a[0].IsIntegral ? Value.BigInt(a[0].AsBig) : FloatToInt(Math.Ceiling(a[0].ToDouble())));
            MFn(math, "trunc", (vm, a, kw) => a[0].IsIntegral ? Value.BigInt(a[0].AsBig) : FloatToInt(Math.Truncate(a[0].ToDouble())));
            M1(math, "fabs", Math.Abs);
            M1(math, "exp", Math.Exp);
            MFn(math, "log", (vm, a, kw) =>
            {
                Arity("log", a, 1, 2);
                double x = a[0].IsBigInt ? BigInteger.Log(a[0].AsBig) : a[0].ToDouble();
                if (x <= 0 && !a[0].IsBigInt) throw Err.Value("math domain error");
                double r = a[0].IsBigInt ? x : Math.Log(x);
                return Value.Float(a.Length > 1 ? r / Math.Log(a[1].ToDouble()) : r);
            });
            M1(math, "log2", Math.Log2);
            M1(math, "log10", Math.Log10);
            M1(math, "log1p", x => Math.Log(1 + x));
            M1(math, "expm1", x => Math.Exp(x) - 1);
            M1(math, "sin", Math.Sin); M1(math, "cos", Math.Cos); M1(math, "tan", Math.Tan);
            M1(math, "asin", Math.Asin); M1(math, "acos", Math.Acos); M1(math, "atan", Math.Atan);
            M1(math, "sinh", Math.Sinh); M1(math, "cosh", Math.Cosh); M1(math, "tanh", Math.Tanh);
            M1(math, "degrees", x => x * 180.0 / Math.PI);
            M1(math, "radians", x => x * Math.PI / 180.0);
            MFn(math, "atan2", (vm, a, kw) => Value.Float(Math.Atan2(a[0].ToDouble(), a[1].ToDouble())));
            MFn(math, "pow", (vm, a, kw) => Value.Float(Math.Pow(a[0].ToDouble(), a[1].ToDouble())));
            MFn(math, "fmod", (vm, a, kw) => Value.Float(a[0].ToDouble() % a[1].ToDouble()));
            MFn(math, "copysign", (vm, a, kw) => Value.Float(Math.CopySign(a[0].ToDouble(), a[1].ToDouble())));
            MFn(math, "hypot", (vm, a, kw) => { double s = 0; foreach (var x in a) { double d = x.ToDouble(); s += d * d; } return Value.Float(Math.Sqrt(s)); });
            MFn(math, "isnan", (vm, a, kw) => Value.Bool(double.IsNaN(a[0].ToDouble())));
            MFn(math, "isinf", (vm, a, kw) => Value.Bool(double.IsInfinity(a[0].ToDouble())));
            MFn(math, "isfinite", (vm, a, kw) => Value.Bool(double.IsFinite(a[0].ToDouble())));
            MFn(math, "isclose", (vm, a, kw) =>
            {
                double x = a[0].ToDouble(), y = a[1].ToDouble();
                double rel = kw != null ? kw.Get("rel_tol", Value.Float(1e-9)).ToDouble() : 1e-9;
                double abs = kw != null ? kw.Get("abs_tol", Value.Float(0)).ToDouble() : 0;
                return Value.Bool(x == y || Math.Abs(x - y) <= Math.Max(rel * Math.Max(Math.Abs(x), Math.Abs(y)), abs));
            });
            MFn(math, "gcd", (vm, a, kw) =>
            {
                BigInteger g = 0;
                foreach (var x in a) g = BigInteger.GreatestCommonDivisor(g, x.AsBig);
                return Value.BigInt(g);
            });
            MFn(math, "lcm", (vm, a, kw) =>
            {
                BigInteger l = 1;
                foreach (var x in a)
                {
                    var v = BigInteger.Abs(x.AsBig);
                    if (v.IsZero) return Value.Int(0);
                    l = l / BigInteger.GreatestCommonDivisor(l, v) * v;
                }
                return Value.BigInt(l);
            });
            MFn(math, "factorial", (vm, a, kw) =>
            {
                long n = ToLong(a[0], "factorial() argument");
                if (n < 0) throw Err.Value("factorial() not defined for negative values");
                BigInteger r = 1;
                for (long i = 2; i <= n; i++) r *= i;
                return Value.BigInt(r);
            });
            MFn(math, "comb", (vm, a, kw) =>
            {
                long n = ToLong(a[0], "n"), k = ToLong(a[1], "k");
                if (k < 0 || n < 0) throw Err.Value("must be non-negative integer");
                if (k > n) return Value.Int(0);
                k = Math.Min(k, n - k);
                BigInteger r = 1;
                for (long i = 1; i <= k; i++) r = r * (n - k + i) / i;
                return Value.BigInt(r);
            });
            MFn(math, "perm", (vm, a, kw) =>
            {
                long n = ToLong(a[0], "n"), k = a.Length > 1 ? ToLong(a[1], "k") : n;
                if (k > n) return Value.Int(0);
                BigInteger r = 1;
                for (long i = n - k + 1; i <= n; i++) r *= i;
                return Value.BigInt(r);
            });
            MFn(math, "isqrt", (vm, a, kw) =>
            {
                var n = a[0].AsBig;
                if (n.Sign < 0) throw Err.Value("isqrt() argument must be nonnegative");
                if (n.IsZero) return Value.Int(0);
                var x = (BigInteger)Math.Sqrt((double)n);
                while (x * x > n) x--;
                while ((x + 1) * (x + 1) <= n) x++;
                return Value.BigInt(x);
            });
            MFn(math, "prod", (vm, a, kw) =>
            {
                Value acc = kw?.Get("start", Value.Int(1)) ?? Value.Int(1);
                foreach (var x in Ops.Iterate(a[0])) acc = Ops.Mul(acc, x);
                return acc;
            });
            MFn(math, "fsum", (vm, a, kw) =>
            {
                double s = 0, c = 0;
                foreach (var x in Ops.Iterate(a[0]))
                {
                    double y = x.ToDouble() - c;
                    double t = s + y;
                    c = (t - s) - y;
                    s = t;
                }
                return Value.Float(s);
            });
            MFn(math, "modf", (vm, a, kw) =>
            {
                double d = a[0].ToDouble();
                double ip = Math.Truncate(d);
                return Tuple(Value.Float(d - ip), Value.Float(ip));
            });

            var time = Mod("time");
            MFn(time, "time", (vm, a, kw) => Value.Float((DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds));
            MFn(time, "perf_counter", (vm, a, kw) => Value.Float(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency));
            MFn(time, "monotonic", (vm, a, kw) => Value.Float(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency));
            MFn(time, "sleep", (vm, a, kw) => { System.Threading.Thread.Sleep(TimeSpan.FromSeconds(a[0].ToDouble())); return Value.None; });

            var random = Mod("random");
            MFn(random, "seed", (vm, a, kw) => { vm.Random.Seed(a.Length > 0 && !a[0].IsNone ? (long)ValueEq.Instance.GetHashCode(a[0]) ^ (a[0].IsInt ? a[0].N : 0) : Environment.TickCount); return Value.None; });
            MFn(random, "random", (vm, a, kw) => Value.Float(vm.Random.NextDouble()));
            MFn(random, "uniform", (vm, a, kw) => { double lo = a[0].ToDouble(), hi = a[1].ToDouble(); return Value.Float(lo + (hi - lo) * vm.Random.NextDouble()); });
            MFn(random, "randint", (vm, a, kw) => Value.Int(vm.Random.NextRange(ToLong(a[0], "a"), ToLong(a[1], "b") + 1)));
            MFn(random, "randrange", (vm, a, kw) =>
            {
                long start = 0, stop, step = 1;
                if (a.Length == 1) stop = ToLong(a[0], "stop");
                else { start = ToLong(a[0], "start"); stop = ToLong(a[1], "stop"); if (a.Length > 2) step = ToLong(a[2], "step"); }
                long n = new PyRange(start, stop, step).Length;
                return Value.Int(start + step * vm.Random.NextRange(0, n));
            });
            MFn(random, "choice", (vm, a, kw) =>
            {
                long n = Ops.Len(a[0]);
                if (n == 0) throw Err.Index("Cannot choose from an empty sequence");
                return Ops.GetItem(a[0], Value.Int(vm.Random.NextRange(0, n)));
            });
            MFn(random, "shuffle", (vm, a, kw) =>
            {
                var l = L(a[0]).Items;
                for (int i = l.Count - 1; i > 0; i--)
                {
                    int j = (int)vm.Random.NextRange(0, i + 1);
                    (l[i], l[j]) = (l[j], l[i]);
                }
                return Value.None;
            });
            MFn(random, "sample", (vm, a, kw) =>
            {
                var pool = Ops.ToList(a[0]);
                int k = ToInt(a[1], "k");
                if (k > pool.Count) throw Err.Value("Sample larger than population or is negative");
                var res = new List<Value>();
                for (int i = 0; i < k; i++)
                {
                    int j = (int)vm.Random.NextRange(i, pool.Count);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                    res.Add(pool[i]);
                }
                return Value.Obj(new PyList(res));
            });
        }
    }
}
