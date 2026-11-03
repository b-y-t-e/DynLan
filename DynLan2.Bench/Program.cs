using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DynLan2;

namespace DynLan2.Bench
{
    /// <summary>
    /// Benchmarks DynLan2 (interpreter / JIT) against C#, CPython (if installed) and DynLan 1.
    /// Usage:
    ///   DynLan2.Bench                 run all benchmarks
    ///   DynLan2.Bench save &lt;file&gt;     run a long job, suspend half-way, write state to file
    ///   DynLan2.Bench resume &lt;file&gt;   resume the job from file (e.g. on another machine)
    /// </summary>
    static class Program
    {
        static readonly (string Name, string Script, Func<long> CSharp)[] Cases =
        {
            ("fib(30) recursive", @"
def fib(n):
    if n < 2:
        return n
    return fib(n - 1) + fib(n - 2)
result = fib(30)
", () => Fib(30)),

            ("loop sum 20M (function)", @"
def run(n):
    s = 0
    for i in range(n):
        s += i
    return s
result = run(20000000)
", () => { long s = 0; for (long i = 0; i < 20000000; i++) s += i; return s; }),

            ("typed loop 20M (x: int)", @"
def run(n: int) -> int:
    s: int = 0
    i: int = 0
    while i < n:
        s += i * 3 % 7
        i += 1
    return s
result = run(20000000)
", () => { long s = 0; for (long i = 0; i < 20000000; i++) s += i * 3 % 7; return s; }),

            ("float math 5M", @"
def run(n):
    x = 0.0
    for i in range(n):
        x += (i * 0.5) / (i + 1.0)
    return x
result = int(run(5000000))
", () => { double x = 0; for (long i = 0; i < 5000000; i++) x += (i * 0.5) / (i + 1.0); return (long)x; }),

            ("nested loops / lists (matmul 120)", @"
def matmul(n):
    a = [[i * j % 7 for j in range(n)] for i in range(n)]
    b = [[(i + j) % 5 for j in range(n)] for i in range(n)]
    c = [[0] * n for _ in range(n)]
    for i in range(n):
        ai = a[i]
        ci = c[i]
        for k in range(n):
            aik = ai[k]
            bk = b[k]
            for j in range(n):
                ci[j] += aik * bk[j]
    return sum(sum(r) for r in c)
result = matmul(120)
", () => MatMul(120)),

            ("objects + methods 2M", @"
class P:
    def __init__(self, x):
        self.x = x
    def step(self, d):
        self.x += d
        return self.x
def run(n):
    p = P(0)
    t = 0
    for i in range(n):
        t += p.step(i & 3)
    return t
result = run(2000000)
", () => { long x = 0, t = 0; for (long i = 0; i < 2000000; i++) { x += i & 3; t += x; } return t; }),

            ("dict + strings 300k", @"
def run(n):
    d = {}
    for i in range(n):
        k = 'k' + str(i % 1000)
        d[k] = d.get(k, 0) + i
    return len(d) + d['k7']
result = run(300000)
", () => { var d = new Dictionary<string, long>(); for (long i = 0; i < 300000; i++) { var k = "k" + (i % 1000); d.TryGetValue(k, out var v); d[k] = v + i; } return d.Count + d["k7"]; }),

            ("top-level loop 5M (globals)", @"
s = 0
for i in range(5000000):
    s += i
result = s
", () => { long s = 0; for (long i = 0; i < 5000000; i++) s += i; return s; }),
        };

        static long Fib(long n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);

        static long MatMul(int n)
        {
            var a = new long[n, n]; var b = new long[n, n]; var c = new long[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) { a[i, j] = i * j % 7; b[i, j] = (i + j) % 5; }
            for (int i = 0; i < n; i++) for (int k = 0; k < n; k++) for (int j = 0; j < n; j++) c[i, j] += a[i, k] * b[k, j];
            long s = 0; foreach (var x in c) s += x; return s;
        }

        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "save") return Save(args[1]);
            if (args.Length == 2 && args[0] == "resume") return Resume(args[1]);
            if (args.Length == 1 && args[0] == "parsebench") { ParseBench(); return 0; }
            bool quick = args.Contains("--quick");
            string python = FindPython();

            Console.WriteLine($"{"benchmark",-36} {"C#",9} {"DL2 interp",11} {"JIT cold",9} {"JIT warm",9} {"CPython",9}   warm vs C#  warm vs Py");
            foreach (var (name, script, cs) in Cases)
            {
                var sw = Stopwatch.StartNew();
                long expected = cs();
                double tCs = sw.Elapsed.TotalMilliseconds;
                sw.Restart(); expected = cs(); tCs = Math.Min(tCs, sw.Elapsed.TotalMilliseconds);

                double tInterp = quick ? double.NaN : Time(script, JitMode.Off, expected).cold;
                var (tCold, tWarm) = Time(script, JitMode.Eager, expected);
                double tPy = python != null ? TimePython(python, script) : double.NaN;
                Console.WriteLine($"{name,-36} {Ms(tCs),9} {Ms(tInterp),11} {Ms(tCold),9} {Ms(tWarm),9} {Ms(tPy),9}   {Ratio(tWarm, tCs),9} {Ratio(tWarm, tPy),10}");
            }

            Console.WriteLine();
            OldDynLanComparison();
            Console.WriteLine();
            LoadTime();
            return 0;
        }

        static string Ms(double t) => double.IsNaN(t) ? "-" : t < 10 ? t.ToString("0.00") + "ms" : t.ToString("0") + "ms";
        static string Ratio(double a, double b) => double.IsNaN(a) || double.IsNaN(b) || b <= 0 ? "-" : (a / b).ToString("0.0") + "x";

        /// <summary>cold = fresh engine (parse + bytecode + JIT compile included); warm = same script run again (code reused).</summary>
        static (double cold, double warm) Time(string script, JitMode mode, long expected)
        {
            var engine = new ScriptEngine(new EngineOptions { Jit = mode, CacheScripts = true });
            double cold = 0, warm = double.MaxValue;
            for (int rep = 0; rep < 3; rep++)
            {
                var sw = Stopwatch.StartNew();
                var ctx = engine.Execute(script);
                double t = sw.Elapsed.TotalMilliseconds;
                var r = Convert.ToInt64(ctx["result"]);
                if (r != expected) throw new Exception($"wrong result {r} != {expected} ({mode})");
                if (rep == 0) cold = t; else warm = Math.Min(warm, t);
            }
            return (cold, warm);
        }

        static string FindPython()
        {
            foreach (var exe in new[] { "python", "python3" })
            {
                try
                {
                    var p = Process.Start(new ProcessStartInfo(exe, "--version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
                    p.WaitForExit(5000);
                    if (p.ExitCode == 0) return exe;
                }
                catch { }
            }
            return null;
        }

        static double TimePython(string python, string script)
        {
            string file = Path.Combine(Path.GetTempPath(), "dl2bench_" + Guid.NewGuid().ToString("N") + ".py");
            File.WriteAllText(file, "import time\n_t0 = time.perf_counter()\n" + script + "\nprint((time.perf_counter() - _t0) * 1000)\n");
            try
            {
                var p = Process.Start(new ProcessStartInfo(python, "-I \"" + file + "\"") { RedirectStandardOutput = true, UseShellExecute = false });
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return double.Parse(outp.Trim().Split('\n').Last(), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return double.NaN; }
            finally { File.Delete(file); }
        }

        static void OldDynLanComparison()
        {
            const int n = 200000;
            string oldSrc = "i = 0\ns = 0\nwhile i < " + n + " {\n s = s + i\n i = i + 1\n}\nreturn s";
            var sw = Stopwatch.StartNew();
            var r = new DynLan.Compiler().Compile(oldSrc).Eval();
            double tOld = sw.Elapsed.TotalMilliseconds;
            string newSrc = "i = 0\ns = 0\nwhile i < " + n + ":\n    s = s + i\n    i = i + 1\nresult = s\n";
            double tNewI = Time(newSrc, JitMode.Off, (long)n * (n - 1) / 2).cold;
            double tNewJ = Time(newSrc, JitMode.Eager, (long)n * (n - 1) / 2).cold;
            Console.WriteLine($"DynLan 1 vs 2 — while loop {n:N0} iterations (result {r}):");
            Console.WriteLine($"  DynLan 1: {Ms(tOld)}   DynLan 2 interp: {Ms(tNewI)} ({tOld / tNewI:0}x faster)   DynLan 2 JIT: {Ms(tNewJ)} ({tOld / tNewJ:0}x faster)");
        }

        static void LoadTime()
        {
            // a very long script: 20 000 functions, ~200 000 lines
            var sb = new StringBuilder();
            for (int i = 0; i < 20000; i++)
            {
                sb.Append("def f").Append(i).Append("(a, b=2):\n");
                sb.Append("    x = a * b + ").Append(i).Append('\n');
                sb.Append("    if x > 100:\n        x -= 1\n    else:\n        x += 1\n");
                sb.Append("    items = [x, x + 1, x + 2]\n");
                sb.Append("    total = sum(v * 2 for v in items)\n");
                sb.Append("    return {'x': x, 'total': total, 'name': f'f").Append(i).Append("'}\n\n");
            }
            sb.Append("result = f19999(3)['total']\n");
            string src = sb.ToString();
            int lines = src.Count(c => c == '\n');
            var engine = new ScriptEngine(new EngineOptions { CacheScripts = false });
            string warm = src.Substring(0, 50000);
            warm = warm.Substring(0, warm.LastIndexOf("def f"));
            for (int w = 0; w < 2; w++) engine.Compile(warm); // warm up the CLR JIT
            var sw = Stopwatch.StartNew();
            var toks = Lexer.Tokenize(src);
            double tLex = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            var ast = Parser.ParseModule(src, "x");
            double tLexParse = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"  lexer: {Ms(tLex)} ({toks.Count:N0} tokens), lexer+parser: {Ms(tLexParse)}");
            sw.Restart();
            var script = engine.Compile(src);
            double tParse = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            var ctx = script.CreateContext(engine);
            ctx.Run();
            double tRun = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            script.CompileAll();
            double tAll = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Load time — {lines:N0} lines, {src.Length / 1024:N0} KB, 20 000 functions:");
            Console.WriteLine($"  parse + scope analysis: {Ms(tParse)} ({lines / tParse * 1000:N0} lines/s)");
            Console.WriteLine($"  run module (lazy: only called functions compiled): {Ms(tRun)}");
            Console.WriteLine($"  (eager bytecode for all 20 000 functions would add: {Ms(tAll)})");
        }

        static void ParseBench()
        {
            string Gen(Func<int, string> body)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < 20000; i++) sb.Append(body(i));
                return sb.ToString();
            }
            var variants = new (string, string)[]
            {
                ("simple assigns", Gen(i => $"def f{i}(a, b=2):\n    x = a * b + {i}\n    y = x - 1\n    return y\n\n")),
                ("if/else", Gen(i => $"def f{i}(a):\n    if a > 100:\n        a -= 1\n    else:\n        a += 1\n    return a\n\n")),
                ("list+dict", Gen(i => $"def f{i}(x):\n    items = [x, x + 1, x + 2]\n    return {{'x': x, 'n': items}}\n\n")),
                ("genexp", Gen(i => $"def f{i}(items):\n    return sum(v * 2 for v in items)\n\n")),
                ("fstring", Gen(i => $"def f{i}():\n    return f'f{i}'\n\n")),
            };
            foreach (var (name, src) in variants)
            {
                Parser.ParseModule(src, "w");
                var sw = Stopwatch.StartNew();
                for (int k = 0; k < 3; k++) Parser.ParseModule(src, "x");
                double t = sw.Elapsed.TotalMilliseconds / 3;
                int lines = src.Count(c => c == '\n');
                var sw2 = Stopwatch.StartNew();
                Lexer.Tokenize(src);
                Console.WriteLine($"{name,-16} {lines,8:N0} lines  parse {t,7:0.0}ms  lex {sw2.Elapsed.TotalMilliseconds,6:0.0}ms  {lines / t * 1000,12:N0} lines/s");
            }
        }

        // ---------------------------------------------------------------- cross-process resume demo
        const string Job = @"
def job():
    primes = []
    n = 2
    while len(primes) < 3000:
        if all(n % p for p in primes if p * p <= n):
            primes.append(n)
        if len(primes) % 500 == 0 and primes[-1] == n:
            print('found', len(primes), 'primes, last =', n)
            checkpoint()
        n += 1
    return primes
result = job()
print('done, sum =', sum(result))
";

        static int Save(string file)
        {
            var ctx = new ScriptEngine().CreateContext(Job, "job.py");
            ctx.Run();
            ctx.Run();
            File.WriteAllBytes(file, ctx.SaveState());
            Console.WriteLine($"suspended; state saved to {file} ({new FileInfo(file).Length:N0} bytes) — resume with: DynLan2.Bench resume {file}");
            return 0;
        }

        static int Resume(string file)
        {
            var ctx = new ScriptEngine().Restore(File.ReadAllBytes(file));
            Console.WriteLine("resumed in process " + Environment.ProcessId);
            while (ctx.Run() == ExecutionStatus.Suspended) { }
            return 0;
        }
    }
}
