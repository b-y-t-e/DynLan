using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DynLan2
{
    internal sealed class Tag
    {
        public readonly string Name;
        public Tag(string name) { Name = name; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// A script value. 16 bytes, no allocation for int / float / bool / None.
    /// <c>O</c> is either a type tag (int, float, bool, None) or the referenced object
    /// (string, boxed BigInteger, list, dict, instance, host object ...).
    /// <c>default(Value)</c> means "unbound" (an unassigned local).
    /// </summary>
    public readonly struct Value
    {
        internal readonly object O;
        internal readonly long N;

        internal static readonly object TInt = new Tag("int");
        internal static readonly object TFloat = new Tag("float");
        internal static readonly object TBool = new Tag("bool");
        internal static readonly object TNone = new Tag("NoneType");

        public static readonly Value None = new Value(TNone, 0);
        public static readonly Value True = new Value(TBool, 1);
        public static readonly Value False = new Value(TBool, 0);
        internal static readonly Value Unbound = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Value(object o, long n) { O = o; N = n; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Int(long v) => new Value(TInt, v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Float(double d) => new Value(TFloat, BitConverter.DoubleToInt64Bits(d));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Bool(bool b) => b ? True : False;

        public static Value Str(string s) => s == null ? None : new Value(s, 0);

        public static Value BigInt(BigInteger b) =>
            (b >= long.MinValue && b <= long.MaxValue) ? Int((long)b) : new Value(b, 0);

        /// <summary>Wraps a runtime object (list, dict, function, host object...). Never use for primitives.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Value Obj(object o) => o == null ? None : new Value(o, 0);

        public bool IsInt { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ReferenceEquals(O, TInt); }
        public bool IsFloat { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ReferenceEquals(O, TFloat); }
        public bool IsBool { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ReferenceEquals(O, TBool); }
        public bool IsNone { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ReferenceEquals(O, TNone); }
        public bool IsUnbound { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => O == null; }
        public bool IsStr => O is string;
        public bool IsBigInt => O is BigInteger;
        /// <summary>int, bool or big int.</summary>
        internal bool IsIntegral => IsInt || IsBool || O is BigInteger;
        internal bool IsNumber => IsInt || IsFloat || IsBool || O is BigInteger;
        internal bool IsTag => O is Tag;

        public long AsInt { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => N; }
        public double AsFloat { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BitConverter.Int64BitsToDouble(N); }
        public bool AsBool => N != 0;
        public string AsStr => (string)O;
        internal BigInteger AsBig => O is BigInteger b ? b : new BigInteger(N);
        internal object Ref => O is Tag ? null : O;

        internal double ToDouble()
        {
            if (IsFloat) return AsFloat;
            if (IsInt || IsBool) return N;
            if (O is BigInteger b) return (double)b;
            throw Err.Type("must be real number, not " + Ops.TypeName(this));
        }

        public override string ToString() => Ops.Repr(this);

        public override bool Equals(object obj) => obj is Value v && ValueEq.Instance.Equals(this, v);
        public override int GetHashCode() => ValueEq.Instance.GetHashCode(this);
    }

    /// <summary>Python equality / hashing semantics for dict keys and sets.</summary>
    internal sealed class ValueEq : IEqualityComparer<Value>
    {
        public static readonly ValueEq Instance = new ValueEq();

        public bool Equals(Value a, Value b)
        {
            if (a.O is string sa) return b.O is string sb && string.Equals(sa, sb, StringComparison.Ordinal);
            if (ReferenceEquals(a.O, b.O) && a.N == b.N) return true;
            return Ops.EqValues(a, b);
        }

        public int GetHashCode(Value v)
        {
            object o = v.O;
            if (o is string s) return s.GetHashCode();
            if (ReferenceEquals(o, Value.TInt) || ReferenceEquals(o, Value.TBool)) return v.N.GetHashCode();
            if (ReferenceEquals(o, Value.TFloat))
            {
                double d = v.AsFloat;
                if (d == Math.Floor(d) && d >= long.MinValue && d <= long.MaxValue) return ((long)d).GetHashCode();
                return d.GetHashCode();
            }
            if (ReferenceEquals(o, Value.TNone)) return 0x5a5a;
            if (o is BigInteger b) return b.GetHashCode();
            if (o is PyTuple t)
            {
                int h = 17;
                foreach (var x in t.Items) h = h * 31 + GetHashCode(x);
                return h;
            }
            if (o is PyInstance inst) return Ops.InstanceHash(inst);
            if (o is PyList || o is PyDict || o is PySet) throw Err.Type("unhashable type: '" + Ops.TypeName(v) + "'");
            return RuntimeHelpers.GetHashCode(o) ^ (o.GetType().IsValueType ? o.GetHashCode() : 0);
        }
    }
}
