using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DynLan2
{
    /// <summary>
    /// APIs missing from netstandard2.1. On .NET 5+ these forward to the BCL (no cost); on netstandard2.1 they
    /// are implemented by hand.
    /// </summary>
    internal static class Compat
    {
        /// <summary>Full signed 64×64→128 multiply; returns the high half, <paramref name="low"/> gets the low half.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BigMul(long a, long b, out long low)
        {
#if NET5_0_OR_GREATER
            return Math.BigMul(a, b, out low);
#else
            // fast path: both factors fit in 32 bits, the product cannot overflow 64 bits
            if (a == (int)a && b == (int)b)
            {
                low = a * b;
                return low >> 63;
            }
            return BigMulSlow(a, b, out low);
#endif
        }

#if !NET5_0_OR_GREATER
        static long BigMulSlow(long a, long b, out long low)
        {
            ulong ua = (ulong)a, ub = (ulong)b;
            ulong aL = ua & 0xFFFFFFFFUL, aH = ua >> 32, bL = ub & 0xFFFFFFFFUL, bH = ub >> 32;
            ulong t = aL * bL;
            ulong w0 = t & 0xFFFFFFFFUL;
            ulong k = t >> 32;
            t = aH * bL + k;
            ulong w1 = t & 0xFFFFFFFFUL;
            ulong w2 = t >> 32;
            t = aL * bH + w1;
            k = t >> 32;
            ulong hiU = aH * bH + w2 + k;
            low = (long)((t << 32) + w0);
            // unsigned → signed high half
            long hi = (long)hiU;
            if (a < 0) hi -= b;
            if (b < 0) hi -= a;
            return hi;
        }
#endif

        public static long GetBitLength(BigInteger v)
        {
#if NET5_0_OR_GREATER
            return (long)BigInteger.Abs(v).GetBitLength(); // magnitude bits, like Python int.bit_length
#else
            var a = BigInteger.Abs(v);
            if (a.IsZero) return 0;
            var bytes = a.ToByteArray(); // little-endian, may carry a trailing 0 sign byte
            int top = bytes.Length - 1;
            while (top > 0 && bytes[top] == 0) top--;
            int bits = 0;
            for (int x = bytes[top]; x != 0; x >>= 1) bits++;
            return top * 8L + bits;
#endif
        }

        public static double Log2(double x)
        {
#if NET5_0_OR_GREATER
            return Math.Log2(x);
#else
            // split into exponent and mantissa so that powers of two are exact (log2(2**k) == k, as in CPython)
            if (!(x > 0) || double.IsInfinity(x)) return Math.Log(x) / Ln2;
            int shift = 0;
            if (x < 2.2250738585072014E-308) { x *= 18014398509481984.0; shift = 54; } // subnormal: scale by 2**54 (exact)
            long bits = BitConverter.DoubleToInt64Bits(x);
            int exp = (int)((bits >> 52) & 0x7FF) - 1023 - shift;
            double m = BitConverter.Int64BitsToDouble((bits & 0xFFFFFFFFFFFFFL) | (1023L << 52)); // [1, 2)
            if (m > 1.4142135623730951) { m *= 0.5; exp++; } // keep the fraction small: m in (0.707, 1.414]
            return exp + Math.Log(m) / Ln2;
#endif
        }

#if !NET5_0_OR_GREATER
        const double Ln2 = 0.6931471805599453;
#endif

        public static double CopySign(double x, double y)
        {
#if NET5_0_OR_GREATER
            return Math.CopySign(x, y);
#else
            bool neg = y < 0 || (y == 0 && 1 / y < 0) || (double.IsNaN(y) && BitConverter.DoubleToInt64Bits(y) < 0);
            double ax = Math.Abs(x);
            return neg ? -ax : ax;
#endif
        }
    }

    /// <summary>Reference-identity comparer (System.Collections.Generic.ReferenceEqualityComparer is .NET 5+).</summary>
    internal sealed class RefComparer : IEqualityComparer<object>
    {
        public static readonly RefComparer Instance = new RefComparer();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
    }
}
