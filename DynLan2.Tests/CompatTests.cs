using System;
using System.Numerics;
using Xunit;

namespace DynLan2.Tests
{
    /// <summary>Hand-written replacements used by the netstandard2.1 build (run with -p:EngineTarget=netstandard2.1).</summary>
    public class CompatTests
    {
        static readonly long[] Edge =
        {
            0, 1, -1, 2, -2, 3, int.MaxValue, int.MinValue, (long)int.MaxValue + 1, (long)int.MinValue - 1,
            uint.MaxValue, 3037000499, 3037000500, -3037000500, long.MaxValue, long.MinValue, long.MaxValue - 1, long.MinValue + 1,
            1L << 32, -(1L << 32), 1L << 62, -(1L << 62), 0x7FFFFFFF_FFFF, -0x7FFFFFFF_FFFF
        };

        static void Check(long a, long b)
        {
            long hi = Compat.BigMul(a, b, out long lo);
            var exact = (BigInteger)a * b;
            var got = ((BigInteger)hi << 64) + (ulong)lo;
            Assert.True(exact == got, $"{a} * {b}: expected {exact}, got hi={hi} lo={lo}");
        }

        [Fact]
        public void BigMulMatchesBigInteger()
        {
            foreach (var a in Edge)
                foreach (var b in Edge)
                    Check(a, b);
            var rnd = new Random(12345);
            var buf = new byte[16];
            for (int i = 0; i < 200_000; i++)
            {
                rnd.NextBytes(buf);
                long a = BitConverter.ToInt64(buf, 0) >> rnd.Next(64);
                long b = BitConverter.ToInt64(buf, 8) >> rnd.Next(64);
                Check(a, b);
            }
        }

        [Fact]
        public void BitLengthLog2CopySign()
        {
            foreach (var v in new BigInteger[] { 0, 1, -1, 255, 256, -256, BigInteger.Pow(2, 100), -BigInteger.Pow(2, 100) + 1, long.MaxValue })
            {
                long expected = 0;
                for (var x = BigInteger.Abs(v); !x.IsZero; x >>= 1) expected++;
                Assert.Equal(expected, Compat.GetBitLength(v));
            }
            Assert.Equal(10.0, Compat.Log2(1024), 12);
            Assert.Equal(-3.0, Compat.CopySign(3, -0.0));
            Assert.Equal(3.0, Compat.CopySign(-3, 2));
        }
    }
}
