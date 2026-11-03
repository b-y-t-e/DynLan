using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DynLan2
{
    internal static partial class Builtins
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static readonly object NotImplementedObj = new Tag("NotImplemented");

        // ------------------------------------------------------------ classes
        internal static readonly PyClass ObjectClass;
        internal static readonly PyClass BaseException, Exception, ArithmeticError, ZeroDivisionError, OverflowError, LookupError,
            KeyError, IndexError, TypeError, ValueError, NameError, UnboundLocalError, AttributeError, RuntimeError,
            RecursionError, NotImplementedError, StopIteration, AssertionError, ImportError, ModuleNotFoundError,
            MemoryError, SyntaxError, GeneratorExit, OSError, KeyboardInterrupt;

        internal static readonly Dictionary<string, Value> Dict = new Dictionary<string, Value>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, BuiltinFunction> Registry = new Dictionary<string, BuiltinFunction>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, PyClass> ClassRegistry = new Dictionary<string, PyClass>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, PyType> TypeRegistry = new Dictionary<string, PyType>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, PyModule> Modules = new Dictionary<string, PyModule>(StringComparer.Ordinal);

        internal static PyType IntType, FloatType, StrType, BoolType, ListType, TupleType, DictType, SetType, FrozenSetType,
            RangeType, TypeType, SliceType, NoneType, FunctionType;

        static readonly string[] CharCache = new string[256];

        static Builtins()
        {
            for (int i = 0; i < CharCache.Length; i++) CharCache[i] = ((char)i).ToString();

            ObjectClass = new PyClass("object", Array.Empty<PyClass>()) { IsBuiltin = true };
            ClassRegistry["object"] = ObjectClass;
            BaseException = Cls("BaseException", ObjectClass);
            BaseException.IsException = true;
            Exception = Cls("Exception", BaseException);
            ArithmeticError = Cls("ArithmeticError", Exception);
            ZeroDivisionError = Cls("ZeroDivisionError", ArithmeticError);
            OverflowError = Cls("OverflowError", ArithmeticError);
            LookupError = Cls("LookupError", Exception);
            KeyError = Cls("KeyError", LookupError);
            IndexError = Cls("IndexError", LookupError);
            TypeError = Cls("TypeError", Exception);
            ValueError = Cls("ValueError", Exception);
            NameError = Cls("NameError", Exception);
            UnboundLocalError = Cls("UnboundLocalError", NameError);
            AttributeError = Cls("AttributeError", Exception);
            RuntimeError = Cls("RuntimeError", Exception);
            RecursionError = Cls("RecursionError", RuntimeError);
            NotImplementedError = Cls("NotImplementedError", RuntimeError);
            StopIteration = Cls("StopIteration", Exception);
            AssertionError = Cls("AssertionError", Exception);
            ImportError = Cls("ImportError", Exception);
            ModuleNotFoundError = Cls("ModuleNotFoundError", ImportError);
            MemoryError = Cls("MemoryError", Exception);
            SyntaxError = Cls("SyntaxError", Exception);
            OSError = Cls("OSError", Exception);
            GeneratorExit = Cls("GeneratorExit", BaseException);
            KeyboardInterrupt = Cls("KeyboardInterrupt", BaseException);

            ObjectClass.Dict["__init__"] = Value.Obj(Fn("object.__init__", "__init__", (vm, a, kw) =>
            {
                if (a.Length > 1) throw Err.Type(Ops.TypeName(a[0]) + "() takes no arguments");
                return Value.None;
            }));
            BaseException.Dict["__init__"] = Value.Obj(Fn("BaseException.__init__", "__init__", (vm, a, kw) =>
            {
                ((PyInstance)a[0].O).Attrs["args"] = Value.Obj(new PyTuple(a.Slice(1).ToArray()));
                return Value.None;
            }));
            BaseException.Dict["__str__"] = Value.Obj(Fn("BaseException.__str__", "__str__", (vm, a, kw) =>
            {
                var inst = (PyInstance)a[0].O;
                var args = inst.Attrs.TryGetValue("args", out var av) && av.O is PyTuple t ? t.Items : Array.Empty<Value>();
                if (args.Length == 0) return Value.Str("");
                if (args.Length == 1) return Value.Str(inst.Class.IsSubclassOf(KeyError) ? Ops.Repr(args[0]) : Ops.Str(args[0]));
                return Value.Str(Ops.Repr(Value.Obj(new PyTuple(args))));
            }));
            BaseException.Dict["__repr__"] = Value.Obj(Fn("BaseException.__repr__", "__repr__", (vm, a, kw) =>
            {
                var inst = (PyInstance)a[0].O;
                var args = inst.Attrs.TryGetValue("args", out var av) && av.O is PyTuple t ? t.Items : Array.Empty<Value>();
                return Value.Str(inst.Class.Name + "(" + string.Join(", ", args.Select(Ops.Repr)) + ")");
            }));

            foreach (var kv in ClassRegistry) Dict[kv.Key] = Value.Obj(kv.Value);
            Dict["NotImplemented"] = new Value(NotImplementedObj, 0);
            Dict["Ellipsis"] = Value.None;

            InitTypes();
            InitFunctions();
            InitMethods();
            InitModules();
        }

        static PyClass Cls(string name, PyClass b)
        {
            var c = new PyClass(name, new[] { b }) { IsBuiltin = true };
            ClassRegistry[name] = c;
            return c;
        }

        internal static BuiltinFunction Fn(string key, string name, BuiltinFn fn)
        {
            var f = new BuiltinFunction(key, name, fn);
            Registry[key] = f;
            return f;
        }

        static void Def(string name, BuiltinFn fn) => Dict[name] = Value.Obj(Fn(name, name, fn));

        internal static Value Lookup(string name) => Dict.TryGetValue(name, out var v) ? v : default;

        internal static string CharStr(char c) => c < 256 ? CharCache[c] : c.ToString();

        internal static PyInstance NewInstance(PyClass cls)
        {
            var inst = new PyInstance(cls);
            if (cls.IsException) inst.Attrs["args"] = Value.Obj(PyTuple.Empty);
            return inst;
        }

        internal static PyInstance NewException(PyClass cls, string message)
        {
            var inst = new PyInstance(cls);
            inst.Attrs["args"] = Value.Obj(new PyTuple(message == null ? Array.Empty<Value>() : new[] { Value.Str(message) }));
            return inst;
        }

        internal static PyInstance NewExceptionArgs(PyClass cls, params Value[] args)
        {
            var inst = new PyInstance(cls);
            inst.Attrs["args"] = Value.Obj(new PyTuple(args));
            return inst;
        }

        internal static Value AttrsDict(Dictionary<string, Value> attrs)
        {
            var d = new PyDict(attrs.Count);
            foreach (var kv in attrs) d.Set(Value.Str(kv.Key), kv.Value);
            return Value.Obj(d);
        }

        internal static Value IndexOf(Value v)
        {
            if (v.IsInt || v.IsBool) return Value.Int(v.N);
            if (v.O is PyInstance inst && inst.Class.Lookup("__index__", out _)) return VM.Current.CallMethod(v, "__index__", ReadOnlySpan<Value>.Empty);
            throw Err.Type("'" + Ops.TypeName(v) + "' object cannot be interpreted as an integer");
        }

        // ------------------------------------------------------------ argument helpers
        static void Arity(string fn, ReadOnlySpan<Value> a, int min, int max)
        {
            if (a.Length < min || a.Length > max)
            {
                if (min == max) throw Err.Type(fn + "() takes exactly " + min + " argument" + (min == 1 ? "" : "s") + " (" + a.Length + " given)");
                if (a.Length < min) throw Err.Type(fn + "() expected at least " + min + " argument" + (min == 1 ? "" : "s") + ", got " + a.Length);
                throw Err.Type(fn + "() expected at most " + max + " argument" + (max == 1 ? "" : "s") + ", got " + a.Length);
            }
        }

        static long ToLong(Value v, string what)
        {
            if (v.IsInt || v.IsBool) return v.N;
            if (v.O is BigInteger b)
            {
                if (b > long.MaxValue || b < long.MinValue) throw Err.Overflow("Python int too large to convert");
                return (long)b;
            }
            if (v.O is PyInstance) return IndexOf(v).N;
            throw Err.Type(what + " must be an integer, not '" + Ops.TypeName(v) + "'");
        }

        static int ToInt(Value v, string what)
        {
            long l = ToLong(v, what);
            if (l > int.MaxValue || l < int.MinValue) throw Err.Overflow(what + " too large");
            return (int)l;
        }

        static string ToStr(Value v, string what)
        {
            if (v.O is string s) return s;
            throw Err.Type(what + " must be str, not " + Ops.TypeName(v));
        }

        internal static Value List(IEnumerable<Value> items) => Value.Obj(new PyList(new List<Value>(items)));
        internal static Value Tuple(params Value[] items) => Value.Obj(new PyTuple(items));

        // ------------------------------------------------------------ types
        static PyType Type(string name, BuiltinFn ctor, Func<Value, bool> check)
        {
            var t = new PyType(name, ctor, check);
            TypeRegistry[name] = t;
            Dict[name] = Value.Obj(t);
            return t;
        }

        internal static Value TypeOf(Value v)
        {
            switch (v.O)
            {
                case PyInstance i: return Value.Obj(i.Class);
                case PyClass _: case PyType _: return Value.Obj(TypeType);
                case string _: return Value.Obj(StrType);
                case BigInteger _: return Value.Obj(IntType);
                case PyList _: return Value.Obj(ListType);
                case PyTuple _: return Value.Obj(TupleType);
                case PyDict _: return Value.Obj(DictType);
                case PySet s: return Value.Obj(s.Frozen ? FrozenSetType : SetType);
                case PyRange _: return Value.Obj(RangeType);
                case PySlice _: return Value.Obj(SliceType);
                case PyFunction _: return Value.Obj(FunctionType);
            }
            if (v.IsInt) return Value.Obj(IntType);
            if (v.IsFloat) return Value.Obj(FloatType);
            if (v.IsBool) return Value.Obj(BoolType);
            if (v.IsNone) return Value.Obj(NoneType);
            string name = Ops.TypeName(v);
            lock (TypeRegistry)
            {
                if (!TypeRegistry.TryGetValue(name, out var t))
                {
                    t = new PyType(name, (vm, a, kw) => throw Err.Type("cannot create '" + name + "' instances"), x => Ops.TypeName(x) == name);
                    TypeRegistry[name] = t;
                }
                return Value.Obj(t);
            }
        }

        static void InitTypes()
        {
            IntType = Type("int", (vm, a, kw) =>
            {
                if (kw != null) { kw.CheckOnly("int", "base"); }
                if (a.Length == 0 && kw == null) return Value.Int(0);
                Arity("int", a, 1, 2);
                var baseV = a.Length > 1 ? a[1] : kw?.Get("base", default) ?? default;
                var x = a[0];
                if (!baseV.IsUnbound)
                {
                    if (!(x.O is string bs)) throw Err.Type("int() can't convert non-string with explicit base");
                    return ParseInt(bs, ToInt(baseV, "base"));
                }
                if (x.IsInt) return x;
                if (x.IsBool) return Value.Int(x.N);
                if (x.O is BigInteger) return x;
                if (x.IsFloat)
                {
                    double d = x.AsFloat;
                    if (double.IsNaN(d)) throw Err.Value("cannot convert float NaN to integer");
                    if (double.IsInfinity(d)) throw Err.Overflow("cannot convert float infinity to integer");
                    d = Math.Truncate(d);
                    if (d >= -9.2e18 && d <= 9.2e18) return Value.Int((long)d);
                    return Value.BigInt(new BigInteger(d));
                }
                if (x.O is string s) return ParseInt(s, 10);
                if (x.O is PyInstance inst)
                {
                    if (inst.Class.Lookup("__int__", out _)) return vm.CallMethod(x, "__int__", ReadOnlySpan<Value>.Empty);
                    if (inst.Class.Lookup("__index__", out _)) return vm.CallMethod(x, "__index__", ReadOnlySpan<Value>.Empty);
                }
                if (Interop.IsHost(x)) return Interop.ToValue(Convert.ToInt64(x.O, Inv));
                throw Err.Type("int() argument must be a string or a number, not '" + Ops.TypeName(x) + "'");
            }, v => v.IsInt || v.IsBool || v.O is BigInteger);

            FloatType = Type("float", (vm, a, kw) =>
            {
                if (a.Length == 0) return Value.Float(0);
                Arity("float", a, 1, 1);
                var x = a[0];
                if (x.IsFloat) return x;
                if (x.IsNumber) return Value.Float(x.ToDouble());
                if (x.O is string s) return Value.Float(ParseFloat(s));
                if (x.O is PyInstance inst && inst.Class.Lookup("__float__", out _)) return vm.CallMethod(x, "__float__", ReadOnlySpan<Value>.Empty);
                if (Interop.IsHost(x)) return Value.Float(Convert.ToDouble(x.O, Inv));
                throw Err.Type("float() argument must be a string or a number, not '" + Ops.TypeName(x) + "'");
            }, v => v.IsFloat);

            StrType = Type("str", (vm, a, kw) =>
            {
                if (a.Length == 0) return Value.Str("");
                return Value.Str(Ops.Str(a[0]));
            }, v => v.IsStr);

            BoolType = Type("bool", (vm, a, kw) => a.Length == 0 ? Value.False : Value.Bool(Ops.Truthy(a[0])), v => v.IsBool);

            ListType = Type("list", (vm, a, kw) =>
            {
                Arity("list", a, 0, 1);
                return Value.Obj(new PyList(a.Length == 0 ? new List<Value>() : Ops.ToList(a[0])));
            }, v => v.O is PyList);

            TupleType = Type("tuple", (vm, a, kw) =>
            {
                Arity("tuple", a, 0, 1);
                if (a.Length == 0) return Value.Obj(PyTuple.Empty);
                if (a[0].O is PyTuple) return a[0];
                return Value.Obj(new PyTuple(Ops.ToList(a[0]).ToArray()));
            }, v => v.O is PyTuple);

            DictType = Type("dict", (vm, a, kw) =>
            {
                Arity("dict", a, 0, 1);
                var d = new PyDict();
                if (a.Length == 1) DictUpdate(d, a[0]);
                if (kw != null) for (int i = 0; i < kw.Count; i++) d.Set(Value.Str(kw.Names[i]), kw.Values[i]);
                return Value.Obj(d);
            }, v => v.O is PyDict);

            SetType = Type("set", (vm, a, kw) =>
            {
                Arity("set", a, 0, 1);
                return Value.Obj(a.Length == 0 ? new PySet() : new PySet(Ops.Iterate(a[0])));
            }, v => v.O is PySet s && !s.Frozen);

            FrozenSetType = Type("frozenset", (vm, a, kw) =>
            {
                Arity("frozenset", a, 0, 1);
                var s = a.Length == 0 ? new PySet() : new PySet(Ops.Iterate(a[0]));
                s.Frozen = true;
                return Value.Obj(s);
            }, v => v.O is PySet s && s.Frozen);

            RangeType = Type("range", (vm, a, kw) =>
            {
                Arity("range", a, 1, 3);
                long start = 0, stop, step = 1;
                if (a.Length == 1) stop = ToLong(a[0], "range() argument");
                else
                {
                    start = ToLong(a[0], "range() argument");
                    stop = ToLong(a[1], "range() argument");
                    if (a.Length == 3) step = ToLong(a[2], "range() argument");
                }
                if (step == 0) throw Err.Value("range() arg 3 must not be zero");
                return Value.Obj(new PyRange(start, stop, step));
            }, v => v.O is PyRange);

            SliceType = Type("slice", (vm, a, kw) =>
            {
                Arity("slice", a, 1, 3);
                if (a.Length == 1) return Value.Obj(new PySlice(Value.None, a[0], Value.None));
                return Value.Obj(new PySlice(a[0], a[1], a.Length > 2 ? a[2] : Value.None));
            }, v => v.O is PySlice);

            TypeType = Type("type", (vm, a, kw) =>
            {
                if (a.Length == 1) return TypeOf(a[0]);
                Arity("type", a, 3, 3);
                var name = ToStr(a[0], "type() argument 1");
                var bases = Ops.ToList(a[1]).Select(b => b.O as PyClass ?? throw Err.Type("bases must be classes")).ToArray();
                var cls = new PyClass(name, bases.Length == 0 ? new[] { ObjectClass } : bases);
                if (a[2].O is PyDict d) foreach (var kv in d.Items()) cls.Dict[ToStr(kv.Key, "attribute name")] = kv.Value;
                return Value.Obj(cls);
            }, v => v.O is PyClass || v.O is PyType);

            NoneType = new PyType("NoneType", (vm, a, kw) => Value.None, v => v.IsNone);
            TypeRegistry["NoneType"] = NoneType;
            FunctionType = new PyType("function", (vm, a, kw) => throw Err.Type("cannot create 'function' instances"), v => v.O is PyFunction);
            TypeRegistry["function"] = FunctionType;
        }

        static void DictUpdate(PyDict d, Value src)
        {
            if (src.O is PyDict sd) { foreach (var kv in sd.Items()) d.Set(kv.Key, kv.Value); return; }
            if (src.O is System.Collections.IDictionary hd)
            {
                foreach (System.Collections.DictionaryEntry e in hd) d.Set(Interop.ToValue(e.Key), Interop.ToValue(e.Value));
                return;
            }
            foreach (var item in Ops.Iterate(src))
            {
                var pair = Ops.ToList(item);
                if (pair.Count != 2) throw Err.Value("dictionary update sequence element has length " + pair.Count + "; 2 is required");
                d.Set(pair[0], pair[1]);
            }
        }

        internal static Value ParseInt(string s, int radix)
        {
            string t = s.Trim().Replace("_", "");
            bool neg = false;
            if (t.StartsWith("-")) { neg = true; t = t.Substring(1); }
            else if (t.StartsWith("+")) t = t.Substring(1);
            if (radix == 0 || radix == 16 || radix == 8 || radix == 2)
            {
                string lower = t.ToLowerInvariant();
                if (lower.StartsWith("0x") && (radix == 0 || radix == 16)) { radix = 16; t = t.Substring(2); }
                else if (lower.StartsWith("0o") && (radix == 0 || radix == 8)) { radix = 8; t = t.Substring(2); }
                else if (lower.StartsWith("0b") && (radix == 0 || radix == 2)) { radix = 2; t = t.Substring(2); }
                else if (radix == 0) radix = 10;
            }
            if (radix < 2 || radix > 36) throw Err.Value("int() base must be >= 2 and <= 36, or 0");
            if (t.Length == 0) throw Err.Value("invalid literal for int() with base " + radix + ": " + Format.StrRepr(s));
            if (radix == 10 && t.Length < 18 && long.TryParse(t, NumberStyles.None, Inv, out long l)) return Value.Int(neg ? -l : l);
            BigInteger v = BigInteger.Zero;
            foreach (char c in t)
            {
                int d = char.IsDigit(c) ? c - '0' : char.IsLetter(c) ? char.ToLowerInvariant(c) - 'a' + 10 : 99;
                if (d >= radix) throw Err.Value("invalid literal for int() with base " + radix + ": " + Format.StrRepr(s));
                v = v * radix + d;
            }
            return Value.BigInt(neg ? -v : v);
        }

        internal static double ParseFloat(string s)
        {
            string t = s.Trim().Replace("_", "").ToLowerInvariant();
            switch (t)
            {
                case "inf": case "+inf": case "infinity": case "+infinity": return double.PositiveInfinity;
                case "-inf": case "-infinity": return double.NegativeInfinity;
                case "nan": case "+nan": case "-nan": return double.NaN;
            }
            if (double.TryParse(t, NumberStyles.Float, Inv, out double d)) return d;
            throw Err.Value("could not convert string to float: " + Format.StrRepr(s));
        }

        // ------------------------------------------------------------ functions
        static void InitFunctions()
        {
            Def("print", (vm, a, kw) =>
            {
                string sep = " ", end = "\n";
                if (kw != null)
                {
                    kw.CheckOnly("print", "sep", "end", "file", "flush");
                    if (kw.TryGet("sep", out var s) && !s.IsNone) sep = ToStr(s, "sep");
                    if (kw.TryGet("end", out var e) && !e.IsNone) end = ToStr(e, "end");
                }
                var sb = new StringBuilder();
                for (int i = 0; i < a.Length; i++)
                {
                    if (i > 0) sb.Append(sep);
                    sb.Append(Ops.Str(a[i]));
                }
                sb.Append(end);
                vm.Out.Write(sb.ToString());
                return Value.None;
            });
            Def("len", (vm, a, kw) => { Arity("len", a, 1, 1); return Value.Int(Ops.Len(a[0])); });
            Def("repr", (vm, a, kw) => { Arity("repr", a, 1, 1); return Value.Str(Ops.Repr(a[0])); });
            Def("ascii", (vm, a, kw) => { Arity("ascii", a, 1, 1); return Value.Str(Format.Ascii(Ops.Repr(a[0]))); });
            Def("abs", (vm, a, kw) =>
            {
                Arity("abs", a, 1, 1);
                var x = a[0];
                if (x.IsInt) return x.N == long.MinValue ? Value.BigInt(BigInteger.Abs(x.N)) : Value.Int(Math.Abs(x.N));
                if (x.IsFloat) return Value.Float(Math.Abs(x.AsFloat));
                if (x.IsBool) return Value.Int(x.N);
                if (x.O is BigInteger b) return Value.BigInt(BigInteger.Abs(b));
                if (x.O is PyInstance) return vm.CallMethod(x, "__abs__", ReadOnlySpan<Value>.Empty);
                if (Interop.IsHost(x)) return Interop.ToValue(Math.Abs(Convert.ToDecimal(x.O, Inv)));
                throw Err.Type("bad operand type for abs(): '" + Ops.TypeName(x) + "'");
            });
            Def("min", (vm, a, kw) => MinMax(vm, a, kw, true));
            Def("max", (vm, a, kw) => MinMax(vm, a, kw, false));
            Def("sum", (vm, a, kw) =>
            {
                Arity("sum", a, 1, 2);
                Value acc = a.Length > 1 ? a[1] : kw?.Get("start", Value.Int(0)) ?? Value.Int(0);
                if (acc.IsStr) throw Err.Type("sum() can't sum strings [use ''.join(seq) instead]");
                if (a[0].O is PyRange r && acc.IsInt && acc.N == 0)
                {
                    long n = r.Length;
                    if (n == 0) return acc;
                    var total = (BigInteger)n * (2 * (BigInteger)r.Start + (n - 1) * (BigInteger)r.Step) / 2;
                    return Value.BigInt(total);
                }
                foreach (var x in Ops.Iterate(a[0]))
                {
                    if (acc.IsInt && x.IsInt) acc = Ops.AddInt(acc.N, x.N);
                    else acc = Ops.Add(acc, x);
                }
                return acc;
            });
            Def("sorted", (vm, a, kw) =>
            {
                Arity("sorted", a, 1, 1);
                kw?.CheckOnly("sorted", "key", "reverse");
                var list = Ops.ToList(a[0]);
                SortList(vm, list, kw?.Get("key", Value.None) ?? Value.None, kw != null && Ops.Truthy(kw.Get("reverse", Value.False)));
                return Value.Obj(new PyList(list));
            });
            Def("reversed", (vm, a, kw) =>
            {
                Arity("reversed", a, 1, 1);
                var x = a[0];
                if (x.O is PyRange r)
                {
                    long n = r.Length;
                    return Value.Obj(new RangeIter(r.At(n - 1), r.Start - r.Step, -r.Step));
                }
                if (x.O is PyInstance inst && inst.Class.Lookup("__reversed__", out _)) return vm.CallMethod(x, "__reversed__", ReadOnlySpan<Value>.Empty);
                var items = Ops.ToList(x);
                items.Reverse();
                return Value.Obj(new ArrayIter(items.ToArray()));
            });
            Def("enumerate", (vm, a, kw) =>
            {
                Arity("enumerate", a, 1, 2);
                long start = a.Length > 1 ? ToLong(a[1], "start") : kw != null ? ToLong(kw.Get("start", Value.Int(0)), "start") : 0;
                return Value.Obj(new EnumerateIter(Ops.GetIter(a[0]), start));
            });
            Def("zip", (vm, a, kw) =>
            {
                var its = new PyIterator[a.Length];
                for (int i = 0; i < a.Length; i++) its[i] = Ops.GetIter(a[i]);
                return Value.Obj(new ZipIter(its));
            });
            Def("map", (vm, a, kw) =>
            {
                if (a.Length < 2) throw Err.Type("map() must have at least two arguments.");
                var its = new PyIterator[a.Length - 1];
                for (int i = 1; i < a.Length; i++) its[i - 1] = Ops.GetIter(a[i]);
                return Value.Obj(new MapIter(a[0], its));
            });
            Def("filter", (vm, a, kw) =>
            {
                Arity("filter", a, 2, 2);
                return Value.Obj(new FilterIter(a[0], Ops.GetIter(a[1])));
            });
            Def("any", (vm, a, kw) =>
            {
                Arity("any", a, 1, 1);
                foreach (var x in Ops.Iterate(a[0])) if (Ops.Truthy(x)) return Value.True;
                return Value.False;
            });
            Def("all", (vm, a, kw) =>
            {
                Arity("all", a, 1, 1);
                foreach (var x in Ops.Iterate(a[0])) if (!Ops.Truthy(x)) return Value.False;
                return Value.True;
            });
            Def("isinstance", (vm, a, kw) => { Arity("isinstance", a, 2, 2); return Value.Bool(IsInstance(a[0], a[1])); });
            Def("issubclass", (vm, a, kw) =>
            {
                Arity("issubclass", a, 2, 2);
                return Value.Bool(IsSubclass(a[0], a[1]));
            });
            Def("hasattr", (vm, a, kw) =>
            {
                Arity("hasattr", a, 2, 2);
                try { return Value.Bool(Ops.TryGetAttr(a[0], ToStr(a[1], "attribute name"), out _)); }
                catch (ScriptException e) when (e.IsInstanceOf(AttributeError)) { return Value.False; }
            });
            Def("getattr", (vm, a, kw) =>
            {
                Arity("getattr", a, 2, 3);
                string name = ToStr(a[1], "attribute name");
                if (a.Length == 3)
                {
                    try { return Ops.GetAttr(a[0], name); }
                    catch (ScriptException e) when (e.IsInstanceOf(AttributeError)) { return a[2]; }
                }
                return Ops.GetAttr(a[0], name);
            });
            Def("setattr", (vm, a, kw) => { Arity("setattr", a, 3, 3); Ops.SetAttr(a[0], ToStr(a[1], "attribute name"), a[2]); return Value.None; });
            Def("delattr", (vm, a, kw) => { Arity("delattr", a, 2, 2); Ops.DelAttr(a[0], ToStr(a[1], "attribute name")); return Value.None; });
            Def("round", (vm, a, kw) =>
            {
                Arity("round", a, 1, 2);
                var x = a[0];
                Value nd = a.Length > 1 ? a[1] : kw?.Get("ndigits", Value.None) ?? Value.None;
                if (x.O is PyInstance) return vm.CallMethod(x, "__round__", nd.IsNone ? ReadOnlySpan<Value>.Empty : new[] { nd });
                if (x.IsIntegral)
                {
                    if (nd.IsNone || ToLong(nd, "ndigits") >= 0) return Value.Int(x.N).IsInt && !x.IsBigInt ? Value.Int(x.N) : x;
                    var p = BigInteger.Pow(10, (int)-ToLong(nd, "ndigits"));
                    var v = x.AsBig;
                    var q = BigInteger.DivRem(v, p, out var rem);
                    var twice = BigInteger.Abs(rem) * 2;
                    if (twice > p || (twice == p && !q.IsEven)) q += v.Sign;
                    return Value.BigInt(q * p);
                }
                double d = x.ToDouble();
                if (nd.IsNone)
                {
                    if (double.IsNaN(d)) throw Err.Value("cannot convert float NaN to integer");
                    if (double.IsInfinity(d)) throw Err.Overflow("cannot convert float infinity to integer");
                    double rr = Math.Round(d, MidpointRounding.ToEven);
                    return Math.Abs(rr) < 9e18 ? Value.Int((long)rr) : Value.BigInt(new BigInteger(rr));
                }
                long n = ToLong(nd, "ndigits");
                if (double.IsNaN(d) || double.IsInfinity(d) || d == 0) return x;
                return Value.Float(RoundExact(d, n));
            });
            Def("divmod", (vm, a, kw) =>
            {
                Arity("divmod", a, 2, 2);
                return Tuple(Ops.FloorDiv(a[0], a[1]), Ops.Mod(a[0], a[1]));
            });
            Def("pow", (vm, a, kw) =>
            {
                Arity("pow", a, 2, 3);
                if (a.Length == 3 && !a[2].IsNone)
                {
                    if (!a[0].IsIntegral || !a[1].IsIntegral || !a[2].IsIntegral) throw Err.Type("pow() 3rd argument not allowed unless all arguments are integers");
                    var m = a[2].AsBig;
                    if (m.IsZero) throw Err.Value("pow() 3rd argument cannot be 0");
                    var e = a[1].AsBig;
                    if (e.Sign < 0) throw Err.Value("base is not invertible for the given modulus");
                    var r = BigInteger.ModPow(a[0].AsBig, e, m);
                    if (!r.IsZero && (r.Sign < 0) != (m.Sign < 0)) r += m;
                    return Value.BigInt(r);
                }
                return Ops.Pow(a[0], a[1]);
            });
            Def("chr", (vm, a, kw) => { Arity("chr", a, 1, 1); return Value.Str(char.ConvertFromUtf32(ToInt(a[0], "chr() arg"))); });
            Def("ord", (vm, a, kw) =>
            {
                Arity("ord", a, 1, 1);
                string s = ToStr(a[0], "ord() argument");
                if (s.Length == 1) return Value.Int(s[0]);
                if (s.Length == 2 && char.IsSurrogatePair(s, 0)) return Value.Int(char.ConvertToUtf32(s, 0));
                throw Err.Type("ord() expected a character, but string of length " + s.Length + " found");
            });
            Def("hex", (vm, a, kw) => { Arity("hex", a, 1, 1); return Value.Str(RadixStr(IndexOf(a[0]).IsInt ? a[0].AsBig : a[0].AsBig, 16, "0x")); });
            Def("oct", (vm, a, kw) => { Arity("oct", a, 1, 1); return Value.Str(RadixStr(a[0].AsBig, 8, "0o")); });
            Def("bin", (vm, a, kw) => { Arity("bin", a, 1, 1); return Value.Str(RadixStr(a[0].AsBig, 2, "0b")); });
            Def("iter", (vm, a, kw) => { Arity("iter", a, 1, 1); return Value.Obj(Ops.GetIter(a[0])); });
            Def("next", (vm, a, kw) =>
            {
                Arity("next", a, 1, 2);
                if (a[0].O is PyInstance inst && inst.Class.Lookup("__next__", out _))
                {
                    try { return vm.CallMethod(a[0], "__next__", ReadOnlySpan<Value>.Empty); }
                    catch (ScriptException e) when (a.Length > 1 && e.IsInstanceOf(StopIteration)) { return a[1]; }
                }
                if (!(a[0].O is PyIterator it)) throw Err.Type("'" + Ops.TypeName(a[0]) + "' object is not an iterator");
                if (it.Next(vm, out var v)) return v;
                if (a.Length > 1) return a[1];
                throw Err.StopIter(vm.Result.IsUnbound || !(it is PyGenerator) ? Value.None : vm.Result);
            });
            Def("hash", (vm, a, kw) => { Arity("hash", a, 1, 1); return Value.Int(ValueEq.Instance.GetHashCode(a[0])); });
            Def("id", (vm, a, kw) =>
            {
                Arity("id", a, 1, 1);
                var x = a[0];
                if (x.O is Tag) return Value.Int(x.N ^ x.O.GetHashCode());
                return Value.Int(RuntimeHelpers.GetHashCode(x.O));
            });
            Def("callable", (vm, a, kw) =>
            {
                Arity("callable", a, 1, 1);
                var x = a[0];
                bool c = x.O is PyFunction || x.O is BoundMethod || x.O is BuiltinFunction || x.O is BuiltinMethod || x.O is PyClass
                    || x.O is PyType || (x.O is PyInstance i && i.Class.Lookup("__call__", out _)) || x.O is Delegate;
                return Value.Bool(c);
            });
            Def("format", (vm, a, kw) =>
            {
                Arity("format", a, 1, 2);
                return Value.Str(Format.FormatValue(a[0], a.Length > 1 ? ToStr(a[1], "format spec") : ""));
            });
            Def("vars", (vm, a, kw) =>
            {
                Arity("vars", a, 1, 1);
                return Ops.GetAttr(a[0], "__dict__");
            });
            Def("staticmethod", (vm, a, kw) => { Arity("staticmethod", a, 1, 1); return Value.Obj(new PyStaticMethod(a[0])); });
            Def("classmethod", (vm, a, kw) => { Arity("classmethod", a, 1, 1); return Value.Obj(new PyClassMethod(a[0])); });
            Def("property", (vm, a, kw) =>
            {
                Arity("property", a, 0, 3);
                Value g = a.Length > 0 ? a[0] : kw?.Get("fget", Value.None) ?? Value.None;
                Value s = a.Length > 1 ? a[1] : kw?.Get("fset", Value.None) ?? Value.None;
                Value d = a.Length > 2 ? a[2] : kw?.Get("fdel", Value.None) ?? Value.None;
                return Value.Obj(new PyProperty(g, s, d));
            });
            Def("super", (vm, a, kw) =>
            {
                Arity("super", a, 2, 2);
                if (!(a[0].O is PyClass c)) throw Err.Type("super() argument 1 must be a type");
                return Value.Obj(new PySuper(c, a[1]));
            });
            Def("checkpoint", (vm, a, kw) =>
            {
                // Requests suspension at the next safe point; the host resumes with ScriptContext.Run().
                vm.Interrupt = true;
                return Value.None;
            });
            Def("input", (vm, a, kw) => throw Err.NotImpl("input() is not available in scripts"));
        }

        /// <summary>Correctly rounded round(x, n) (round-half-even on the exact binary value, like CPython).</summary>
        internal static double RoundExact(double d, long n)
        {
            if (n > 320) return d;
            long bits = BitConverter.DoubleToInt64Bits(d);
            bool neg = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp++; else man |= 1L << 52;
            exp -= 1075;
            // value = man * 2^exp ; scaled = value * 10^n
            BigInteger num = man, den = 1;
            if (exp > 0) num <<= exp; else den <<= -exp;
            if (n >= 0) num *= BigInteger.Pow(10, (int)n); else den *= BigInteger.Pow(10, (int)-n);
            var q = BigInteger.DivRem(num, den, out var rem);
            var twice = rem * 2;
            if (twice > den || (twice == den && !q.IsEven)) q += 1;
            string digits = q.ToString(Inv);
            string text;
            if (n > 0)
            {
                digits = digits.PadLeft((int)n + 1, '0');
                text = digits.Substring(0, digits.Length - (int)n) + "." + digits.Substring(digits.Length - (int)n);
            }
            else text = digits + new string('0', (int)-n);
            double r = double.Parse(text, NumberStyles.Float, Inv);
            return neg ? -r : r;
        }

        static string RadixStr(BigInteger v, int radix, string prefix) =>
            (v.Sign < 0 ? "-" : "") + prefix + Format.ToBase(BigInteger.Abs(v), radix, false);

        internal static bool IsInstance(Value x, Value t)
        {
            switch (t.O)
            {
                case PyTuple tu:
                    foreach (var y in tu.Items) if (IsInstance(x, y)) return true;
                    return false;
                case PyClass c:
                    if (c == ObjectClass) return true;
                    return x.O is PyInstance inst && inst.Class.IsSubclassOf(c);
                case PyType pt:
                    return pt.Check(x);
                case Type ht:
                    return x.O != null && !(x.O is Tag) && ht.IsInstanceOfType(x.O);
            }
            throw Err.Type("isinstance() arg 2 must be a type or tuple of types");
        }

        static bool IsSubclass(Value a, Value b)
        {
            if (b.O is PyTuple tu)
            {
                foreach (var y in tu.Items) if (IsSubclass(a, y)) return true;
                return false;
            }
            if (a.O is PyClass ca && b.O is PyClass cb) return ca.IsSubclassOf(cb);
            if (a.O is PyType ta && b.O is PyType tb) return ta == tb || (ta == BoolType && tb == IntType);
            if (b.O is PyClass cb2 && cb2 == ObjectClass) return a.O is PyType;
            if (a.O is PyClass || a.O is PyType) return false;
            throw Err.Type("issubclass() arg 1 must be a class");
        }

        static Value MinMax(VM vm, ReadOnlySpan<Value> a, KwArgs kw, bool min)
        {
            string fn = min ? "min" : "max";
            kw?.CheckOnly(fn, "key", "default");
            Value key = kw?.Get("key", Value.None) ?? Value.None;
            IEnumerable<Value> items;
            if (a.Length == 1) items = Ops.Iterate(a[0]);
            else if (a.Length == 0) throw Err.Type(fn + " expected at least 1 argument, got 0");
            else items = a.ToArray();
            bool has = false;
            Value best = default, bestKey = default;
            foreach (var x in items)
            {
                var k = key.IsNone ? x : vm.CallValue(key, new[] { x }, null);
                if (!has) { best = x; bestKey = k; has = true; continue; }
                if (min ? Ops.Lt(k, bestKey) : Ops.Lt(bestKey, k)) { best = x; bestKey = k; }
            }
            if (!has)
            {
                if (kw != null && kw.TryGet("default", out var d)) return d;
                throw Err.Value(fn + "() arg is an empty sequence");
            }
            return best;
        }

        internal static void SortList(VM vm, List<Value> list, Value key, bool reverse)
        {
            int n = list.Count;
            if (n < 2) return;
            var keys = new Value[n];
            for (int i = 0; i < n; i++) keys[i] = key.IsNone ? list[i] : vm.CallValue(key, new[] { list[i] }, null);
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            bool allInt = true, allStr = true, allFloat = true;
            foreach (var k in keys) { allInt &= k.IsInt; allStr &= k.IsStr; allFloat &= k.IsFloat || k.IsInt; }
            Comparison<int> cmp;
            if (allInt) cmp = (x, y) => { int c = keys[x].N.CompareTo(keys[y].N); return c != 0 ? (reverse ? -c : c) : x.CompareTo(y); };
            else if (allStr) cmp = (x, y) => { int c = string.CompareOrdinal(keys[x].AsStr, keys[y].AsStr); return c != 0 ? (reverse ? -c : c) : x.CompareTo(y); };
            else if (allFloat) cmp = (x, y) => { int c = keys[x].ToDouble().CompareTo(keys[y].ToDouble()); return c != 0 ? (reverse ? -c : c) : x.CompareTo(y); };
            else cmp = (x, y) =>
            {
                int c = Ops.Lt(keys[x], keys[y]) ? -1 : Ops.Lt(keys[y], keys[x]) ? 1 : 0;
                return c != 0 ? (reverse ? -c : c) : x.CompareTo(y);
            };
            try { Array.Sort(idx, cmp); }
            catch (InvalidOperationException e) when (e.InnerException is ScriptException se) { throw se; }
            var copy = list.ToArray();
            for (int i = 0; i < n; i++) list[i] = copy[idx[i]];
        }

        internal static Value Import(string name, VM vm)
        {
            if (Modules.TryGetValue(name, out var m)) return Value.Obj(m);
            if (vm.Context != null && vm.Context.HostModules.TryGetValue(name, out var hm)) return hm;
            throw new ScriptException(ModuleNotFoundError, "No module named '" + name + "'");
        }

        internal static bool TryPropertyAttr(Value o, PyProperty p, string name, out Value v)
        {
            MethodFn fn;
            switch (name)
            {
                case "getter": fn = (vm, self, a, kw) => Value.Obj(new PyProperty(a[0], p.Set, p.Del)); break;
                case "setter": fn = (vm, self, a, kw) => Value.Obj(new PyProperty(p.Get, a[0], p.Del)); break;
                case "deleter": fn = (vm, self, a, kw) => Value.Obj(new PyProperty(p.Get, p.Set, a[0])); break;
                case "fget": v = p.Get; return true;
                case "fset": v = p.Set; return true;
                default: v = default; return false;
            }
            v = Value.Obj(new BuiltinMethod(o, name, fn));
            return true;
        }
    }
}
