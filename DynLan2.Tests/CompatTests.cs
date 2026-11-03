using System;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace DynLan2.Tests
{
    /// <summary>
    /// Hand-written replacements used by the netstandard2.1 build. Meaningful when run with
    /// -p:EngineTarget=netstandard2.1 (the engine's Compat then differs from the .NET 8 BCL used by this test).
    /// </summary>
    public class CompatTests
    {
        readonly ITestOutputHelper _out;
        public CompatTests(ITestOutputHelper output) { _out = output; }

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
        public void Log2IsExactForEveryPowerOfTwo()
        {
            for (int k = -1074; k <= 1023; k++)
            {
                double x = Math.Pow(2, k);
                Assert.Equal((double)k, Compat.Log2(x));
            }
            Assert.Equal(double.NegativeInfinity, Compat.Log2(0));
            Assert.Equal(double.PositiveInfinity, Compat.Log2(double.PositiveInfinity));
            Assert.True(double.IsNaN(Compat.Log2(-1)));
            Assert.True(double.IsNaN(Compat.Log2(double.NaN)));
        }

        [Fact]
        public void Log2WithinOneUlpOfBcl()
        {
            var rnd = new Random(7);
            int differ = 0;
            for (int i = 0; i < 1_000_000; i++)
            {
                double x = Math.Exp((rnd.NextDouble() - 0.5) * 1400);
                if (i % 3 == 0) x = Math.Floor(rnd.NextDouble() * 1e6) + 1; // integers, the common case
                double a = Compat.Log2(x), b = Math.Log2(x);
                if (a == b) continue;
                differ++;
                long ulps = Math.Abs(BitConverter.DoubleToInt64Bits(a) - BitConverter.DoubleToInt64Bits(b));
                Assert.True(ulps <= 1, $"log2({x:R}): {a:R} vs {b:R} ({ulps} ulp)");
            }
            _out.WriteLine($"{differ} of 1,000,000 differ by 1 ulp from Math.Log2");
        }

        [Fact]
        public void CopySignMatchesBcl()
        {
            double[] xs = { 0.0, -0.0, 1.5, -1.5, double.PositiveInfinity, double.NegativeInfinity, double.NaN, -double.NaN, double.Epsilon };
            foreach (var x in xs)
                foreach (var y in xs)
                {
                    double a = Compat.CopySign(x, y), b = Math.CopySign(x, y);
                    if (double.IsNaN(b)) Assert.True(double.IsNaN(a));
                    else Assert.Equal(BitConverter.DoubleToInt64Bits(b), BitConverter.DoubleToInt64Bits(a));
                }
        }

        [Fact]
        public void BitLengthMatchesBigIntegerOnRandomValues()
        {
            var rnd = new Random(3);
            for (int i = 0; i < 100_000; i++)
            {
                var bytes = new byte[rnd.Next(1, 40)];
                rnd.NextBytes(bytes);
                var v = new BigInteger(bytes);
                long expected = (long)BigInteger.Abs(v).GetBitLength();
                Assert.Equal(expected, Compat.GetBitLength(v));
            }
        }
    }
}
