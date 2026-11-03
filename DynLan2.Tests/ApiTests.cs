using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DynLan2.Tests
{
    public class ApiTests
    {
        public class Customer
        {
            public string Name { get; set; }
            public decimal Balance { get; set; }
            public List<string> Tags { get; } = new List<string>();
            public int Score;
            public string Greet(string greeting) => greeting + ", " + Name;
            public string Greet(string greeting, int times) => string.Concat(Enumerable.Repeat(greeting, times)) + " " + Name;
            public static int Twice(int x) => x * 2;
            public int Apply(Func<int, int> f, int v) => f(v);
        }

        [Fact]
        public void HostObjectsPropertiesMethodsOverloadsDelegates()
        {
            var c = new Customer { Name = "Ann", Balance = 10.5m };
            var ctx = new ScriptEngine().CreateContext(@"
c.Score = 7
c.Tags.Add('vip')
c.Balance = c.Balance * 2
a = c.Greet('Hi')
b = c.Greet('Yo', 2)
t = Customer.Twice(21)
d = c.Apply(lambda x: x * 10, 4)
n = len(c.Tags)
first = c.Tags[0]
for tag in c.Tags:
    last = tag
");
            ctx["c"] = c;
            ctx["Customer"] = typeof(Customer);
            ctx.Run();
            Assert.Equal(7, c.Score);
            Assert.Equal(21m, c.Balance);
            Assert.Equal(new[] { "vip" }, c.Tags);
            Assert.Equal("Hi, Ann", ctx["a"]);
            Assert.Equal("YoYo Ann", ctx["b"]);
            Assert.Equal(42L, ctx["t"]);
            Assert.Equal(40L, ctx["d"]);
            Assert.Equal(1L, ctx["n"]);
            Assert.Equal("vip", ctx["first"]);
            Assert.Equal("vip", ctx["last"]);
        }

        [Theory]
        [InlineData("c.GetType()")]
        [InlineData("Customer.Assembly")]
        [InlineData("Customer.GetMethods()")]
        [InlineData("f.Method")]
        public void ReflectionIsNotReachableFromScripts(string expr)
        {
            var ctx = new ScriptEngine().CreateContext("");
            ctx["c"] = new Customer { Name = "x" };
            ctx["Customer"] = typeof(Customer);
            ctx["f"] = new Func<int, int>(x => x);
            var ex = Assert.Throws<ScriptException>(() => ctx.Eval(expr));
            Assert.Equal("AttributeError", ex.PythonType);
        }

        public class Leaky { public object GetTypeObject() => typeof(System.IO.File); }

        [Theory]
        [InlineData("t.Exists('x')")]
        [InlineData("t()")]
        public void TypeNotGivenByHostIsInert(string expr)
        {
            var ctx = new ScriptEngine().CreateContext("");
            ctx["leaky"] = new Leaky();
            ctx.Exec("t = leaky.GetTypeObject()");
            Assert.Throws<ScriptException>(() => ctx.Eval(expr));
        }

        [Fact]
        public void HostGivenTypeKeepsStaticAccessAfterRestore()
        {
            var ctx = new ScriptEngine().CreateContext("checkpoint()\nr = M.Max(3, 9)\n");
            ctx.BindHost("M", typeof(Math));
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run());
            var restored = new ScriptEngine().Restore(ctx.SaveState(), new Dictionary<string, object> { ["M"] = typeof(Math) });
            restored.Run();
            Assert.Equal(9L, restored["r"]);
        }

        [Fact]
        public void CannotSaveStateFromInsideRunningScript()
        {
            var ctx = new ScriptEngine().CreateContext("r = save()\n");
            Exception caught = null;
            ctx["save"] = new Func<int>(() =>
            {
                try { ctx.SaveState(); }
                catch (Exception e) { caught = e; }
                return 0;
            });
            ctx.Run();
            Assert.IsType<SnapshotException>(caught);
        }

        public class Svc { public int Ping() => 42; }

        [Fact]
        public void HostModuleSurvivesRestore()
        {
            var ctx = new ScriptEngine().CreateContext("checkpoint()\nimport svc\nr = svc.Ping()\n");
            ctx.AddModule("svc", new Svc());
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run());
            var restored = new ScriptEngine().Restore(ctx.SaveState(), new Dictionary<string, object> { ["module:svc"] = new Svc() });
            Assert.Equal(ExecutionStatus.Completed, restored.Run());
            Assert.Equal(42L, restored["r"]);
        }

        [Fact]
        public void DictionariesAndExpandoFromHost()
        {
            var data = new Dictionary<string, object> { ["x"] = 5, ["name"] = "box" };
            var ctx = new ScriptEngine().CreateContext("y = data['x'] * 2\ndata['z'] = 'new'\nname_len = len(data['name'])\n");
            ctx["data"] = data;
            ctx.Run();
            Assert.Equal(10L, ctx["y"]);
            Assert.Equal("new", data["z"]);
            Assert.Equal(3L, ctx["name_len"]);
        }

        [Fact]
        public void CallEvalExec()
        {
            var ctx = new ScriptEngine().CreateContext(@"
def area(w, h=2):
    return w * h
class Point:
    def __init__(self, x, y):
        self.x = x
        self.y = y
total = 0
");
            ctx.Run();
            Assert.Equal(12L, ctx.Call("area", 3, 4));
            Assert.Equal(6L, ctx.Call("area", 3));
            Assert.Equal(17L, ctx.Eval("area(5) + 7"));
            ctx.Exec("total = sum(area(i) for i in range(4))\np = Point(1, 2)");
            Assert.Equal(12L, ctx["total"]);
            Assert.Equal(3L, ctx.Eval("p.x + p.y"));
            Assert.Equal(new List<object> { 1L, "a", null }, ctx.Eval("[1, 'a', None]"));
            Assert.Equal(3.5, new ScriptEngine().Eval("7 / 2"));
            Assert.Equal("abcabc", new ScriptEngine().Eval("s * 2", new Dictionary<string, object> { ["s"] = "abc" }));
        }

        [Fact]
        public void PrintGoesToConfiguredOutput()
        {
            var sw = new StringWriter();
            var ctx = new ScriptEngine(new EngineOptions { Output = sw }).CreateContext("print('a', 1, sep='-', end='!')");
            ctx.Run();
            Assert.Equal("a-1!", sw.ToString());
        }

        [Theory]
        [InlineData("x = (1, 2", 1)]
        [InlineData("def f():\nreturn 1\n", 2)]
        [InlineData("if True:\n    x = 1\n   y = 2\n", 3)]
        [InlineData("x = 1\ny = 'unterminated\n", 2)]
        [InlineData("for i in range(3)\n    pass\n", 1)]
        [InlineData("x = 1 +\n", 1)]
        [InlineData("return 5\n", 1)]
        public void SyntaxErrorsReportLine(string src, int line)
        {
            var ex = Assert.Throws<ScriptSyntaxException>(() => new ScriptEngine().Compile(src));
            Assert.Equal(line, ex.Line);
            Assert.Equal("SyntaxError", ex.PythonType);
        }

        [Fact]
        public void RuntimeErrorHasPythonTraceback()
        {
            var ctx = new ScriptEngine(new EngineOptions { Jit = JitMode.Off }).CreateContext("def a(x):\n    return b(x)\n\ndef b(x):\n    return {}[x]\n\na('key')\n", "app.py");
            var ex = Assert.Throws<ScriptException>(() => ctx.Run());
            Assert.Equal("KeyError", ex.PythonType);
            Assert.Equal("KeyError: 'key'", ex.Message);
            Assert.Equal(5, ex.Line);
            Assert.Contains("File \"app.py\", line 7, in <module>", ex.ScriptTraceback);
            Assert.Contains("line 2, in a", ex.ScriptTraceback);
            Assert.Contains("line 5, in b", ex.ScriptTraceback);
        }

        [Fact]
        public void ScriptCacheSharesCompiledCodeBetweenContexts()
        {
            var e = new ScriptEngine();
            var s1 = e.Compile("x = 1");
            var s2 = e.Compile("x = 1");
            Assert.Same(s1, s2);
            var c1 = s1.CreateContext(e);
            var c2 = s1.CreateContext(e);
            c1.Run();
            c2["x"] = 5;
            Assert.Equal(1L, c1["x"]);
            Assert.Equal(5L, c2["x"]);
        }

        [Fact]
        public void LazyCompilationOnlyTouchesCalledFunctions()
        {
            var src = string.Concat(Enumerable.Range(0, 500).Select(i => $"def f{i}(x):\n    return x + {i}\n\n")) + "r = f7(1)\n";
            var e = new ScriptEngine();
            var script = e.Compile(src);
            var ctx = script.CreateContext(e);
            ctx.Run();
            Assert.Equal(8L, ctx["r"]);
            Assert.Equal(2, script.AllCodes.Count(c => c.Compiled)); // module + f7
        }

        [Fact]
        public void StepLimitIsExactWithJit()
        {
            var e = new ScriptEngine(new EngineOptions { Jit = JitMode.Eager, JitThreshold = 0 });
            var ctx = e.CreateContext("def run():\n    global i\n    while True:\n        i += 1\ni = 0\nrun()\n");
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run(stepLimit: 100));
            Assert.Equal(100L, ctx["i"]);
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run(stepLimit: 250));
            Assert.Equal(350L, ctx["i"]);
        }
    }
}
