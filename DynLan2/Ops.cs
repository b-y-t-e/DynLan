using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DynLan2
{
    /// <summary>Python semantics of operators, attribute access, items, iteration, repr/str.</summary>
    internal static partial class Ops
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ type info
        public static string TypeName(Value v)
        {
            object o = v.O;
            if (o == null) return "<unbound>";
            if (o is Tag t) return t.Name;
            switch (o)
            {
                case string _: return "str";
                case BigInteger _: return "int";
                case PyList _: return "list";
                case PyTuple _: return "tuple";
                case PyDict _: return "dict";
                case PySet s: return s.Frozen ? "frozenset" : "set";
                case PyFunction _: return "function";
                case BoundMethod _: return "method";
                case BuiltinFunction _: return "builtin_function_or_method";
                case BuiltinMethod _: return "builtin_function_or_method";
                case PyClass _: return "type";
                case PyType _: return "type";
                case PyInstance i: return i.Class.Name;
                case PyRange _: return "range";
                case PySlice _: return "slice";
                case PyGenerator _: return "generator";
                case PyModule _: return "module";
                case DictView dv: return dv.Mode == 0 ? "dict_keys" : dv.Mode == 1 ? "dict_values" : "dict_items";
                case PyIterator _: return "iterator";
                case PyProperty _: return "property";
                case PySuper _: return "super";
                case Cell _: return "cell";
                default: return o.GetType().Name;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Truthy(Value v)
        {
            object o = v.O;
            if (ReferenceEquals(o, Value.TBool) || ReferenceEquals(o, Value.TInt)) return v.N != 0;
            if (ReferenceEquals(o, Value.TNone)) return false;
            return TruthySlow(v);
        }

        static bool TruthySlow(Value v)
        {
            switch (v.O)
            {
                case Tag _: return v.IsFloat ? v.AsFloat != 0.0 : v.N != 0;
                case string s: return s.Length != 0;
                case BigInteger b: return !b.IsZero;
                case PyList l: return l.Items.Count != 0;
                case PyTuple t: return t.Items.Length != 0;
                case PyDict d: return d.Count != 0;
                case PySet s: return s.Items.Count != 0;
                case PyRange r: return r.Length != 0;
                case DictView dv: return dv.D.Count != 0;
                case PyInstance inst:
                    {
                        var vm = VM.Current;
                        if (inst.Class.Lookup("__bool__", out _)) return Truthy(vm.CallMethod(v, "__bool__", ReadOnlySpan<Value>.Empty));
                        if (inst.Class.Lookup("__len__", out _)) return Truthy(vm.CallMethod(v, "__len__", ReadOnlySpan<Value>.Empty));
                        return true;
                    }
                case null: throw Err.Runtime("unbound value");
                case ICollection c: return c.Count != 0;
                default: return true;
            }
        }

        // ------------------------------------------------------------ arithmetic
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value AddInt(long x, long y)
        {
            long s = x + y;
            if (((x ^ s) & (y ^ s)) < 0) return Value.BigInt((BigInteger)x + y);
            return Value.Int(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value SubInt(long x, long y)
        {
            long s = x - y;
            if (((x ^ y) & (x ^ s)) < 0) return Value.BigInt((BigInteger)x - y);
            return Value.Int(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value MulInt(long x, long y)
        {
            long hi = Compat.BigMul(x, y, out long lo);
            if (hi != (lo >> 63)) return Value.BigInt((BigInteger)x * y);
            return Value.Int(lo);
        }

        public static long FloorDivLong(long x, long y)
        {
            if (y == 0) throw Err.ZeroDiv("integer division or modulo by zero");
            long q = x / y;
            if ((x % y != 0) && ((x < 0) != (y < 0))) q--;
            return q;
        }

        public static long ModLong(long x, long y)
        {
            if (y == 0) throw Err.ZeroDiv("integer division or modulo by zero");
            if (y == -1) return 0;
            long r = x % y;
            if (r != 0 && ((r < 0) != (y < 0))) r += y;
            return r;
        }

        static double ModFloat(double x, double y)
        {
            if (y == 0) throw Err.ZeroDiv("float modulo");
            double r = x % y;
            if (r != 0 && ((r < 0) != (y < 0))) r += y;
            return r;
        }

        static BigInteger FloorDivBig(BigInteger x, BigInteger y)
        {
            if (y.IsZero) throw Err.ZeroDiv("integer division or modulo by zero");
            var q = BigInteger.DivRem(x, y, out var r);
            if (!r.IsZero && ((r.Sign < 0) != (y.Sign < 0))) q -= 1;
            return q;
        }

        static BigInteger ModBig(BigInteger x, BigInteger y)
        {
            if (y.IsZero) throw Err.ZeroDiv("integer division or modulo by zero");
            var r = BigInteger.Remainder(x, y);
            if (!r.IsZero && ((r.Sign < 0) != (y.Sign < 0))) r += y;
            return r;
        }

        enum NumKind { None, Int, Big, Float }

        static NumKind Kind(Value v)
        {
            object o = v.O;
            if (ReferenceEquals(o, Value.TInt) || ReferenceEquals(o, Value.TBool)) return NumKind.Int;
            if (ReferenceEquals(o, Value.TFloat)) return NumKind.Float;
            if (o is BigInteger) return NumKind.Big;
            return NumKind.None;
        }

        public static Value Binary(Op op, Value a, Value b)
        {
            switch (op)
            {
                case Op.ADD: return Add(a, b);
                case Op.IADD: return IAdd(a, b);
                case Op.SUB: return Sub(a, b);
                case Op.MUL: return Mul(a, b);
                case Op.TRUEDIV: return TrueDiv(a, b);
                case Op.FLOORDIV: return FloorDiv(a, b);
                case Op.MOD: return Mod(a, b);
                case Op.POW: return Pow(a, b);
                case Op.LSHIFT: return Shift(a, b, true);
                case Op.RSHIFT: return Shift(a, b, false);
                case Op.BITAND: return Bitwise(a, b, '&');
                case Op.BITOR: return Bitwise(a, b, '|');
                case Op.BITXOR: return Bitwise(a, b, '^');
                case Op.MATMUL: return Dunder(a, b, "__matmul__", "__rmatmul__", "@");
                case Op.LT: return Value.Bool(Lt(a, b));
                case Op.LE: return Value.Bool(Le(a, b));
                case Op.GT: return Value.Bool(Lt(b, a));
                case Op.GE: return Value.Bool(Le(b, a));
                case Op.EQ: return Value.Bool(EqValues(a, b));
                case Op.NE: return Value.Bool(!EqValues(a, b));
                case Op.IN: return Value.Bool(Contains(b, a));
                case Op.NOTIN: return Value.Bool(!Contains(b, a));
                case Op.IS: return Value.Bool(Is(a, b));
                case Op.ISNOT: return Value.Bool(!Is(a, b));
            }
            throw new InvalidOperationException(op.ToString());
        }

        public static bool Is(Value a, Value b)
        {
            if (a.O is Tag) return ReferenceEquals(a.O, b.O) && a.N == b.N;
            if (a.O is string sa && b.O is string sb) return ReferenceEquals(sa, sb) || (sa.Length == 0 && sb.Length == 0);
            return ReferenceEquals(a.O, b.O);
        }

        public static Value Add(Value a, Value b)
        {
            if (a.IsInt && b.IsInt) return AddInt(a.N, b.N);
            if (a.O is string sa && b.O is string sb) return Value.Str(string.Concat(sa, sb));
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Float || kb == NumKind.Float) return Value.Float(a.ToDouble() + b.ToDouble());
                if (ka == NumKind.Int && kb == NumKind.Int) return AddInt(a.N, b.N);
                return Value.BigInt(a.AsBig + b.AsBig);
            }
            if (a.O is PyList la && b.O is PyList lb)
            {
                var r = new List<Value>(la.Items.Count + lb.Items.Count);
                r.AddRange(la.Items); r.AddRange(lb.Items);
                return Value.Obj(new PyList(r));
            }
            if (a.O is PyTuple ta && b.O is PyTuple tb)
            {
                var r = new Value[ta.Items.Length + tb.Items.Length];
                ta.Items.CopyTo(r, 0); tb.Items.CopyTo(r, ta.Items.Length);
                return Value.Obj(new PyTuple(r));
            }
            if (a.O is string && !(b.O is PyInstance)) throw Err.Type("can only concatenate str (not \"" + TypeName(b) + "\") to str");
            return Dunder(a, b, "__add__", "__radd__", "+");
        }

        public static Value IAdd(Value a, Value b)
        {
            if (a.O is PyList la)
            {
                if (b.O is PyList lb) { la.Items.AddRange(lb.Items.ToArray()); return a; }
                foreach (var x in Iterate(b)) la.Items.Add(x);
                return a;
            }
            if (a.O is PyInstance inst && inst.Class.Lookup("__iadd__", out _))
                return VM.Current.CallMethod(a, "__iadd__", new[] { b });
            return Add(a, b);
        }

        public static Value Sub(Value a, Value b)
        {
            if (a.IsInt && b.IsInt) return SubInt(a.N, b.N);
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Float || kb == NumKind.Float) return Value.Float(a.ToDouble() - b.ToDouble());
                if (ka == NumKind.Int && kb == NumKind.Int) return SubInt(a.N, b.N);
                return Value.BigInt(a.AsBig - b.AsBig);
            }
            if (a.O is PySet sa && b.O is PySet sb)
            {
                var r = new PySet(sa.Items);
                r.Items.ExceptWith(sb.Items);
                return Value.Obj(r);
            }
            if (a.O is DateTime || a.O is TimeSpan) return Interop.HostBinary("-", a, b);
            return Dunder(a, b, "__sub__", "__rsub__", "-");
        }

        public static Value Mul(Value a, Value b)
        {
            if (a.IsInt && b.IsInt) return MulInt(a.N, b.N);
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Float || kb == NumKind.Float) return Value.Float(a.ToDouble() * b.ToDouble());
                if (ka == NumKind.Int && kb == NumKind.Int) return MulInt(a.N, b.N);
                return Value.BigInt(a.AsBig * b.AsBig);
            }
            if (ka == NumKind.Int && !(b.O is PyInstance)) { var t = a; a = b; b = t; kb = ka; }
            if (kb == NumKind.Int)
            {
                long n = Math.Max(0, b.N);
                if (a.O is string s)
                {
                    if (n == 0 || s.Length == 0) return Value.Str("");
                    var sb = new StringBuilder(checked((int)(s.Length * n)));
                    for (long i = 0; i < n; i++) sb.Append(s);
                    return Value.Str(sb.ToString());
                }
                if (a.O is PyList l)
                {
                    var r = new List<Value>(checked((int)(l.Items.Count * n)));
                    for (long i = 0; i < n; i++) r.AddRange(l.Items);
                    return Value.Obj(new PyList(r));
                }
                if (a.O is PyTuple tu)
                {
                    var r = new Value[checked(tu.Items.Length * (int)n)];
                    for (int i = 0; i < n; i++) tu.Items.CopyTo(r, i * tu.Items.Length);
                    return Value.Obj(new PyTuple(r));
                }
            }
            return Dunder(a, b, "__mul__", "__rmul__", "*");
        }

        public static Value TrueDiv(Value a, Value b)
        {
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                double y = b.ToDouble();
                if (y == 0) throw Err.ZeroDiv(ka == NumKind.Float || kb == NumKind.Float ? "float division by zero" : "division by zero");
                if (ka == NumKind.Big || kb == NumKind.Big)
                {
                    var bx = a.AsBig; var by = b.AsBig;
                    if (ka != NumKind.Float && kb != NumKind.Float && BigInteger.Remainder(bx, by).IsZero)
                        return Value.Float((double)(bx / by));
                }
                return Value.Float(a.ToDouble() / y);
            }
            return Dunder(a, b, "__truediv__", "__rtruediv__", "/");
        }

        public static Value FloorDiv(Value a, Value b)
        {
            if (a.IsInt && b.IsInt)
            {
                if (a.N == long.MinValue && b.N == -1) return Value.BigInt(-(BigInteger)a.N);
                return Value.Int(FloorDivLong(a.N, b.N));
            }
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Float || kb == NumKind.Float)
                {
                    double y = b.ToDouble();
                    if (y == 0) throw Err.ZeroDiv("float floor division by zero");
                    return Value.Float(Math.Floor(a.ToDouble() / y));
                }
                if (ka == NumKind.Int && kb == NumKind.Int) return Value.Int(FloorDivLong(a.N, b.N));
                return Value.BigInt(FloorDivBig(a.AsBig, b.AsBig));
            }
            return Dunder(a, b, "__floordiv__", "__rfloordiv__", "//");
        }

        public static Value Mod(Value a, Value b)
        {
            if (a.IsInt && b.IsInt) return Value.Int(ModLong(a.N, b.N));
            if (a.O is string fmt) return Value.Str(Format.Percent(fmt, b));
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Float || kb == NumKind.Float) return Value.Float(ModFloat(a.ToDouble(), b.ToDouble()));
                if (ka == NumKind.Int && kb == NumKind.Int) return Value.Int(ModLong(a.N, b.N));
                return Value.BigInt(ModBig(a.AsBig, b.AsBig));
            }
            return Dunder(a, b, "__mod__", "__rmod__", "%");
        }

        public static Value Pow(Value a, Value b)
        {
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Float || kb == NumKind.Float)
                {
                    double x = a.ToDouble(), y = b.ToDouble();
                    if (x == 0 && y < 0) throw Err.ZeroDiv("0.0 cannot be raised to a negative power");
                    double r = Math.Pow(x, y);
                    if (double.IsNaN(r) && x < 0 && !double.IsNaN(y)) throw Err.Value("negative number cannot be raised to a fractional power");
                    return Value.Float(r);
                }
                var e = b.AsBig;
                if (e.Sign < 0)
                {
                    if (a.AsBig.IsZero) throw Err.ZeroDiv("0.0 cannot be raised to a negative power");
                    return Value.Float(Math.Pow(a.ToDouble(), (double)e));
                }
                if (ka == NumKind.Int && e <= 64)
                {
                    long bse = a.N, res = 1;
                    int ex = (int)e;
                    bool overflow = false;
                    while (ex > 0)
                    {
                        if ((ex & 1) != 0)
                        {
                            long hi = Compat.BigMul(res, bse, out long lo);
                            if (hi != (lo >> 63)) { overflow = true; break; }
                            res = lo;
                        }
                        ex >>= 1;
                        if (ex > 0)
                        {
                            long hi = Compat.BigMul(bse, bse, out long lo);
                            if (hi != (lo >> 63)) { overflow = true; break; }
                            bse = lo;
                        }
                    }
                    if (!overflow) return Value.Int(res);
                }
                if (e > int.MaxValue) throw Err.Overflow("exponent too large");
                return Value.BigInt(BigInteger.Pow(a.AsBig, (int)e));
            }
            return Dunder(a, b, "__pow__", "__rpow__", "**");
        }

        static Value Shift(Value a, Value b, bool left)
        {
            var ka = Kind(a); var kb = Kind(b);
            if ((ka == NumKind.Int || ka == NumKind.Big) && (kb == NumKind.Int || kb == NumKind.Big))
            {
                var n = b.AsBig;
                if (n.Sign < 0) throw Err.Value("negative shift count");
                if (n > 1_000_000) throw Err.Overflow("shift count too large");
                int s = (int)n;
                if (left)
                {
                    if (ka == NumKind.Int && s < 63)
                    {
                        long x = a.N;
                        long r = x << s;
                        if ((r >> s) == x) return Value.Int(r);
                    }
                    return Value.BigInt(a.AsBig << s);
                }
                if (ka == NumKind.Int) return Value.Int(s >= 63 ? (a.N < 0 ? -1 : 0) : a.N >> s);
                return Value.BigInt(a.AsBig >> s);
            }
            return Dunder(a, b, left ? "__lshift__" : "__rshift__", left ? "__rlshift__" : "__rrshift__", left ? "<<" : ">>");
        }

        static Value Bitwise(Value a, Value b, char op)
        {
            if (a.IsBool && b.IsBool)
            {
                bool x = a.AsBool, y = b.AsBool;
                return Value.Bool(op == '&' ? x & y : op == '|' ? x | y : x ^ y);
            }
            var ka = Kind(a); var kb = Kind(b);
            if ((ka == NumKind.Int || ka == NumKind.Big) && (kb == NumKind.Int || kb == NumKind.Big))
            {
                if (ka == NumKind.Int && kb == NumKind.Int)
                    return Value.Int(op == '&' ? a.N & b.N : op == '|' ? a.N | b.N : a.N ^ b.N);
                var x = a.AsBig; var y = b.AsBig;
                return Value.BigInt(op == '&' ? x & y : op == '|' ? x | y : x ^ y);
            }
            if (a.O is PySet sa && b.O is PySet sb)
            {
                var r = new PySet(sa.Items);
                if (op == '&') r.Items.IntersectWith(sb.Items);
                else if (op == '|') r.Items.UnionWith(sb.Items);
                else r.Items.SymmetricExceptWith(sb.Items);
                return Value.Obj(r);
            }
            if (op == '|' && a.O is PyDict da && b.O is PyDict db)
            {
                var r = da.Copy();
                foreach (var kv in db.Items()) r.Set(kv.Key, kv.Value);
                return Value.Obj(r);
            }
            string name = op == '&' ? "and" : op == '|' ? "or" : "xor";
            return Dunder(a, b, "__" + name + "__", "__r" + name + "__", op.ToString());
        }

        static Value Dunder(Value a, Value b, string name, string rname, string sym)
        {
            var vm = VM.Current;
            if (a.O is PyInstance ia && ia.Class.Lookup(name, out _))
            {
                var r = vm.CallMethod(a, name, new[] { b });
                if (!ReferenceEquals(r.O, Builtins.NotImplementedObj)) return r;
            }
            if (b.O is PyInstance ib && ib.Class.Lookup(rname, out _))
            {
                var r = vm.CallMethod(b, rname, new[] { a });
                if (!ReferenceEquals(r.O, Builtins.NotImplementedObj)) return r;
            }
            if (Interop.IsHost(a) || Interop.IsHost(b)) return Interop.HostBinary(sym, a, b);
            throw Err.Unsupported(sym, a, b);
        }

        public static Value Neg(Value a)
        {
            if (a.IsInt) return a.N == long.MinValue ? Value.BigInt(-(BigInteger)a.N) : Value.Int(-a.N);
            if (a.IsFloat) return Value.Float(-a.AsFloat);
            if (a.IsBool) return Value.Int(-a.N);
            if (a.O is BigInteger b) return Value.BigInt(-b);
            if (a.O is PyInstance) return VM.Current.CallMethod(a, "__neg__", ReadOnlySpan<Value>.Empty);
            if (Interop.IsHost(a)) return Interop.HostUnary("-", a);
            throw Err.Type("bad operand type for unary -: '" + TypeName(a) + "'");
        }

        public static Value Pos(Value a)
        {
            if (a.IsInt || a.IsFloat || a.O is BigInteger) return a;
            if (a.IsBool) return Value.Int(a.N);
            if (a.O is PyInstance) return VM.Current.CallMethod(a, "__pos__", ReadOnlySpan<Value>.Empty);
            throw Err.Type("bad operand type for unary +: '" + TypeName(a) + "'");
        }

        public static Value Invert(Value a)
        {
            if (a.IsInt || a.IsBool) return Value.Int(~a.N);
            if (a.O is BigInteger b) return Value.BigInt(-(b + 1));
            if (a.O is PyInstance) return VM.Current.CallMethod(a, "__invert__", ReadOnlySpan<Value>.Empty);
            throw Err.Type("bad operand type for unary ~: '" + TypeName(a) + "'");
        }

        // ------------------------------------------------------------ comparison
        public static bool EqValues(Value a, Value b)
        {
            object x = a.O, y = b.O;
            if (x is string sx) return y is string sy && string.Equals(sx, sy, StringComparison.Ordinal);
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Int && kb == NumKind.Int) return a.N == b.N;
                if (ka == NumKind.Float || kb == NumKind.Float)
                {
                    if (ka == NumKind.Big || kb == NumKind.Big)
                    {
                        double d = ka == NumKind.Float ? a.AsFloat : b.AsFloat;
                        var big = ka == NumKind.Big ? (BigInteger)x : (BigInteger)y;
                        return !double.IsInfinity(d) && d == Math.Floor(d) && new BigInteger(d) == big;
                    }
                    return a.ToDouble() == b.ToDouble();
                }
                return a.AsBig == b.AsBig;
            }
            if (ReferenceEquals(x, Value.TNone)) return ReferenceEquals(y, Value.TNone);
            if (ReferenceEquals(x, y)) return true;
            switch (x)
            {
                case PyList la:
                    return y is PyList lb && SeqEq(la.Items, lb.Items);
                case PyTuple ta:
                    return y is PyTuple tb && SeqEq(ta.Items, tb.Items);
                case PyDict da:
                    {
                        if (!(y is PyDict db) || da.Count != db.Count) return false;
                        foreach (var kv in da.Items())
                            if (!db.TryGet(kv.Key, out var v2) || !EqValues(kv.Value, v2)) return false;
                        return true;
                    }
                case PySet sa:
                    return y is PySet sb && sa.Items.SetEquals(sb.Items);
                case PyRange ra:
                    return y is PyRange rb && ra.Length == rb.Length && (ra.Length == 0 || (ra.Start == rb.Start && (ra.Length == 1 || ra.Step == rb.Step)));
                case PyInstance ia:
                    {
                        if (ia.Class.Lookup("__eq__", out _))
                        {
                            var r = VM.Current.CallMethod(a, "__eq__", new[] { b });
                            if (!ReferenceEquals(r.O, Builtins.NotImplementedObj)) return Truthy(r);
                        }
                        break;
                    }
                case BoundMethod ma:
                    return y is BoundMethod mb && Is(ma.Self, mb.Self) && ReferenceEquals(ma.Func.O, mb.Func.O);
                case Tag _:
                    return false;
            }
            if (y is PyInstance ib && ib.Class.Lookup("__eq__", out _))
            {
                var r = VM.Current.CallMethod(b, "__eq__", new[] { a });
                if (!ReferenceEquals(r.O, Builtins.NotImplementedObj)) return Truthy(r);
            }
            if (x != null && !(x is Tag) && y != null && !(y is Tag) && Interop.IsHostObject(x)) return x.Equals(y);
            return false;
        }

        static bool SeqEq(IList<Value> a, IList<Value> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!Is(a[i], b[i]) && !EqValues(a[i], b[i])) return false;
            return true;
        }

        /// <summary>Three-way compare for orderable built-ins; null when not comparable natively.</summary>
        static int? Compare3(Value a, Value b)
        {
            var ka = Kind(a); var kb = Kind(b);
            if (ka != NumKind.None && kb != NumKind.None)
            {
                if (ka == NumKind.Int && kb == NumKind.Int) return a.N.CompareTo(b.N);
                if (ka == NumKind.Float || kb == NumKind.Float)
                {
                    double x = a.ToDouble(), y = b.ToDouble();
                    if (double.IsNaN(x) || double.IsNaN(y)) return null;
                    return x.CompareTo(y);
                }
                return a.AsBig.CompareTo(b.AsBig);
            }
            if (a.O is string sa && b.O is string sb) return Math.Sign(string.CompareOrdinal(sa, sb));
            if (a.O is PyList la && b.O is PyList lb) return SeqCompare(la.Items, lb.Items);
            if (a.O is PyTuple ta && b.O is PyTuple tb) return SeqCompare(ta.Items, tb.Items);
            if (a.O is IComparable ca && a.O.GetType() == b.O?.GetType() && Interop.IsHostObject(a.O)) return ca.CompareTo(b.O);
            return null;
        }

        static int SeqCompare(IList<Value> a, IList<Value> b)
        {
            int n = Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++)
            {
                if (Is(a[i], b[i]) || EqValues(a[i], b[i])) continue;
                return Lt(a[i], b[i]) ? -1 : 1;
            }
            return a.Count.CompareTo(b.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Lt(Value a, Value b)
        {
            if (a.IsInt && b.IsInt) return a.N < b.N;
            if (a.IsFloat && b.IsFloat) return a.AsFloat < b.AsFloat;
            return Rich(a, b, "__lt__", "__gt__", "<", c => c < 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Le(Value a, Value b)
        {
            if (a.IsInt && b.IsInt) return a.N <= b.N;
            if (a.IsFloat && b.IsFloat) return a.AsFloat <= b.AsFloat;
            return Rich(a, b, "__le__", "__ge__", "<=", c => c <= 0);
        }

        static bool Rich(Value a, Value b, string name, string reflected, string sym, Func<int, bool> pred)
        {
            if (a.O is PySet sa && b.O is PySet sb)
            {
                if (sym == "<") return sa.Items.IsProperSubsetOf(sb.Items);
                return sa.Items.IsSubsetOf(sb.Items);
            }
            var c = Compare3(a, b);
            if (c.HasValue) return pred(c.Value);
            if ((a.IsFloat || b.IsFloat) && Kind(a) != NumKind.None && Kind(b) != NumKind.None) return false; // NaN
            var vm = VM.Current;
            if (a.O is PyInstance ia && ia.Class.Lookup(name, out _))
            {
                var r = vm.CallMethod(a, name, new[] { b });
                if (!ReferenceEquals(r.O, Builtins.NotImplementedObj)) return Truthy(r);
            }
            if (b.O is PyInstance ib && ib.Class.Lookup(reflected, out _))
            {
                var r = vm.CallMethod(b, reflected, new[] { a });
                if (!ReferenceEquals(r.O, Builtins.NotImplementedObj)) return Truthy(r);
            }
            throw Err.Type("'" + sym + "' not supported between instances of '" + TypeName(a) + "' and '" + TypeName(b) + "'");
        }

        public static bool CompareOp(Cmp op, Value a, Value b)
        {
            switch (op)
            {
                case Cmp.LT: return Lt(a, b);
                case Cmp.LE: return Le(a, b);
                case Cmp.GT: return Lt(b, a);
                case Cmp.GE: return Le(b, a);
                case Cmp.EQ: return EqValues(a, b);
                default: return !EqValues(a, b);
            }
        }

        public static int InstanceHash(PyInstance inst)
        {
            if (inst.Class.Lookup("__hash__", out var h))
            {
                if (h.IsNone) throw Err.Type("unhashable type: '" + inst.Class.Name + "'");
                var r = VM.Current.CallMethod(Value.Obj(inst), "__hash__", ReadOnlySpan<Value>.Empty);
                return r.N.GetHashCode();
            }
            if (inst.Class.Lookup("__eq__", out _)) throw Err.Type("unhashable type: '" + inst.Class.Name + "'");
            return RuntimeHelpers.GetHashCode(inst);
        }

        // ------------------------------------------------------------ containment / length
        public static bool Contains(Value container, Value item)
        {
            switch (container.O)
            {
                case string s:
                    if (item.O is string sub) return s.IndexOf(sub, StringComparison.Ordinal) >= 0;
                    throw Err.Type("'in <string>' requires string as left operand, not " + TypeName(item));
                case PyList l:
                    foreach (var x in l.Items) if (Is(x, item) || EqValues(x, item)) return true;
                    return false;
                case PyTuple t:
                    foreach (var x in t.Items) if (Is(x, item) || EqValues(x, item)) return true;
                    return false;
                case PyDict d: return d.ContainsKey(item);
                case PySet st: return st.Items.Contains(item);
                case PyRange r:
                    if (item.IsInt || item.IsBool) return r.Contains(item.N);
                    break;
                case DictView dv:
                    if (dv.Mode == 0) return dv.D.ContainsKey(item);
                    break;
                case PyInstance inst:
                    if (inst.Class.Lookup("__contains__", out _))
                        return Truthy(VM.Current.CallMethod(container, "__contains__", new[] { item }));
                    break;
                case IDictionary hd:
                    return hd.Contains(Interop.ToClr(item, typeof(object)));
            }
            foreach (var x in Iterate(container)) if (Is(x, item) || EqValues(x, item)) return true;
            return false;
        }

        public static long Len(Value v)
        {
            switch (v.O)
            {
                case string s: return s.Length;
                case PyList l: return l.Items.Count;
                case PyTuple t: return t.Items.Length;
                case PyDict d: return d.Count;
                case PySet s: return s.Items.Count;
                case PyRange r: return r.Length;
                case DictView dv: return dv.D.Count;
                case PyInstance inst:
                    if (inst.Class.Lookup("__len__", out _))
                        return VM.Current.CallMethod(v, "__len__", ReadOnlySpan<Value>.Empty).N;
                    break;
                case Array a: return a.Length;
                case ICollection c: return c.Count;
            }
            throw Err.Type("object of type '" + TypeName(v) + "' has no len()");
        }

        // ------------------------------------------------------------ iteration
        public static PyIterator GetIter(Value v)
        {
            switch (v.O)
            {
                case PyList l: return new ListIter(l);
                case PyTuple t: return new ArrayIter(t.Items);
                case string s: return new StrIter(s);
                case PyRange r: return new RangeIter(r.Start, r.Stop, r.Step);
                case PyDict d: return new DictIter(d, 0);
                case DictView dv: return new DictIter(dv.D, dv.Mode);
                case PySet st: { var arr = new Value[st.Items.Count]; st.Items.CopyTo(arr); return new ArrayIter(arr); }
                case PyIterator it: return it;
                case PyInstance inst:
                    if (inst.Class.Lookup("__iter__", out _))
                    {
                        var r = VM.Current.CallMethod(v, "__iter__", ReadOnlySpan<Value>.Empty);
                        if (r.O is PyIterator pi) return pi;
                        if (r.O is PyInstance) return new InstanceIter(r);
                        return GetIter(r);
                    }
                    if (inst.Class.Lookup("__getitem__", out _)) return new GetItemIter(v);
                    break;
                case IEnumerable e when Interop.IsHostObject(v.O): return new HostIter(e.GetEnumerator());
            }
            throw Err.Type("'" + TypeName(v) + "' object is not iterable");
        }

        public static IEnumerable<Value> Iterate(Value v)
        {
            if (v.O is PyList l)
            {
                for (int i = 0; i < l.Items.Count; i++) yield return l.Items[i];
                yield break;
            }
            if (v.O is PyTuple t)
            {
                foreach (var x in t.Items) yield return x;
                yield break;
            }
            var it = GetIter(v);
            var vm = VM.Current;
            while (it.Next(vm, out var x)) yield return x;
        }

        public static List<Value> ToList(Value v)
        {
            if (v.O is PyList l) return new List<Value>(l.Items);
            if (v.O is PyTuple t) return new List<Value>(t.Items);
            var r = new List<Value>();
            foreach (var x in Iterate(v)) r.Add(x);
            return r;
        }

        internal sealed class GetItemIter : PyIterator
        {
            internal Value Obj;
            internal long I;
            public GetItemIter(Value obj) { Obj = obj; }
            internal override bool Next(VM vm, out Value v)
            {
                try { v = GetItem(Obj, Value.Int(I++)); return true; }
                catch (ScriptException e) when (e.IsInstanceOf(Builtins.IndexError) || e.IsInstanceOf(Builtins.StopIteration))
                {
                    v = default;
                    return false;
                }
            }
        }

        // ------------------------------------------------------------ items
        static int NormIndex(Value idx, int len, string what)
        {
            if (!(idx.IsInt || idx.IsBool))
            {
                if (idx.O is BigInteger) throw Err.Index(what + " index out of range");
                if (idx.O is PyInstance) idx = Builtins.IndexOf(idx);
                else throw Err.Type(what + " indices must be integers or slices, not " + TypeName(idx));
            }
            long i = idx.N;
            if (i < 0) i += len;
            if (i < 0 || i >= len) throw Err.Index(what + " index out of range");
            return (int)i;
        }

        public static Value GetItem(Value o, Value k)
        {
            switch (o.O)
            {
                case PyList l:
                    if (k.O is PySlice ls) return Value.Obj(new PyList(SliceList(l.Items, ls)));
                    return l.Items[NormIndex(k, l.Items.Count, "list")];
                case PyDict d:
                    if (d.TryGet(k, out var v)) return v;
                    throw Err.Key(k);
                case PyTuple t:
                    if (k.O is PySlice ts) return Value.Obj(new PyTuple(SliceList(t.Items, ts).ToArray()));
                    return t.Items[NormIndex(k, t.Items.Length, "tuple")];
                case string s:
                    if (k.O is PySlice ss) return Value.Str(SliceStr(s, ss));
                    return Value.Str(Builtins.CharStr(s[NormIndex(k, s.Length, "string")]));
                case PyRange r:
                    {
                        if (k.O is PySlice rs)
                        {
                            var (start, stop, step, _) = SliceIndices(rs, r.Length);
                            return Value.Obj(new PyRange(r.At(start), r.At(stop), r.Step * step));
                        }
                        long len = r.Length;
                        long i = k.N;
                        if (!k.IsInt) throw Err.Type("range indices must be integers");
                        if (i < 0) i += len;
                        if (i < 0 || i >= len) throw Err.Index("range object index out of range");
                        return Value.Int(r.At(i));
                    }
                case PyInstance inst:
                    if (inst.Class.Lookup("__getitem__", out _))
                        return VM.Current.CallMethod(o, "__getitem__", new[] { k });
                    break;
                case PyClass _:
                case PyType _:
                    return o; // generic alias: list[int] etc.
            }
            if (Interop.IsHost(o)) return Interop.GetIndex(o.O, k);
            throw Err.Type("'" + TypeName(o) + "' object is not subscriptable");
        }

        public static void SetItem(Value o, Value k, Value v)
        {
            switch (o.O)
            {
                case PyList l:
                    if (k.O is PySlice sl) { SetSlice(l.Items, sl, v); return; }
                    l.Items[NormIndex(k, l.Items.Count, "list assignment")] = v;
                    return;
                case PyDict d:
                    d.Set(k, v);
                    return;
                case PyInstance inst:
                    if (inst.Class.Lookup("__setitem__", out _)) { VM.Current.CallMethod(o, "__setitem__", new[] { k, v }); return; }
                    break;
            }
            if (Interop.IsHost(o)) { Interop.SetIndex(o.O, k, v); return; }
            throw Err.Type("'" + TypeName(o) + "' object does not support item assignment");
        }

        public static void DelItem(Value o, Value k)
        {
            switch (o.O)
            {
                case PyList l:
                    if (k.O is PySlice sl)
                    {
                        var (start, stop, step, n) = SliceIndices(sl, l.Items.Count);
                        if (step == 1) { if (n > 0) l.Items.RemoveRange((int)start, (int)n); return; }
                        var idx = new List<int>();
                        for (long i = 0, j = start; i < n; i++, j += step) idx.Add((int)j);
                        idx.Sort();
                        for (int i = idx.Count - 1; i >= 0; i--) l.Items.RemoveAt(idx[i]);
                        return;
                    }
                    l.Items.RemoveAt(NormIndex(k, l.Items.Count, "list assignment"));
                    return;
                case PyDict d:
                    if (!d.Remove(k, out _)) throw Err.Key(k);
                    return;
                case PyInstance inst:
                    if (inst.Class.Lookup("__delitem__", out _)) { VM.Current.CallMethod(o, "__delitem__", new[] { k }); return; }
                    break;
            }
            throw Err.Type("'" + TypeName(o) + "' object doesn't support item deletion");
        }

        static long SliceArg(Value v, string what)
        {
            if (v.IsInt || v.IsBool) return v.N;
            if (v.O is BigInteger b) return b.Sign < 0 ? long.MinValue / 2 : long.MaxValue / 2;
            if (v.O is PyInstance) return Builtins.IndexOf(v).N;
            throw Err.Type("slice indices must be integers or None or have an __index__ method");
        }

        internal static (long start, long stop, long step, long count) SliceIndices(PySlice s, long len)
        {
            long step = s.Step.IsNone || s.Step.IsUnbound ? 1 : SliceArg(s.Step, "step");
            if (step == 0) throw Err.Value("slice step cannot be zero");
            long start, stop;
            if (step > 0)
            {
                start = s.Start.IsNone || s.Start.IsUnbound ? 0 : SliceArg(s.Start, "start");
                stop = s.Stop.IsNone || s.Stop.IsUnbound ? len : SliceArg(s.Stop, "stop");
                if (start < 0) start = Math.Max(0, start + len); else start = Math.Min(start, len);
                if (stop < 0) stop = Math.Max(0, stop + len); else stop = Math.Min(stop, len);
                long n = stop > start ? (stop - start + step - 1) / step : 0;
                return (start, stop, step, n);
            }
            else
            {
                start = s.Start.IsNone || s.Start.IsUnbound ? len - 1 : SliceArg(s.Start, "start");
                stop = s.Stop.IsNone || s.Stop.IsUnbound ? -1 : SliceArg(s.Stop, "stop");
                if (start < 0) start = Math.Max(-1, start + len); else start = Math.Min(start, len - 1);
                if (!(s.Stop.IsNone || s.Stop.IsUnbound)) { if (stop < 0) stop = Math.Max(-1, stop + len); else stop = Math.Min(stop, len - 1); }
                long n = start > stop ? (start - stop - step - 1) / (-step) : 0;
                return (start, stop, step, n);
            }
        }

        static List<Value> SliceList(IList<Value> items, PySlice s)
        {
            var (start, _, step, n) = SliceIndices(s, items.Count);
            var r = new List<Value>((int)n);
            if (step == 1 && items is List<Value> lst) { r.AddRange(lst.GetRange((int)start, (int)n)); return r; }
            for (long i = 0, j = start; i < n; i++, j += step) r.Add(items[(int)j]);
            return r;
        }

        static string SliceStr(string s, PySlice sl)
        {
            var (start, _, step, n) = SliceIndices(sl, s.Length);
            if (step == 1) return s.Substring((int)start, (int)n);
            var sb = new StringBuilder((int)n);
            for (long i = 0, j = start; i < n; i++, j += step) sb.Append(s[(int)j]);
            return sb.ToString();
        }

        static void SetSlice(List<Value> items, PySlice s, Value v)
        {
            var src = ToList(v);
            var (start, _, step, n) = SliceIndices(s, items.Count);
            if (step == 1)
            {
                items.RemoveRange((int)start, (int)n);
                items.InsertRange((int)start, src);
                return;
            }
            if (src.Count != n) throw Err.Value("attempt to assign sequence of size " + src.Count + " to extended slice of size " + n);
            for (long i = 0, j = start; i < n; i++, j += step) items[(int)j] = src[(int)i];
        }

        // ------------------------------------------------------------ attributes
        public static Value GetAttr(Value o, string name)
        {
            if (TryGetAttr(o, name, out var v)) return v;
            if (o.O is PyInstance inst && inst.Class.Lookup("__getattr__", out _))
                return VM.Current.CallMethod(o, "__getattr__", new[] { Value.Str(name) });
            if (o.O is PyClass pc) throw new ScriptException(Builtins.AttributeError, "type object '" + pc.Name + "' has no attribute '" + name + "'");
            if (o.O is PyModule m) throw new ScriptException(Builtins.AttributeError, "module '" + m.Name + "' has no attribute '" + name + "'");
            throw Err.Attr(o, name);
        }

        public static bool TryGetAttr(Value o, string name, out Value v)
        {
            switch (o.O)
            {
                case PyInstance inst:
                    {
                        if (inst.Attrs.TryGetValue(name, out v))
                        {
                            return true;
                        }
                        if (inst.Class.Lookup(name, out var cv))
                        {
                            v = BindClassAttr(cv, o, inst.Class);
                            return true;
                        }
                        if (name == "__class__") { v = Value.Obj(inst.Class); return true; }
                        if (name == "__dict__") { v = Builtins.AttrsDict(inst.Attrs); return true; }
                        return false;
                    }
                case PyClass cls:
                    {
                        if (cls.Lookup(name, out var cv))
                        {
                            if (cv.O is PyClassMethod cm) { v = Value.Obj(new BoundMethod(o, cm.Fn)); return true; }
                            if (cv.O is PyStaticMethod sm) { v = sm.Fn; return true; }
                            v = cv;
                            return true;
                        }
                        if (name == "__name__") { v = Value.Str(cls.Name); return true; }
                        if (name == "__mro__") { v = Value.Obj(new PyTuple(Array.ConvertAll(cls.Mro, c => Value.Obj(c)))); return true; }
                        if (name == "__bases__") { v = Value.Obj(new PyTuple(Array.ConvertAll(cls.Bases, c => Value.Obj(c)))); return true; }
                        if (name == "__dict__") { v = Builtins.AttrsDict(cls.Dict); return true; }
                        v = default;
                        return false;
                    }
                case PySuper sup:
                    {
                        var start = sup.Self.O is PyInstance si ? si.Class : sup.Self.O as PyClass;
                        if (start != null && start.LookupAfter(sup.Start, name, out var cv))
                        {
                            v = BindClassAttr(cv, sup.Self, start);
                            return true;
                        }
                        v = default;
                        return false;
                    }
                case PyModule m:
                    return m.Dict.TryGetValue(name, out v);
                case PyFunction f:
                    if (name == "__name__") { v = Value.Str(f.Name); return true; }
                    if (f.Attrs != null && f.Attrs.TryGetValue(name, out v)) return true;
                    v = default;
                    return false;
                case PyProperty p:
                    return Builtins.TryPropertyAttr(o, p, name, out v);
            }
            if (o.IsUnbound) throw Err.Runtime("unbound value");
            if (Builtins.TryGetBuiltinAttr(o, name, out v)) return true;
            if (Interop.IsHost(o)) return Interop.TryGetMember(o.O, name, out v);
            v = default;
            return false;
        }

        internal static Value BindClassAttr(Value cv, Value self, PyClass cls)
        {
            switch (cv.O)
            {
                case PyFunction _: return Value.Obj(new BoundMethod(self, cv));
                case BuiltinFunction _: return Value.Obj(new BoundMethod(self, cv));
                case PyStaticMethod sm: return sm.Fn;
                case PyClassMethod cm: return Value.Obj(new BoundMethod(Value.Obj(cls), cm.Fn));
                case PyProperty p:
                    if (p.Get.IsNone || p.Get.IsUnbound) throw new ScriptException(Builtins.AttributeError, "property has no getter");
                    return VM.Current.CallValue(p.Get, new[] { self }, null);
                default: return cv;
            }
        }

        public static void SetAttr(Value o, string name, Value v)
        {
            switch (o.O)
            {
                case PyInstance inst:
                    if (inst.Class.Lookup(name, out var cv))
                    {
                        if (cv.O is PyProperty p)
                        {
                            if (p.Set.IsNone || p.Set.IsUnbound) throw new ScriptException(Builtins.AttributeError, "can't set attribute '" + name + "'");
                            VM.Current.CallValue(p.Set, new[] { o, v }, null);
                            return;
                        }
                    }
                    if (inst.Class.Lookup("__setattr__", out _))
                    {
                        VM.Current.CallMethod(o, "__setattr__", new[] { Value.Str(name), v });
                        return;
                    }
                    inst.Attrs[name] = v;
                    return;
                case PyClass cls:
                    if (cls.IsBuiltin) throw Err.Type("cannot set '" + name + "' attribute of immutable type '" + cls.Name + "'");
                    cls.Set(name, v);
                    return;
                case PyFunction f:
                    (f.Attrs ??= new Dictionary<string, Value>(StringComparer.Ordinal))[name] = v;
                    return;
            }
            if (Interop.IsHost(o)) { Interop.SetMember(o.O, name, v); return; }
            throw new ScriptException(Builtins.AttributeError, "'" + TypeName(o) + "' object has no attribute '" + name + "'");
        }

        public static void DelAttr(Value o, string name)
        {
            if (o.O is PyInstance inst)
            {
                if (!inst.Attrs.Remove(name)) throw Err.Attr(o, name);
                return;
            }
            if (o.O is PyClass cls && !cls.IsBuiltin)
            {
                if (!cls.Dict.Remove(name)) throw Err.Attr(o, name);
                System.Threading.Interlocked.Increment(ref PyClass.Stamp);
                return;
            }
            throw Err.Attr(o, name);
        }

        // ------------------------------------------------------------ string conversion
        [ThreadStatic] static int _reprDepth;

        public static string Repr(Value v)
        {
            object o = v.O;
            if (ReferenceEquals(o, Value.TInt)) return v.N.ToString(Inv);
            if (o is string s) return Format.StrRepr(s);
            if (ReferenceEquals(o, Value.TFloat)) return Format.FloatRepr(v.AsFloat);
            if (ReferenceEquals(o, Value.TBool)) return v.N != 0 ? "True" : "False";
            if (ReferenceEquals(o, Value.TNone)) return "None";
            if (o == null) return "<unbound>";
            if (_reprDepth > 200) return "...";
            _reprDepth++;
            try { return ReprSlow(v); }
            finally { _reprDepth--; }
        }

        static string ReprSlow(Value v)
        {
            switch (v.O)
            {
                case BigInteger b: return b.ToString(Inv);
                case PyList l:
                    {
                        var sb = new StringBuilder("[");
                        for (int i = 0; i < l.Items.Count; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append(ReferenceEquals(l.Items[i].O, l) ? "[...]" : Repr(l.Items[i]));
                        }
                        return sb.Append(']').ToString();
                    }
                case PyTuple t:
                    {
                        if (t.Items.Length == 1) return "(" + Repr(t.Items[0]) + ",)";
                        var sb = new StringBuilder("(");
                        for (int i = 0; i < t.Items.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(Repr(t.Items[i])); }
                        return sb.Append(')').ToString();
                    }
                case PyDict d:
                    {
                        var sb = new StringBuilder("{");
                        bool first = true;
                        foreach (var kv in d.Items())
                        {
                            if (!first) sb.Append(", ");
                            first = false;
                            sb.Append(Repr(kv.Key)).Append(": ").Append(ReferenceEquals(kv.Value.O, d) ? "{...}" : Repr(kv.Value));
                        }
                        return sb.Append('}').ToString();
                    }
                case PySet st:
                    {
                        if (st.Items.Count == 0) return st.Frozen ? "frozenset()" : "set()";
                        var sb = new StringBuilder(st.Frozen ? "frozenset({" : "{");
                        bool first = true;
                        foreach (var x in st.Items) { if (!first) sb.Append(", "); first = false; sb.Append(Repr(x)); }
                        return sb.Append(st.Frozen ? "})" : "}").ToString();
                    }
                case DictView dv:
                    {
                        var items = new List<Value>();
                        var it = new DictIter(dv.D, dv.Mode);
                        while (it.Next(VM.Current, out var x)) items.Add(x);
                        return TypeName(v) + "(" + Repr(Value.Obj(new PyList(items))) + ")";
                    }
                case PyRange r:
                    return r.Step == 1 ? "range(" + r.Start + ", " + r.Stop + ")" : "range(" + r.Start + ", " + r.Stop + ", " + r.Step + ")";
                case PySlice sl:
                    return "slice(" + Repr(Norm(sl.Start)) + ", " + Repr(Norm(sl.Stop)) + ", " + Repr(Norm(sl.Step)) + ")";
                case PyFunction f: return "<function " + f.Name + " at " + Addr(f) + ">";
                case BoundMethod bm: return "<bound method " + FuncName(bm.Func) + " of " + Repr(bm.Self) + ">";
                case BuiltinFunction bf: return "<built-in function " + bf.Name + ">";
                case BuiltinMethod bm: return "<built-in method " + bm.Name + " of " + TypeName(bm.Self) + " object>";
                case PyClass c: return "<class '" + c.Name + "'>";
                case PyType pt: return "<class '" + pt.Name + "'>";
                case PyModule m: return "<module '" + m.Name + "' (built-in)>";
                case PyGenerator g: return "<generator object " + g.Name + " at " + Addr(g) + ">";
                case PyInstance inst:
                    {
                        if (inst.Class.Lookup("__repr__", out _))
                            return Str(VM.Current.CallMethod(v, "__repr__", ReadOnlySpan<Value>.Empty));
                        return "<" + inst.Class.Name + " object at " + Addr(inst) + ">";
                    }
                case Tag t: return t.Name;
                default:
                    if (ReferenceEquals(v.O, Builtins.NotImplementedObj)) return "NotImplemented";
                    return Interop.HostToString(v.O);
            }
        }

        static Value Norm(Value v) => v.IsUnbound ? Value.None : v;
        static string FuncName(Value f) => f.O is PyFunction pf ? pf.Name : f.O is BuiltinFunction bf ? bf.Name : "?";
        static string Addr(object o) => "0x" + RuntimeHelpers.GetHashCode(o).ToString("x8");

        public static string Str(Value v)
        {
            object o = v.O;
            if (o is string s) return s;
            if (ReferenceEquals(o, Value.TInt)) return v.N.ToString(Inv);
            if (o is PyInstance inst)
            {
                if (inst.Class.Lookup("__str__", out _))
                {
                    var r = VM.Current.CallMethod(v, "__str__", ReadOnlySpan<Value>.Empty);
                    if (!(r.O is string)) throw Err.Type("__str__ returned non-string (type " + TypeName(r) + ")");
                    return (string)r.O;
                }
            }
            if (o != null && !(o is Tag) && Interop.IsHostObject(o)) return Interop.HostToString(o);
            return Repr(v);
        }
    }
}
