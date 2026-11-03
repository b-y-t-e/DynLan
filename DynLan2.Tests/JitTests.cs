using System.IO;
using System.Linq;
using Xunit;

namespace DynLan2.Tests
{
    public class JitTests
    {
        static ScriptContext RunJit(string src)
        {
            var e = new ScriptEngine(new EngineOptions { Jit = JitMode.Eager, JitThreshold = 0, CacheScripts = false, Output = TextWriter.Null });
            var ctx = e.CreateContext(H.Dedent(src));
            while (ctx.Run() == ExecutionStatus.Suspended) { }
            return ctx;
        }

        static void AssertAllJitted(ScriptContext ctx, params string[] names)
        {
            foreach (var n in names)
            {
                var fc = ctx.Script.AllCodes.First(c => c.Name == n);
                Assert.True(fc.JitState == 2, n + " not compiled: state " + fc.JitState + " " + Jit.LastError);
            }
        }

        [Fact]
        public void CompilesTypicalFunctions()
        {
            var ctx = RunJit(@"
                class P:
                    def __init__(self, x):
                        self.x = x
                    def step(self, d):
                        self.x += d
                        return self.x
                def fib(n):
                    return n if n < 2 else fib(n - 1) + fib(n - 2)
                def loop(n: int) -> int:
                    s: int = 0
                    for i in range(n):
                        s += i * 2 % 7
                    return s
                def mixed(xs):
                    out = []
                    for k, v in enumerate(xs):
                        out.append(f'{k}:{v * 1.5:.1f}')
                    return ','.join(out)
                p = P(1)
                r = [fib(15), loop(1000), mixed([1, 2, 3]), p.step(4)]
                ");
            AssertAllJitted(ctx, "fib", "loop", "mixed", "step", "__init__");
            Assert.Equal("[610, 2998, '0:1.5,1:3.0,2:4.5', 5]", ctx.Eval("repr(r)"));
        }

        [Fact]
        public void TypedIntOverflowRaisesOverflowError()
        {
            var ctx = RunJit(@"
                def f(n: int) -> int:
                    x: int = 1
                    for i in range(n):
                        x = x * 10
                    return x
                ok = f(18)
                try:
                    f(30)
                    msg = 'no'
                except OverflowError:
                    msg = 'overflow'
                def g(n):
                    x = 1
                    for i in range(n):
                        x = x * 10
                    return x
                big = g(30)
                ");
            Assert.Equal(1000000000000000000L, ctx["ok"]);
            Assert.Equal("overflow", ctx["msg"]);
            Assert.Equal(System.Numerics.BigInteger.Pow(10, 30), ctx["big"]);
        }

        [Fact]
        public void ShadowedRangeDeoptimizes()
        {
            var ctx = RunJit(@"
                def count(n):
                    t = 0
                    for i in range(n):
                        t += i
                    return t
                a = count(10)
                def range(n):
                    return [100, 200]
                b = count(3)
                ");
            Assert.Equal(45L, ctx["a"]);
            Assert.Equal(300L, ctx["b"]);
        }

        [Fact]
        public void ExceptionTracebackFromCompiledCode()
        {
            var e = new ScriptEngine(new EngineOptions { Jit = JitMode.Eager, JitThreshold = 0 });
            var ctx = e.CreateContext("def f(x):\n    y = x + 1\n    return y / 0\n\ndef g():\n    return f(1)\n\ng()\n");
            var ex = Assert.Throws<ScriptException>(() => ctx.Run());
            Assert.Equal("ZeroDivisionError", ex.PythonType);
            Assert.Equal(3, ex.Line);
            Assert.Contains("in g", ex.ScriptTraceback);
        }

        [Fact]
        public void DeepRecursionFallsBackToHeapFrames()
        {
            var ctx = RunJit(@"
                def depth(n):
                    return 0 if n == 0 else 1 + depth(n - 1)
                d = depth(5000)
                ");
            Assert.Equal(5000L, ctx["d"]);
        }
    }
}
