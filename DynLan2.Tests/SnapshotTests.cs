using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace DynLan2.Tests
{
    public class SnapshotTests
    {
        const string Torture = @"
import math, random
random.seed(7)

class Account:
    count = 0
    def __init__(self, owner, balance=0):
        self.owner = owner
        self.balance = balance
        self.history = []
        Account.count += 1
    def deposit(self, amount):
        self.balance += amount
        self.history.append(('dep', amount))
        return self.balance
    def __repr__(self):
        return f'Account({self.owner}, {self.balance})'

def counter():
    n = 0
    def inc(step=1):
        nonlocal n
        n += step
        return n
    return inc

def squares(limit):
    for i in range(limit):
        yield i * i

def fib(n):
    return n if n < 2 else fib(n - 1) + fib(n - 2)

acc = Account('ann', 10)
inc = counter()
gen = squares(50)
data = {'xs': [], 'pairs': {}, 'seen': set()}
log = []
total = 0
for i in range(40):
    total += next(gen)
    acc.deposit(i % 7)
    inc(2)
    data['xs'].append(i * 1.5)
    data['pairs'][str(i)] = (i, i * i)
    data['seen'].add(i % 5)
    if i % 10 == 0:
        log.append(f'{i}: {fib(i // 2)} {random.randint(1, 100)}')
words = sorted('the quick brown fox jumps over the lazy dog'.split(), key=lambda w: (len(w), w))
it = iter(words)
first_two = [next(it), next(it)]
rest = [w.upper() for w in it]
try:
    acc.missing
except AttributeError as e:
    log.append('attr error')
result = (total, acc.balance, len(acc.history), inc(0), sum(data['xs']), sorted(data['seen']),
          data['pairs']['39'], log, first_two, rest, Account.count, math.floor(math.sqrt(total)), repr(acc))
print(result)
";

        static string RunPlain(string src)
        {
            var sw = new StringWriter { NewLine = "\n" };
            var e = new ScriptEngine(new EngineOptions { Jit = JitMode.Off, Output = sw });
            var ctx = e.CreateContext(src);
            Assert.Equal(ExecutionStatus.Completed, ctx.Run());
            return sw.ToString();
        }

        [Theory]
        [InlineData(1, "interp")]
        [InlineData(3, "interp")]
        [InlineData(17, "interp")]
        [InlineData(1, "jit")]
        [InlineData(5, "jit")]
        public void SuspendSaveRestoreAtEverySafePoint(int stepLimit, string mode)
        {
            string expected = RunPlain(Torture);
            var output = new StringWriter { NewLine = "\n" };
            var engine = H.Engine(mode);
            engine.Options.Output = output;
            var ctx = engine.CreateContext(Torture);
            int suspensions = 0;
            while (ctx.Run(stepLimit) == ExecutionStatus.Suspended)
            {
                suspensions++;
                byte[] state = ctx.SaveState();
                // brand-new engine each time: nothing shared except the bytes
                var fresh = H.Engine(mode);
                fresh.Options.Output = output;
                ctx = fresh.Restore(state);
            }
            Assert.True(suspensions >= 3, "expected several suspensions, got " + suspensions);
            Assert.Equal(expected, output.ToString());
        }

        [Fact]
        public void CheckpointBuiltinSuspendsAndStateMovesToNewEngine()
        {
            const string src = @"
results = []
def work(n):
    acc = 0
    for i in range(n):
        acc += i
        if i % 100 == 99:
            checkpoint()
    return acc
for k in range(3):
    results.append(work(300))
done = True
";
            var e1 = new ScriptEngine(new EngineOptions { Jit = JitMode.Off });
            var ctx = e1.CreateContext(src);
            int n = 0;
            byte[] state = null;
            while (ctx.Run() == ExecutionStatus.Suspended)
            {
                n++;
                state = ctx.SaveState();
                ctx = new ScriptEngine().Restore(state);
            }
            Assert.Equal(9, n);
            Assert.Equal(true, ctx["done"]);
            Assert.Equal(new List<object> { 44850L, 44850L, 44850L }, ctx["results"]);
        }

        [Fact]
        public void SaveWithoutSourceRequiresSourceOnRestore()
        {
            const string src = "x = 0\nwhile x < 1000:\n    x += 1\n    if x == 500:\n        checkpoint()\n";
            var ctx = new ScriptEngine().CreateContext(src);
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run());
            var state = ctx.SaveState(includeSource: false);
            Assert.Throws<SnapshotException>(() => new ScriptEngine().Restore(state));
            Assert.Throws<SnapshotException>(() => new ScriptEngine().Restore(state, src + "\n# changed"));
            var restored = new ScriptEngine().Restore(state, src);
            Assert.Equal(500L, restored["x"]);
            Assert.Equal(ExecutionStatus.Completed, restored.Run());
            Assert.Equal(1000L, restored["x"]);
        }

        public class Service
        {
            public string Name { get; set; }
            public int Calls;
            public string Hello(string who) { Calls++; return Name + " greets " + who; }
        }

        [Fact]
        public void HostObjectsAreSavedByNameAndRebound()
        {
            const string src = @"
out = []
for i in range(4):
    out.append(svc.Hello(str(i)))
    checkpoint()
";
            var svc1 = new Service { Name = "A" };
            var ctx = new ScriptEngine().CreateContext(src);
            ctx.BindHost("svc", svc1);
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run());
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run());
            var state = ctx.SaveState();

            var svc2 = new Service { Name = "B" };
            var restored = new ScriptEngine().Restore(state, new Dictionary<string, object> { ["svc"] = svc2 });
            while (restored.Run() == ExecutionStatus.Suspended) { }
            Assert.Equal(new List<object> { "A greets 0", "A greets 1", "B greets 2", "B greets 3" }, restored["out"]);
            Assert.Equal(2, svc2.Calls);
        }

        [Fact]
        public void UnboundHostObjectCannotBeSaved()
        {
            var ctx = new ScriptEngine().CreateContext("checkpoint()\n");
            ctx["svc"] = new Service();
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run());
            Assert.Throws<SnapshotException>(() => ctx.SaveState());
        }

        [Fact]
        public void RequestSuspendFromAnotherThread()
        {
            const string src = "i = 0\nwhile True:\n    i += 1\n";
            var ctx = new ScriptEngine().CreateContext(src);
            var t = new Thread(() => { Thread.Sleep(100); ctx.RequestSuspend(); });
            t.Start();
            Assert.Equal(ExecutionStatus.Suspended, ctx.Run());
            t.Join();
            long i1 = (long)ctx["i"];
            Assert.True(i1 > 0);
            var restored = new ScriptEngine().Restore(ctx.SaveState());
            Assert.Equal(i1, restored["i"]);
            Assert.Equal(ExecutionStatus.Suspended, restored.Run(stepLimit: 1000));
            Assert.Equal(i1 + 1000, restored["i"]);
        }

        [Fact]
        public void GeneratorSuspendedInsideForLoopSurvivesRestore()
        {
            const string src = @"
def numbers():
    for i in range(10):
        checkpoint()
        yield i
total = 0
for n in numbers():
    total += n
";
            var ctx = new ScriptEngine().CreateContext(src);
            int k = 0;
            while (ctx.Run() == ExecutionStatus.Suspended)
            {
                k++;
                ctx = new ScriptEngine().Restore(ctx.SaveState());
            }
            Assert.Equal(10, k);
            Assert.Equal(45L, ctx["total"]);
        }
    }
}
