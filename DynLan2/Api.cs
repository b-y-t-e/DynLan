using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace DynLan2
{
    public enum JitMode
    {
        /// <summary>Interpreter only.</summary>
        Off,
        /// <summary>Hot functions / loops are compiled to IL on a background thread (default).</summary>
        Background,
        /// <summary>Hot code is compiled synchronously when it crosses the threshold (deterministic; good for tests).</summary>
        Eager
    }

    public enum ExecutionStatus { Completed, Suspended }

    public sealed class EngineOptions
    {
        public JitMode Jit { get; set; } = JitMode.Background;
        /// <summary>Calls + loop back-edges before a code unit is JIT-compiled.</summary>
        public int JitThreshold { get; set; } = 2000;
        /// <summary>Where print() writes. Defaults to Console.Out.</summary>
        public TextWriter Output { get; set; }
        /// <summary>Maximum script call depth (frames live on the heap, so this can be large).</summary>
        public int RecursionLimit { get; set; } = 10000;
        /// <summary>Reuse parsed scripts with identical source (parsing / bytecode / JIT are shared).</summary>
        public bool CacheScripts { get; set; } = true;
    }

    /// <summary>Entry point: compiles scripts, creates execution contexts and restores saved state.</summary>
    public sealed class ScriptEngine
    {
        public EngineOptions Options { get; }
        readonly ConcurrentDictionary<string, Script> _cache = new ConcurrentDictionary<string, Script>(StringComparer.Ordinal);

        public ScriptEngine(EngineOptions options = null)
        {
            Options = options ?? new EngineOptions();
        }

        /// <summary>Parses and analyzes a script. Bytecode is generated lazily, per function, on first use.</summary>
        public Script Compile(string source, string name = "<script>")
        {
            if (!Options.CacheScripts) return new Script(source, name);
            return _cache.GetOrAdd(name + "\0" + source, _ => new Script(source, name));
        }

        public ScriptContext CreateContext(string source, string name = "<script>") => new ScriptContext(this, Compile(source, name));

        /// <summary>Compiles and runs a script to completion; returns the context for inspecting globals.</summary>
        public ScriptContext Execute(string source, IDictionary<string, object> globals = null, string name = "<script>")
        {
            var ctx = CreateContext(source, name);
            if (globals != null) foreach (var kv in globals) ctx[kv.Key] = kv.Value;
            while (ctx.Run() == ExecutionStatus.Suspended) { }
            return ctx;
        }

        /// <summary>Evaluates a single expression in a fresh context.</summary>
        public object Eval(string expression, IDictionary<string, object> globals = null)
        {
            var ctx = new ScriptContext(this, Compile("", "<eval>"));
            if (globals != null) foreach (var kv in globals) ctx[kv.Key] = kv.Value;
            return ctx.Eval(expression);
        }

        /// <summary>Restores a context saved with <see cref="ScriptContext.SaveState"/> (possibly on another machine).</summary>
        public ScriptContext Restore(byte[] state, IDictionary<string, object> hostBindings = null) =>
            Snapshot.Load(this, state, hostBindings, null);

        /// <summary>Restores state when the snapshot was saved without source.</summary>
        public ScriptContext Restore(byte[] state, string source, IDictionary<string, object> hostBindings = null) =>
            Snapshot.Load(this, state, hostBindings, source);
    }

    /// <summary>A parsed script. Immutable and shareable between contexts and threads.</summary>
    public sealed class Script
    {
        public string Name { get; }
        public string Source { get; }
        internal string File => Name;
        internal readonly ModuleNode Ast;
        readonly ConcurrentDictionary<long, FunctionCode> _codes = new ConcurrentDictionary<long, FunctionCode>();
        string _hash;

        internal Script(string source, string name)
            : this(Parser.ParseModule(source ?? "", name), source ?? "", name) { }

        internal Script(ModuleNode ast, string source, string name)
        {
            Name = name;
            Source = source;
            Ast = ast;
            ScopeAnalyzer.AnalyzeModule(ast, name, Register);
        }

        internal void Register(Scope s) => _codes[s.CodeKey] = MakeCode(s, s.CodeKey);

        internal FunctionCode ModuleCode => _codes[0];

        /// <summary>Code unit by key; nested units of a not-yet-analyzed function are created on demand.</summary>
        internal FunctionCode GetCode(long key)
        {
            if (_codes.TryGetValue(key, out var c)) return c;
            long root = key & ~ScopeAnalyzer.LocalMask;
            if (root != key && _codes.TryGetValue(root, out var rc))
            {
                rc.EnsureCompiled();
                if (_codes.TryGetValue(key, out c)) return c;
            }
            throw new SnapshotException("unknown code unit " + key);
        }

        /// <summary>Code units created so far (lazily analyzed functions appear after their first compile).</summary>
        internal IEnumerable<FunctionCode> AllCodes => _codes.Values;

        internal string Hash => _hash ??= HashOf(Source);

        internal static string HashOf(string s)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? "")));
        }

        FunctionCode MakeCode(Scope s, long key)
        {
            var fc = new FunctionCode
            {
                Key = key,
                Name = s.Name,
                Script = this,
                Scope = s,
                FreeNames = s.FreeNames.ToArray(),
                ParamNames = s.ParamNames.ToArray(),
                NumParams = s.ParamNames.Count,
                IsGenerator = s.IsGenerator,
            };
            switch (s.Node)
            {
                case ModuleNode _: fc.IsModule = true; break;
                case FuncDef fd: Sig(fc, fd.Args); break;
                case LambdaExpr l: Sig(fc, l.Args); break;
                case ClassDef _: fc.IsClassBody = true; break;
                case CompExpr _: fc.NumPos = 1; break;
            }
            return fc;
        }

        static void Sig(FunctionCode fc, Params p)
        {
            fc.NumPos = p.Positional.Count;
            fc.HasVarArgs = p.VarArg != null;
            fc.NumKwOnly = p.KwOnly.Count;
            fc.HasKwArgs = p.KwArg != null;
        }

        public ScriptContext CreateContext(ScriptEngine engine) => new ScriptContext(engine, this);

        /// <summary>Forces bytecode generation for every function (normally lazy).</summary>
        public void CompileAll()
        {
            int done = -1;
            while (done != _codes.Count)
            {
                done = _codes.Count;
                foreach (var c in _codes.Values.ToList()) c.EnsureCompiled();
            }
        }
    }

    /// <summary>
    /// Execution context: module globals + call stack. Can be suspended at safe points, saved to bytes,
    /// moved to another process/machine and resumed there.
    /// </summary>
    public sealed class ScriptContext
    {
        public ScriptEngine Engine { get; }
        public Script Script { get; }
        internal readonly VM Vm;
        internal readonly ModuleInstance Module;
        internal readonly Dictionary<string, object> HostBindings = new Dictionary<string, object>(StringComparer.Ordinal);
        internal readonly Dictionary<object, string> HostNames = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        /// <summary>True while <see cref="Run"/> executes; state cannot be saved then.</summary>
        internal volatile bool IsRunning;
        internal readonly Dictionary<string, Value> HostModules = new Dictionary<string, Value>(StringComparer.Ordinal);
        internal bool Started, Completed;

        internal ScriptContext(ScriptEngine engine, Script script)
        {
            Engine = engine;
            Script = script;
            Module = new ModuleInstance { Script = script };
            Vm = new VM(engine.Options, Module) { Context = this };
            Module.GetCell("__name__").V = Value.Str("__main__");
        }

        public bool IsSuspended => Started && !Completed;
        public bool IsCompleted => Completed;

        public TextWriter Output
        {
            get => Vm.Out;
            set => Vm.Out = value ?? Console.Out;
        }

        /// <summary>Runs the script from the start, or resumes it after a suspension.</summary>
        /// <param name="stepLimit">Optional budget of safe points (loop iterations / calls) before auto-suspending.</param>
        public ExecutionStatus Run(long stepLimit = -1)
        {
            if (Completed) return ExecutionStatus.Completed;
            if (!Started)
            {
                Started = true;
                var pf = new PyFunction(Script.ModuleCode, Module);
                var f = Vm.NewFrame(pf);
                f.Kind = FK.Module;
                Vm.Push(f);
            }
            Vm.Fuel = stepLimit >= 0 ? stepLimit : long.MaxValue;
            IsRunning = true;
            try
            {
                var st = Vm.Run(0);
                if (st == RunStatus.Suspended) return ExecutionStatus.Suspended;
                Completed = true;
                return ExecutionStatus.Completed;
            }
            catch
            {
                Completed = true;
                while (Vm.Sp > 0) Vm.Frames[--Vm.Sp] = null;
                throw;
            }
            finally
            {
                Vm.Fuel = long.MaxValue;
                IsRunning = false;
            }
        }

        /// <summary>Asks a running script to suspend at its next safe point. Thread-safe.</summary>
        public void RequestSuspend() => Vm.Interrupt = true;

        // ------------------------------------------------------------ globals
        public object this[string name]
        {
            get => GetGlobal(name);
            set => SetGlobal(name, value);
        }

        public object GetGlobal(string name)
        {
            if (!TryGetGlobal(name, out var v)) throw new KeyNotFoundException("global '" + name + "' is not defined");
            return v;
        }

        public bool TryGetGlobal(string name, out object value)
        {
            if (Module.Globals.TryGetValue(name, out var c) && !c.V.IsUnbound)
            {
                value = Interop.ToObject(c.V);
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>Raw script value of a global (no conversion).</summary>
        public Value GetValue(string name) =>
            Module.Globals.TryGetValue(name, out var c) && !c.V.IsUnbound ? c.V : throw new KeyNotFoundException("global '" + name + "' is not defined");

        public void SetGlobal(string name, object value)
        {
            AllowType(value);
            Module.GetCell(name).V = Interop.ToValue(value);
        }

        /// <summary>
        /// .NET types the host handed to the script (globals, modules, bindings). Only these allow static member
        /// access and construction; a Type that reaches the script any other way (e.g. returned by a host method) is inert.
        /// </summary>
        internal readonly HashSet<Type> AllowedTypes = new HashSet<Type>();

        internal void AllowType(object value)
        {
            if (value is Type t) AllowedTypes.Add(t);
        }

        public IEnumerable<string> GlobalNames => Module.Order.Where(c => !c.V.IsUnbound).Select(c => c.Name).ToList();

        /// <summary>
        /// Exposes a host object as a global that is saved by name, not by value. When state is restored,
        /// the host provides a (possibly different) object for the same name.
        /// </summary>
        public void BindHost(string name, object value)
        {
            HostBindings[name] = value;
            AllowType(value);
            if (value != null) HostNames[value] = name;
            SetGlobal(name, value);
        }

        /// <summary>Makes <c>import name</c> return the given host object.</summary>
        public void AddModule(string name, object module)
        {
            AllowType(module);
            HostModules[name] = Interop.ToValue(module);
            if (module != null && Interop.IsHostObject(module)) HostNames[module] = "module:" + name;
        }

        // ------------------------------------------------------------ calls
        /// <summary>Calls a script function by global name. Runs to completion (cannot suspend midway).</summary>
        public object Call(string function, params object[] args)
        {
            var fn = GetValue(function);
            var vals = (args ?? Array.Empty<object>()).Select(Interop.ToValue).ToArray();
            return Interop.ToObject(InvokeNested(() => Vm.CallValue(fn, vals, null)));
        }

        /// <summary>Evaluates an expression against the context's globals.</summary>
        public object Eval(string expression) => Interop.ToObject(EvalValue(expression));

        public Value EvalValue(string expression)
        {
            var expr = Parser.ParseExpression(expression, "<eval>");
            var ast = new ModuleNode { Line = 1, Body = new List<Stmt> { new ExprStmt { Line = 1, X = new LambdaExpr { Line = 1, Args = new Params(), Body = expr } } } };
            var script = new Script(ast, expression, "<eval>");
            var lambda = (LambdaExpr)((ExprStmt)ast.Body[0]).X;
            var pf = new PyFunction(script.GetCode(lambda.Scope.CodeKey), Module);
            return InvokeNested(() => Vm.CallPyNested(pf, default, false, ReadOnlySpan<Value>.Empty, null, FK.Normal, null));
        }

        /// <summary>Executes additional statements in this context's globals (runs to completion).</summary>
        public void Exec(string code)
        {
            var script = new Script(code, "<exec>"); // not cached: ad-hoc snippets would grow the engine cache without bound
            var pf = new PyFunction(script.ModuleCode, Module);
            InvokeNested(() =>
            {
                var f = Vm.NewFrame(pf);
                f.Kind = FK.Module;
                return Vm.RunNested(f);
            });
        }

        Value InvokeNested(Func<Value> action)
        {
            var prev = VM.Current;
            VM.Current = Vm;
            try { return action(); }
            finally { VM.Current = prev; }
        }

        // ------------------------------------------------------------ state
        /// <summary>
        /// Serializes the complete execution state (globals, call stack, generators, closures).
        /// Allowed before the first run, while suspended, or after completion.
        /// </summary>
        public byte[] SaveState(bool includeSource = true) => Snapshot.Save(this, includeSource);
    }
}
