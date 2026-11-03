using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DynLan2
{
    internal enum FK : byte { Normal, Init, ClassBody, GenFor, GenNested, Module }

    internal enum RunStatus { Returned, Suspended }

    /// <summary>Activation record. Lives on the heap, so the whole call stack can be serialized.</summary>
    internal sealed class Frame
    {
        public FunctionCode Code;
        public PyFunction Func;
        public Value[] R;
        public int Pc;
        public bool PcExact;
        public int RetDst = -1;
        public FK Kind;
        public object Aux;          // PyInstance (Init), ClassInfo (ClassBody), PyGenerator (generator frames)
        public Dictionary<string, Value> Ns; // class body namespace
        public int ExitPc = -1;     // GenFor: caller pc when the generator is exhausted
        public int SendDst = -1;    // generator: register receiving the sent value
    }

    internal sealed class ClassInfo
    {
        public string Name;
        public PyClass[] Bases;
    }

    internal sealed class ModuleInstance
    {
        internal Script Script;
        internal readonly Dictionary<string, GlobalCell> Globals = new Dictionary<string, GlobalCell>(StringComparer.Ordinal);
        internal readonly List<GlobalCell> Order = new List<GlobalCell>();

        internal GlobalCell GetCell(string name)
        {
            if (Globals.TryGetValue(name, out var c)) return c;
            c = new GlobalCell(name, Builtins.Lookup(name));
            Globals[name] = c;
            Order.Add(c);
            return c;
        }
    }

    internal sealed partial class VM
    {
        [ThreadStatic] internal static VM Current;

        static VM _fallback;
        /// <summary>Detached VM used to evaluate __str__/__repr__ outside of any run (e.g. exception messages).</summary>
        internal static VM Fallback => _fallback ??= new VM(new EngineOptions { Jit = JitMode.Off }, new ModuleInstance());

        internal Frame[] Frames = new Frame[64];
        internal int Sp;
        internal ModuleInstance Module;
        internal EngineOptions Options;
        internal TextWriter Out;
        internal volatile bool Interrupt;
        internal long Fuel = long.MaxValue;
        internal int NoSuspend;
        internal Value Result;
        internal bool Yielded;
        internal int ClrDepth;
        internal Value[] ArgStack = new Value[256];
        readonly bool _jitOn;
        internal int ArgSp;
        internal PyRandom Random = new PyRandom(Environment.TickCount);
        internal ScriptContext Context;

        const int MaxJitClrDepth = 150;
        const int MaxClrDepth = 900;

        internal VM(EngineOptions options, ModuleInstance module)
        {
            Options = options;
            Module = module;
            Out = options.Output ?? Console.Out;
            _jitOn = options.Jit != JitMode.Off;
        }

        internal Frame Top => Frames[Sp - 1];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Push(Frame f)
        {
            if (Sp == Frames.Length)
            {
                if (Sp >= Options.RecursionLimit) throw Err.Recursion();
                Array.Resize(ref Frames, Sp * 2);
            }
            if (Sp >= Options.RecursionLimit) throw Err.Recursion();
            Frames[Sp++] = f;
        }

        // recycled frames of plain calls, bucketed by register count (frames are never shared across VMs)
        readonly Frame[][] _pool = new Frame[65][];
        readonly int[] _poolCount = new int[65];

        internal Frame NewFrame(PyFunction pf)
        {
            var code = pf.Code;
            if (!code.Compiled) code.EnsureCompiled();
            if (pf.GCells.Length < code.GlobalNames.Length) pf.GCells = new GlobalCell[code.GlobalNames.Length];
            int n = code.NumRegs;
            if (n < _pool.Length && _poolCount[n] > 0)
            {
                var f = _pool[n][--_poolCount[n]];
                _pool[n][_poolCount[n]] = null;
                f.Code = code;
                f.Func = pf;
                return f;
            }
            return new Frame { Code = code, Func = pf, R = new Value[n] };
        }

        /// <summary>Returns a finished plain-call frame to the pool (registers cleared: unbound + no leaks).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Recycle(Frame f)
        {
            int n = f.R.Length;
            if (n >= _pool.Length || f.Aux != null || f.Ns != null) return;
            var p = _pool[n] ??= new Frame[16];
            int c = _poolCount[n];
            if (c == p.Length) return;
            Array.Clear(f.R, 0, n);
            f.Code = null; f.Func = null; f.Pc = 0; f.PcExact = false; f.RetDst = -1; f.Kind = FK.Normal; f.ExitPc = -1; f.SendDst = -1;
            p[c] = f;
            _poolCount[n] = c + 1;
        }

        // ------------------------------------------------------------ argument binding
        internal static void Bind(Frame nf, PyFunction pf, Value self, bool hasSelf, ReadOnlySpan<Value> args, string[] kwnames)
        {
            var code = pf.Code;
            var R = nf.R;
            int nkw = kwnames?.Length ?? 0;
            int npos = args.Length - nkw;
            int given = npos + (hasSelf ? 1 : 0);
            int np = code.NumPos;
            if (nkw == 0 && given == np && !code.HasVarArgs && !code.HasKwArgs && code.NumKwOnly == 0)
            {
                int o = 0;
                if (hasSelf) R[o++] = self;
                for (int j = 0; j < npos; j++) R[o++] = args[j];
                return;
            }
            BindSlow(nf, pf, self, hasSelf, args, kwnames);
        }

        static void BindSlow(Frame nf, PyFunction pf, Value self, bool hasSelf, ReadOnlySpan<Value> args, string[] kwnames)
        {
            var code = pf.Code;
            var R = nf.R;
            int nkw = kwnames?.Length ?? 0;
            int npos = args.Length - nkw;
            int np = code.NumPos;
            int i = 0;
            List<Value> extra = null;
            if (hasSelf)
            {
                if (np > 0) R[i++] = self;
                else (extra ??= new List<Value>()).Add(self);
            }
            for (int j = 0; j < npos; j++)
            {
                if (i < np) R[i++] = args[j];
                else (extra ??= new List<Value>()).Add(args[j]);
            }
            int vaReg = code.HasVarArgs ? np : -1;
            int kwOnlyStart = np + (code.HasVarArgs ? 1 : 0);
            int kwReg = code.HasKwArgs ? kwOnlyStart + code.NumKwOnly : -1;
            if (extra != null)
            {
                if (vaReg < 0)
                    throw Err.Type(pf.Name + "() takes " + np + " positional argument" + (np == 1 ? "" : "s") + " but " + (npos + (hasSelf ? 1 : 0)) + " were given");
                R[vaReg] = Value.Obj(new PyTuple(extra.ToArray()));
            }
            else if (vaReg >= 0) R[vaReg] = Value.Obj(PyTuple.Empty);
            PyDict kwd = kwReg >= 0 ? new PyDict() : null;
            for (int j = 0; j < nkw; j++)
            {
                string name = kwnames[j];
                Value v = args[npos + j];
                int idx = -1;
                for (int p = 0; p < np; p++) if (code.ParamNames[p] == name) { idx = p; break; }
                if (idx < 0)
                    for (int p = 0; p < code.NumKwOnly; p++) if (code.ParamNames[kwOnlyStart + p] == name) { idx = kwOnlyStart + p; break; }
                if (idx >= 0)
                {
                    if (!R[idx].IsUnbound) throw Err.Type(pf.Name + "() got multiple values for argument '" + name + "'");
                    R[idx] = v;
                }
                else if (kwd != null) kwd.Set(Value.Str(name), v);
                else throw Err.Type(pf.Name + "() got an unexpected keyword argument '" + name + "'");
            }
            List<string> missing = null;
            var defs = pf.Defaults;
            int firstDef = np - (defs?.Length ?? 0);
            for (int p = 0; p < np; p++)
            {
                if (!R[p].IsUnbound) continue;
                if (p >= firstDef) R[p] = defs[p - firstDef];
                else (missing ??= new List<string>()).Add(code.ParamNames[p]);
            }
            if (missing != null)
                throw Err.Type(pf.Name + "() missing " + missing.Count + " required positional argument" + (missing.Count == 1 ? "" : "s") + ": '" + string.Join("', '", missing) + "'");
            for (int p = 0; p < code.NumKwOnly; p++)
            {
                int r = kwOnlyStart + p;
                if (!R[r].IsUnbound) continue;
                var d = pf.KwDefaults != null && p < pf.KwDefaults.Length ? pf.KwDefaults[p] : default;
                if (d.IsUnbound) throw Err.Type(pf.Name + "() missing 1 required keyword-only argument: '" + code.ParamNames[r] + "'");
                R[r] = d;
            }
            if (kwReg >= 0) R[kwReg] = Value.Obj(kwd);
        }

        // ------------------------------------------------------------ exceptions
        internal static ScriptException ToScriptException(Exception ex)
        {
            switch (ex)
            {
                case ScriptException se: return se;
                case TargetInvocationException tie when tie.InnerException != null: return ToScriptException(tie.InnerException);
                case InsufficientExecutionStackException _: return Err.Recursion();
                case DivideByZeroException _: return Err.ZeroDiv();
                case OverflowException o: return Wrap(Builtins.OverflowError, o);
                case KeyNotFoundException k: return Wrap(Builtins.KeyError, k);
                case IndexOutOfRangeException i: return Wrap(Builtins.IndexError, i);
                case ArgumentOutOfRangeException a: return Wrap(Builtins.IndexError, a);
                case InvalidCastException c: return Wrap(Builtins.TypeError, c);
                case FormatException f: return Wrap(Builtins.ValueError, f);
                case OutOfMemoryException m: return Wrap(Builtins.MemoryError, m);
                case NotImplementedException n: return Wrap(Builtins.NotImplementedError, n);
                case NotSupportedException n: return Wrap(Builtins.NotImplementedError, n);
                default: return Wrap(Builtins.Exception, ex);
            }
        }

        static ScriptException Wrap(PyClass cls, Exception ex)
        {
            var inst = Builtins.NewException(cls, ex.Message);
            inst.Attrs["clr_type"] = Value.Str(ex.GetType().FullName);
            var se = new ScriptException(Value.Obj(inst), ex);
            inst.Clr = se;
            return se;
        }

        /// <summary>Unwinds frames above <paramref name="baseDepth"/> looking for a handler. True if one was found.</summary>
        internal bool HandleException(ScriptException se, int baseDepth)
        {
            if (se.PyValue.O is PyInstance pi) pi.Clr = se;
            se.FileName ??= Module?.Script?.File;
            se.Vm ??= this;
            while (Sp > baseDepth)
            {
                var f = Frames[Sp - 1];
                int pc = f.PcExact ? f.Pc : f.Pc - 1;
                se.Trace.Add((f.Code.Name, f.Code.LineAt(pc)));
                var hs = f.Code.Handlers;
                if (hs != null)
                {
                    for (int i = 0; i < hs.Length; i++)
                    {
                        var h = hs[i];
                        if (pc >= h.Start && pc < h.End)
                        {
                            f.R[h.ExcReg] = se.PyValue;
                            f.Pc = h.Target;
                            f.PcExact = true;
                            return true;
                        }
                    }
                }
                Frames[--Sp] = null;
                if (f.Kind == FK.GenFor || f.Kind == FK.GenNested)
                {
                    var g = (PyGenerator)f.Aux;
                    g.Done = true;
                    g.Running = false;
                }
            }
            return false;
        }

        // ------------------------------------------------------------ frame exit
        /// <summary>Pops the top frame delivering <paramref name="v"/>; true when the run loop must exit.</summary>
        internal bool PopFrame(Value v, int baseDepth)
        {
            var f = Frames[--Sp];
            Frames[Sp] = null;
            Result = v;
            Frame caller = Sp > 0 ? Frames[Sp - 1] : null;
            switch (f.Kind)
            {
                case FK.Normal:
                    if (f.RetDst >= 0 && caller != null) caller.R[f.RetDst] = v;
                    Recycle(f);
                    break;
                case FK.Init:
                    if (!v.IsNone) throw Err.Type("__init__() should return None, not '" + Ops.TypeName(v) + "'");
                    Result = Value.Obj(f.Aux);
                    if (f.RetDst >= 0 && caller != null) caller.R[f.RetDst] = Result;
                    break;
                case FK.ClassBody:
                    Result = Value.Obj(BuildClass((ClassInfo)f.Aux, f.Ns));
                    if (f.RetDst >= 0 && caller != null) caller.R[f.RetDst] = Result;
                    break;
                case FK.GenFor:
                    {
                        var g = (PyGenerator)f.Aux;
                        g.Done = true;
                        g.Running = false;
                        caller.Pc = f.ExitPc;
                        caller.PcExact = true;
                        break;
                    }
                case FK.GenNested:
                    {
                        var g = (PyGenerator)f.Aux;
                        g.Done = true;
                        g.Running = false;
                        Yielded = false;
                        break;
                    }
                case FK.Module:
                    break;
            }
            return Sp <= baseDepth;
        }

        bool YieldFrame(Value v, int baseDepth)
        {
            var f = Frames[--Sp];
            Frames[Sp] = null;
            var g = (PyGenerator)f.Aux;
            g.Running = false;
            Result = v;
            if (f.Kind == FK.GenFor)
            {
                Frames[Sp - 1].R[f.RetDst] = v;
            }
            else Yielded = true;
            return Sp <= baseDepth;
        }

        internal PyClass BuildClass(ClassInfo info, Dictionary<string, Value> ns)
        {
            var bases = info.Bases.Length == 0 ? new[] { Builtins.ObjectClass } : info.Bases;
            var cls = new PyClass(info.Name, bases);
            foreach (var kv in ns)
            {
                cls.Dict[kv.Key] = kv.Value;
                SetOwner(kv.Value, cls);
            }
            System.Threading.Interlocked.Increment(ref PyClass.Stamp);
            return cls;
        }

        static void SetOwner(Value v, PyClass cls)
        {
            switch (v.O)
            {
                case PyFunction f: if (f.Owner == null) f.Owner = cls; break;
                case PyStaticMethod s: SetOwner(s.Fn, cls); break;
                case PyClassMethod c: SetOwner(c.Fn, cls); break;
                case PyProperty p: SetOwner(p.Get, cls); SetOwner(p.Set, cls); SetOwner(p.Del, cls); break;
            }
        }

        internal PyFunction MakeFunction(Frame f, ChildRef child)
        {
            var pf = new PyFunction(child.Code, f.Func.Module);
            var fs = child.FreeSources;
            if (fs.Length > 0)
            {
                var cells = new Cell[fs.Length];
                for (int i = 0; i < fs.Length; i++)
                    cells[i] = fs[i] >= 0 ? (Cell)f.R[fs[i]].O : f.Func.Closure[~fs[i]];
                pf.Closure = cells;
            }
            return pf;
        }

        // ------------------------------------------------------------ entry points
        internal RunStatus Run(int baseDepth)
        {
            var prev = Current;
            Current = this;
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
                while (true)
                {
                    try
                    {
                        return Loop(baseDepth);
                    }
                    catch (Exception ex)
                    {
                        var se = ToScriptException(ex);
                        if (!HandleException(se, baseDepth))
                        {
                            if (ReferenceEquals(se, ex)) throw;
                            throw se;
                        }
                    }
                }
            }
            finally { Current = prev; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Value RK(Value[] r, Value[] k, int o) => o >= 0 ? r[o] : k[~o];

        bool _osrFault;

        bool MarkPc(Frame f, int pc)
        {
            if (_osrFault) { _osrFault = false; return false; } // compiled code already recorded the exact pc
            f.Pc = pc;
            f.PcExact = true;
            return false;
        }

        /// <summary>The interpreter loop. Exits when the frame at <paramref name="baseDepth"/> returns, or on suspension.</summary>
        RunStatus Loop(int baseDepth)
        {
            Frame f = null;
            FunctionCode fc;
            int[] code;
            Value[] r, k;
            int pc = 0;
            try
            {
            reload:
                f = Frames[Sp - 1];
                fc = f.Code;
                code = fc.Code;
                r = f.R;
                k = fc.Consts;
                pc = f.Pc;
                while (true)
                {
                    switch ((Op)code[pc])
                    {
                        case Op.NOP: pc += 1; break;
                        case Op.MOVE: r[code[pc + 1]] = r[code[pc + 2]]; pc += 3; break;
                        case Op.LOADK: r[code[pc + 1]] = k[code[pc + 2]]; pc += 3; break;
                        case Op.LOADGLOBAL:
                            {
                                var cells = f.Func.GCells;
                                int g = code[pc + 2];
                                var c = cells[g] ?? (cells[g] = f.Func.Module.GetCell(fc.GlobalNames[g]));
                                var v = c.V;
                                if (v.IsUnbound) { v = c.Builtin; if (v.IsUnbound) throw Err.Name(c.Name); }
                                r[code[pc + 1]] = v;
                                pc += 3;
                                break;
                            }
                        case Op.STOREGLOBAL:
                            {
                                var cells = f.Func.GCells;
                                int g = code[pc + 1];
                                var c = cells[g] ?? (cells[g] = f.Func.Module.GetCell(fc.GlobalNames[g]));
                                c.V = r[code[pc + 2]];
                                pc += 3;
                                break;
                            }
                        case Op.LOADDEREF:
                            {
                                var v = f.Func.Closure[code[pc + 2]].V;
                                if (v.IsUnbound) throw new ScriptException(Builtins.NameError, "free variable '" + fc.FreeNames[code[pc + 2]] + "' referenced before assignment");
                                r[code[pc + 1]] = v;
                                pc += 3;
                                break;
                            }
                        case Op.STOREDEREF: f.Func.Closure[code[pc + 1]].V = r[code[pc + 2]]; pc += 3; break;
                        case Op.LOADCELL:
                            {
                                var v = ((Cell)r[code[pc + 2]].O).V;
                                if (v.IsUnbound) throw Err.Unbound(CellName(fc, code[pc + 2]));
                                r[code[pc + 1]] = v;
                                pc += 3;
                                break;
                            }
                        case Op.STORECELL: ((Cell)r[code[pc + 1]].O).V = r[code[pc + 2]]; pc += 3; break;
                        case Op.CHKBOUND:
                            if (r[code[pc + 1]].IsUnbound) throw Err.Unbound(fc.Names[code[pc + 2]]);
                            pc += 3;
                            break;

                        case Op.ADD:
                            {
                                Value a = RK(r, k, code[pc + 2]), b = RK(r, k, code[pc + 3]);
                                if (a.IsInt && b.IsInt)
                                {
                                    long x = a.N, y = b.N, s = x + y;
                                    if (((x ^ s) & (y ^ s)) >= 0) { r[code[pc + 1]] = Value.Int(s); pc += 4; break; }
                                }
                                else if (a.IsFloat && b.IsFloat) { r[code[pc + 1]] = Value.Float(a.AsFloat + b.AsFloat); pc += 4; break; }
                                r[code[pc + 1]] = Ops.Add(a, b);
                                pc += 4;
                                break;
                            }
                        case Op.SUB:
                            {
                                Value a = RK(r, k, code[pc + 2]), b = RK(r, k, code[pc + 3]);
                                if (a.IsInt && b.IsInt)
                                {
                                    long x = a.N, y = b.N, s = x - y;
                                    if (((x ^ y) & (x ^ s)) >= 0) { r[code[pc + 1]] = Value.Int(s); pc += 4; break; }
                                }
                                else if (a.IsFloat && b.IsFloat) { r[code[pc + 1]] = Value.Float(a.AsFloat - b.AsFloat); pc += 4; break; }
                                r[code[pc + 1]] = Ops.Sub(a, b);
                                pc += 4;
                                break;
                            }
                        case Op.MUL:
                            {
                                Value a = RK(r, k, code[pc + 2]), b = RK(r, k, code[pc + 3]);
                                if (a.IsInt && b.IsInt) r[code[pc + 1]] = Ops.MulInt(a.N, b.N);
                                else if (a.IsFloat && b.IsFloat) r[code[pc + 1]] = Value.Float(a.AsFloat * b.AsFloat);
                                else r[code[pc + 1]] = Ops.Mul(a, b);
                                pc += 4;
                                break;
                            }
                        case Op.LT:
                            {
                                Value a = RK(r, k, code[pc + 2]), b = RK(r, k, code[pc + 3]);
                                r[code[pc + 1]] = Value.Bool(a.IsInt && b.IsInt ? a.N < b.N : Ops.Lt(a, b));
                                pc += 4;
                                break;
                            }
                        case Op.EQ:
                            {
                                Value a = RK(r, k, code[pc + 2]), b = RK(r, k, code[pc + 3]);
                                r[code[pc + 1]] = Value.Bool(a.IsInt && b.IsInt ? a.N == b.N : Ops.EqValues(a, b));
                                pc += 4;
                                break;
                            }
                        case Op.IADD:
                            {
                                Value a = RK(r, k, code[pc + 2]), b = RK(r, k, code[pc + 3]);
                                if (a.IsInt && b.IsInt)
                                {
                                    long x = a.N, y = b.N, s = x + y;
                                    if (((x ^ s) & (y ^ s)) >= 0) { r[code[pc + 1]] = Value.Int(s); pc += 4; break; }
                                }
                                else if (a.IsFloat && b.IsFloat) { r[code[pc + 1]] = Value.Float(a.AsFloat + b.AsFloat); pc += 4; break; }
                                r[code[pc + 1]] = Ops.IAdd(a, b);
                                pc += 4;
                                break;
                            }
                        case Op.TRUEDIV: case Op.FLOORDIV: case Op.MOD: case Op.POW: case Op.LSHIFT: case Op.RSHIFT:
                        case Op.BITAND: case Op.BITOR: case Op.BITXOR: case Op.MATMUL:
                        case Op.LE: case Op.GT: case Op.GE: case Op.NE: case Op.IN: case Op.NOTIN: case Op.IS: case Op.ISNOT:
                            r[code[pc + 1]] = Ops.Binary((Op)code[pc], RK(r, k, code[pc + 2]), RK(r, k, code[pc + 3]));
                            pc += 4;
                            break;
                        case Op.NOT: r[code[pc + 1]] = Value.Bool(!Ops.Truthy(r[code[pc + 2]])); pc += 3; break;

                        case Op.JMP:
                            {
                                int t = code[pc + 1];
                                if (t <= pc)
                                {
                                    f.Pc = t;
                                    if (Interrupt | (--Fuel <= 0)) { if (TrySuspend()) return RunStatus.Suspended; }
                                    if (_jitOn && ++fc.Hot > Options.JitThreshold && fc.JitState != 3)
                                    {
                                        int st = TryOsr(f, fc, baseDepth);
                                        if (st == 1) return RunStatus.Suspended;
                                        if (st == 0) return RunStatus.Returned;
                                        if (st == 2) goto reload;
                                    }
                                }
                                pc = t;
                                break;
                            }
                        case Op.JT:
                        case Op.JF:
                            {
                                bool tr = Ops.Truthy(r[code[pc + 1]]);
                                if (tr == ((Op)code[pc] == Op.JT))
                                {
                                    int t = code[pc + 2];
                                    if (t <= pc)
                                    {
                                        f.Pc = t;
                                        if (Interrupt | (--Fuel <= 0)) { if (TrySuspend()) return RunStatus.Suspended; }
                                        if (_jitOn && ++fc.Hot > Options.JitThreshold && fc.JitState != 3)
                                        {
                                            int st = TryOsr(f, fc, baseDepth);
                                            if (st == 1) return RunStatus.Suspended;
                                            if (st == 0) return RunStatus.Returned;
                                            if (st == 2) goto reload;
                                        }
                                    }
                                    pc = t;
                                }
                                else pc += 3;
                                break;
                            }
                        case Op.JCMP:
                        case Op.JNCMP:
                            {
                                Value a = RK(r, k, code[pc + 2]), b = RK(r, k, code[pc + 3]);
                                bool res;
                                if (a.IsInt && b.IsInt)
                                {
                                    long x = a.N, y = b.N;
                                    switch ((Cmp)code[pc + 1])
                                    {
                                        case Cmp.LT: res = x < y; break;
                                        case Cmp.LE: res = x <= y; break;
                                        case Cmp.GT: res = x > y; break;
                                        case Cmp.GE: res = x >= y; break;
                                        case Cmp.EQ: res = x == y; break;
                                        default: res = x != y; break;
                                    }
                                }
                                else res = Ops.CompareOp((Cmp)code[pc + 1], a, b);
                                if (res == ((Op)code[pc] == Op.JCMP))
                                {
                                    int t = code[pc + 4];
                                    if (t <= pc)
                                    {
                                        f.Pc = t;
                                        if (Interrupt | (--Fuel <= 0)) { if (TrySuspend()) return RunStatus.Suspended; }
                                        if (_jitOn && ++fc.Hot > Options.JitThreshold && fc.JitState != 3)
                                        {
                                            int st = TryOsr(f, fc, baseDepth);
                                            if (st == 1) return RunStatus.Suspended;
                                            if (st == 0) return RunStatus.Returned;
                                            if (st == 2) goto reload;
                                        }
                                    }
                                    pc = t;
                                }
                                else pc += 5;
                                break;
                            }

                        case Op.GETATTR:
                            {
                                Value o = r[code[pc + 2]];
                                string name = fc.Names[code[pc + 3]];
                                if (o.O is PyInstance inst && inst.Attrs.TryGetValue(name, out var av)) r[code[pc + 1]] = av;
                                else r[code[pc + 1]] = Ops.GetAttr(o, name);
                                pc += 4;
                                break;
                            }
                        case Op.SETATTR:
                            {
                                Value o = r[code[pc + 1]];
                                string name = fc.Names[code[pc + 2]];
                                if (o.O is PyInstance inst && inst.Class.PlainSetAttr)
                                    inst.Attrs[name] = r[code[pc + 3]];
                                else Ops.SetAttr(o, name, r[code[pc + 3]]);
                                pc += 4;
                                break;
                            }
                        case Op.GETITEM:
                            {
                                Value o = r[code[pc + 2]], key = r[code[pc + 3]];
                                if (o.O is PyList l && key.IsInt && (ulong)key.N < (ulong)l.Items.Count) r[code[pc + 1]] = l.Items[(int)key.N];
                                else r[code[pc + 1]] = Ops.GetItem(o, key);
                                pc += 4;
                                break;
                            }
                        case Op.SETITEM:
                            {
                                Value o = r[code[pc + 1]], key = r[code[pc + 2]];
                                if (o.O is PyList l && key.IsInt && (ulong)key.N < (ulong)l.Items.Count) l.Items[(int)key.N] = r[code[pc + 3]];
                                else Ops.SetItem(o, key, r[code[pc + 3]]);
                                pc += 4;
                                break;
                            }

                        case Op.CALL:
                        case Op.CALLKW:
                            {
                                bool kw = (Op)code[pc] == Op.CALLKW;
                                int d = code[pc + 1], b = code[pc + 3], n = code[pc + 4];
                                int next = pc + (kw ? 6 : 5);
                                string[] kwn = kw ? (string[])k[code[pc + 5]].O : null;
                                int st = CallInterp(f, next, d, r[code[pc + 2]], default, false, new ReadOnlySpan<Value>(r, b, n), kwn, baseDepth);
                                if (st == 0) { r[d] = Result; pc = next; if (Interrupt && NoSuspend == 0) { f.Pc = pc; if (TrySuspend()) return RunStatus.Suspended; } break; }
                                if (st == 1) goto reload;
                                if (st == 2) return RunStatus.Suspended;
                                return RunStatus.Returned;
                            }
                        case Op.CALLM:
                        case Op.CALLMKW:
                            {
                                bool kw = (Op)code[pc] == Op.CALLMKW;
                                int d = code[pc + 1], b = code[pc + 2], n = code[pc + 4];
                                int next = pc + (kw ? 6 : 5);
                                string[] kwn = kw ? (string[])k[code[pc + 5]].O : null;
                                string name = fc.Names[code[pc + 3]];
                                Value obj = r[b];
                                var args = new ReadOnlySpan<Value>(r, b + 1, n);
                                int st;
                                if (obj.O is PyInstance inst && !inst.Attrs.ContainsKey(name) && inst.Class.Lookup(name, out var cv) && cv.O is PyFunction mpf)
                                    st = CallInterp(f, next, d, Value.Obj(mpf), obj, true, args, kwn, baseDepth);
                                else
                                {
                                    var mf = obj.O is PyInstance || obj.O is PyClass ? null : Builtins.FindMethod(obj, name);
                                    if (mf != null)
                                    {
                                        Result = mf(this, obj, kwn == null ? args : args.Slice(0, n - kwn.Length), kwn == null ? null : new KwArgs(kwn, args.Slice(n - kwn.Length).ToArray()));
                                        st = 0;
                                    }
                                    else st = CallInterp(f, next, d, Ops.GetAttr(obj, name), default, false, args, kwn, baseDepth);
                                }
                                if (st == 0) { r[d] = Result; pc = next; if (Interrupt && NoSuspend == 0) { f.Pc = pc; if (TrySuspend()) return RunStatus.Suspended; } break; }
                                if (st == 1) goto reload;
                                if (st == 2) return RunStatus.Suspended;
                                return RunStatus.Returned;
                            }
                        case Op.CALLEX:
                            {
                                int d = code[pc + 1];
                                var argList = Ops.ToList(r[code[pc + 3]]);
                                string[] kwn = null;
                                int kwr = code[pc + 4];
                                if (kwr >= 0)
                                {
                                    var kd = r[kwr].O as PyDict ?? throw Err.Type("argument after ** must be a mapping");
                                    kwn = new string[kd.Count];
                                    int i = 0;
                                    foreach (var kv in kd.Items())
                                    {
                                        kwn[i++] = kv.Key.O as string ?? throw Err.Type("keywords must be strings");
                                        argList.Add(kv.Value);
                                    }
                                }
                                int next = pc + 5;
                                var arr = argList.ToArray();
                                int st = CallInterp(f, next, d, r[code[pc + 2]], default, false, arr, kwn, baseDepth);
                                if (st == 0) { r[d] = Result; pc = next; break; }
                                if (st == 1) goto reload;
                                if (st == 2) return RunStatus.Suspended;
                                return RunStatus.Returned;
                            }
                        case Op.RET:
                            if (PopFrame(r[code[pc + 1]], baseDepth)) return RunStatus.Returned;
                            goto reload;
                        case Op.RETNONE:
                            if (PopFrame(Value.None, baseDepth)) return RunStatus.Returned;
                            goto reload;

                        case Op.GETITER: r[code[pc + 1]] = Value.Obj(Ops.GetIter(r[code[pc + 2]])); pc += 3; break;
                        case Op.FORITER:
                            {
                                object it = r[code[pc + 1]].O;
                                if (it is ListIter li)
                                {
                                    if (li.I < li.L.Items.Count) { r[code[pc + 2]] = li.L.Items[li.I++]; pc += 4; }
                                    else pc = code[pc + 3];
                                    break;
                                }
                                if (it is PyGenerator g)
                                {
                                    if (g.Done) { pc = code[pc + 3]; break; }
                                    StartGenFor(f, g, code[pc + 2], code[pc + 3], pc + 4);
                                    goto reload;
                                }
                                if (((PyIterator)it).Next(this, out var v)) { r[code[pc + 2]] = v; pc += 4; }
                                else pc = code[pc + 3];
                                break;
                            }
                        case Op.FORPREP_RANGE:
                            ForPrepRange(f, r, code[pc + 1], r[code[pc + 2]], code[pc + 3], code[pc + 4]);
                            pc += 5;
                            break;
                        case Op.FORITER_RANGE:
                            {
                                int it = code[pc + 1];
                                Value c = r[it];
                                if (c.IsInt)
                                {
                                    long cur = c.N, step = r[it + 2].N;
                                    if (step > 0 ? cur < r[it + 1].N : cur > r[it + 1].N)
                                    {
                                        r[code[pc + 2]] = c;
                                        r[it] = Value.Int(cur + step);
                                        pc += 4;
                                    }
                                    else pc = code[pc + 3];
                                    break;
                                }
                                if (c.O is PyGenerator g)
                                {
                                    if (g.Done) { pc = code[pc + 3]; break; }
                                    StartGenFor(f, g, code[pc + 2], code[pc + 3], pc + 4);
                                    goto reload;
                                }
                                if (((PyIterator)c.O).Next(this, out var v)) { r[code[pc + 2]] = v; pc += 4; }
                                else pc = code[pc + 3];
                                break;
                            }

                        case Op.MAKECLASS:
                            {
                                var child = fc.Children[code[pc + 2]];
                                int b = code[pc + 3], n = code[pc + 4];
                                var bases = new PyClass[n];
                                for (int i = 0; i < n; i++)
                                {
                                    var bv = r[b + i];
                                    if (bv.O is PyClass pc0) bases[i] = pc0;
                                    else if (bv.O is PyType pt0) throw Err.Type("subclassing built-in type '" + pt0.Name + "' is not supported");
                                    else throw Err.Type("bases must be classes");
                                }
                                var pf = MakeFunction(f, child);
                                var nf = NewFrame(pf);
                                nf.Kind = FK.ClassBody;
                                nf.Aux = new ClassInfo { Name = child.Code.Name, Bases = bases };
                                nf.Ns = new Dictionary<string, Value>(StringComparer.Ordinal);
                                nf.RetDst = code[pc + 1];
                                f.Pc = pc + 5;
                                f.PcExact = false;
                                Push(nf);
                                goto reload;
                            }
                        case Op.YIELD:
                            {
                                f.SendDst = code[pc + 1];
                                f.Pc = pc + 3;
                                f.PcExact = false;
                                if (YieldFrame(r[code[pc + 2]], baseDepth)) return RunStatus.Returned;
                                goto reload;
                            }

                        default:
                            ExecSimple(f, r, code, pc);
                            pc += OpInfo.Len[code[pc]];
                            break;
                    }
                }
            }
            catch (Exception) when (MarkPc(f, pc))
            {
                throw;
            }
        }

        static string CellName(FunctionCode fc, int reg)
        {
            foreach (var s in fc.Scope.Syms.Values) if (s.Reg == reg && s.Kind == SymKind.Cell) return s.Name;
            return "?";
        }

        internal bool TrySuspendInternal() => TrySuspend();

        internal int AfterSync() => Interrupt && NoSuspend == 0 && TrySuspend() ? 3 : 0;

        internal int ArgReserve(int n)
        {
            int b = ArgSp;
            if (b + n > ArgStack.Length) Array.Resize(ref ArgStack, Math.Max(ArgStack.Length * 2, b + n + 16));
            ArgSp = b + n;
            return b;
        }

        /// <summary>
        /// Call from JIT-compiled code. 0 = done (Result), 1 = suspended inside the callee (caller must spill and return),
        /// 3 = done but a suspension was requested (caller stores the result, then spills).
        /// </summary>
        internal int JitInvoke(Frame caller, Value fn, Value self, bool hasSelf, ReadOnlySpan<Value> args, string[] kwn, int resumePc, int dst)
        {
            PyFunction pf = null;
            FK kind = FK.Normal;
            object aux = null;
            switch (fn.O)
            {
                case PyFunction p: pf = p; break;
                case BoundMethod bm when bm.Func.O is PyFunction bp && !hasSelf:
                    pf = bp; self = bm.Self; hasSelf = true;
                    break;
                case PyClass cls when !hasSelf:
                    {
                        var inst = Builtins.NewInstance(cls);
                        if (cls.Lookup("__init__", out var init) && init.O is PyFunction ip)
                        {
                            pf = ip; self = Value.Obj(inst); hasSelf = true; kind = FK.Init; aux = inst;
                        }
                        else
                        {
                            Result = Instantiate(cls, inst, args, kwn);
                            return AfterSync();
                        }
                        break;
                    }
            }
            if (pf == null)
            {
                if (hasSelf)
                {
                    var all = new Value[args.Length + 1];
                    all[0] = self;
                    args.CopyTo(new Span<Value>(all, 1, args.Length));
                    Result = CallValueKw(fn, all, kwn);
                }
                else Result = CallValueKw(fn, args, kwn);
                return AfterSync();
            }
            var nf = NewFrame(pf);
            Bind(nf, pf, self, hasSelf, args, kwn);
            var code = pf.Code;
            if (code.IsGenerator)
            {
                Result = Value.Obj(NewGenerator(nf, pf));
                return 0;
            }
            nf.Kind = kind;
            nf.Aux = aux;
            nf.RetDst = dst;
            caller.Pc = resumePc;
            caller.PcExact = false;
            int baseDepth = Sp;
            Push(nf);
            var jit = code.Jit;
            if (jit != null && ClrDepth < MaxJitClrDepth)
            {
                int st;
                try { st = InvokeJit(nf, jit); }
                catch (Exception ex)
                {
                    var se = ToScriptException(ex);
                    HandleException(se, baseDepth);
                    if (ReferenceEquals(se, ex)) throw;
                    throw se;
                }
                if (st == 0) { PopFrame(Result, baseDepth); return AfterSync(); }
                if (st == 1) return 1;
            }
            else if (_jitOn && ++code.Hot > Options.JitThreshold && code.JitState == 0) Jit.Request(code, this);
            if (ClrDepth >= MaxClrDepth) throw Err.Recursion();
            ClrDepth++;
            try
            {
                var rs = Run(baseDepth);
                return rs == RunStatus.Suspended ? 1 : AfterSync();
            }
            finally { ClrDepth--; }
        }

        bool TrySuspend()
        {
            if (NoSuspend > 0) return false;
            Interrupt = false;
            if (Fuel <= 0) Fuel = long.MaxValue;
            return true;
        }

        /// <summary>On-stack replacement at a hot loop back-edge. -1 = keep interpreting, 0 = returned (base reached), 1 = suspended, 2 = reload.</summary>
        int TryOsr(Frame f, FunctionCode fc, int baseDepth)
        {
            var jit = fc.Jit;
            if (jit == null)
            {
                Jit.Request(fc, this);
                jit = fc.Jit;
                if (jit == null) return -1;
            }
            if (ClrDepth >= MaxJitClrDepth) return -1;
            int st;
            try { st = InvokeJit(f, jit); }
            catch { _osrFault = true; throw; }
            if (st == 0) return PopFrame(Result, baseDepth) ? 0 : 2;
            if (st == 1) return 1;
            return 2; // deopt: continue interpreting from f.Pc
        }

        internal int InvokeJit(Frame f, JitFn jit)
        {
            ClrDepth++;
            try { return jit(this, f); }
            finally { ClrDepth--; }
        }

        void StartGenFor(Frame f, PyGenerator g, int dst, int exit, int next)
        {
            if (g.Running) throw Err.Value("generator already executing");
            var gf = g.Frame;
            g.Running = true;
            g.Started = true;
            gf.Kind = FK.GenFor;
            gf.RetDst = dst;
            gf.ExitPc = exit;
            if (gf.SendDst >= 0) gf.R[gf.SendDst] = Value.None;
            f.Pc = next;
            f.PcExact = false;
            Push(gf);
        }

        internal void ForPrepRange(Frame f, Value[] r, int it, Value fn, int b, int n)
        {
            if (ReferenceEquals(fn.O, Builtins.RangeType))
            {
                bool ok = true;
                for (int i = 0; i < n; i++) if (!(r[b + i].IsInt || r[b + i].IsBool)) { ok = false; break; }
                if (ok)
                {
                    long start = 0, stop, step = 1;
                    if (n == 1) stop = r[b].N;
                    else { start = r[b].N; stop = r[b + 1].N; if (n == 3) step = r[b + 2].N; }
                    if (step == 0) throw Err.Value("range() arg 3 must not be zero");
                    r[it] = Value.Int(start);
                    r[it + 1] = Value.Int(stop);
                    r[it + 2] = Value.Int(step);
                    return;
                }
            }
            var v = CallValue(fn, new ReadOnlySpan<Value>(r, b, n), null);
            r[it] = Value.Obj(Ops.GetIter(v));
        }

        // ------------------------------------------------------------ calls from the interpreter
        /// <summary>0 = done (Result), 1 = new frame pushed (reload), 2 = suspended, 3 = base frame returned.</summary>
        int CallInterp(Frame caller, int resumePc, int dst, Value fn, Value self, bool hasSelf, ReadOnlySpan<Value> args, string[] kwn, int baseDepth)
        {
            switch (fn.O)
            {
                case PyFunction pf:
                    return CallPy(caller, resumePc, dst, pf, self, hasSelf, args, kwn, FK.Normal, null, baseDepth);
                case BoundMethod bm when bm.Func.O is PyFunction bpf && !hasSelf:
                    return CallPy(caller, resumePc, dst, bpf, bm.Self, true, args, kwn, FK.Normal, null, baseDepth);
                case PyClass cls when !hasSelf:
                    {
                        var inst = Builtins.NewInstance(cls);
                        if (cls.Lookup("__init__", out var init) && init.O is PyFunction ipf)
                            return CallPy(caller, resumePc, dst, ipf, Value.Obj(inst), true, args, kwn, FK.Init, inst, baseDepth);
                        Result = Instantiate(cls, inst, args, kwn);
                        return 0;
                    }
            }
            if (hasSelf)
            {
                var all = new Value[args.Length + 1];
                all[0] = self;
                args.CopyTo(new Span<Value>(all, 1, args.Length));
                Result = CallValueKw(fn, all, kwn);
            }
            else Result = CallValueKw(fn, args, kwn);
            return 0;
        }

        int CallPy(Frame caller, int resumePc, int dst, PyFunction pf, Value self, bool hasSelf, ReadOnlySpan<Value> args, string[] kwn,
            FK kind, object aux, int baseDepth)
        {
            var nf = NewFrame(pf);
            Bind(nf, pf, self, hasSelf, args, kwn);
            var code = pf.Code;
            if (code.IsGenerator)
            {
                Result = Value.Obj(NewGenerator(nf, pf));
                return 0;
            }
            nf.Kind = kind;
            nf.Aux = aux;
            nf.RetDst = dst;
            caller.Pc = resumePc;
            caller.PcExact = false;
            Push(nf);
            var jit = code.Jit;
            if (jit != null && ClrDepth < MaxJitClrDepth)
            {
                int st = InvokeJit(nf, jit);
                if (st == 0) return PopFrame(Result, baseDepth) ? 3 : 1;
                if (st == 1) return 2;
                return 1;
            }
            if (_jitOn && ++code.Hot > Options.JitThreshold && code.JitState == 0) Jit.Request(code, this);
            if (Interrupt && TrySuspend()) return 2;
            return 1;
        }

        internal PyGenerator NewGenerator(Frame nf, PyFunction pf)
        {
            var g = new PyGenerator(nf, pf.Name);
            nf.Aux = g;
            nf.Kind = FK.GenNested;
            return g;
        }

        // ------------------------------------------------------------ nested (synchronous) calls
        internal Value CallValue(Value fn, ReadOnlySpan<Value> args, KwArgs kw)
        {
            if (kw == null || kw.Count == 0) return CallValueKw(fn, args, null);
            var all = new Value[args.Length + kw.Count];
            args.CopyTo(all);
            kw.Values.CopyTo(all, args.Length);
            return CallValueKw(fn, all, kw.Names);
        }

        internal Value CallValueKw(Value fn, ReadOnlySpan<Value> args, string[] kwn)
        {
            switch (fn.O)
            {
                case PyFunction pf: return CallPyNested(pf, default, false, args, kwn, FK.Normal, null);
                case BoundMethod bm:
                    if (bm.Func.O is PyFunction bpf) return CallPyNested(bpf, bm.Self, true, args, kwn, FK.Normal, null);
                    {
                        var all = new Value[args.Length + 1];
                        all[0] = bm.Self;
                        args.CopyTo(new Span<Value>(all, 1, args.Length));
                        return CallValueKw(bm.Func, all, kwn);
                    }
                case BuiltinFunction bf:
                    return kwn == null ? bf.Fn(this, args, null) : bf.Fn(this, args.Slice(0, args.Length - kwn.Length), MakeKw(args, kwn));
                case BuiltinMethod bmm:
                    return kwn == null ? bmm.Fn(this, bmm.Self, args, null) : bmm.Fn(this, bmm.Self, args.Slice(0, args.Length - kwn.Length), MakeKw(args, kwn));
                case PyType t:
                    return kwn == null ? t.Ctor(this, args, null) : t.Ctor(this, args.Slice(0, args.Length - kwn.Length), MakeKw(args, kwn));
                case PyClass cls:
                    {
                        var inst = Builtins.NewInstance(cls);
                        if (cls.Lookup("__init__", out var init) && init.O is PyFunction ipf)
                        {
                            CallPyNested(ipf, Value.Obj(inst), true, args, kwn, FK.Init, inst);
                            return Value.Obj(inst);
                        }
                        return Instantiate(cls, inst, args, kwn);
                    }
                case PyInstance inst:
                    if (inst.Class.Lookup("__call__", out var call))
                    {
                        var all = new Value[args.Length + 1];
                        all[0] = fn;
                        args.CopyTo(new Span<Value>(all, 1, args.Length));
                        return CallValueKw(call, all, kwn);
                    }
                    break;
            }
            if (Interop.IsHost(fn)) return Interop.Invoke(this, fn.O, args, kwn);
            throw Err.Type("'" + Ops.TypeName(fn) + "' object is not callable");
        }

        static KwArgs MakeKw(ReadOnlySpan<Value> args, string[] kwn) =>
            new KwArgs(kwn, args.Slice(args.Length - kwn.Length).ToArray());

        Value Instantiate(PyClass cls, PyInstance inst, ReadOnlySpan<Value> args, string[] kwn)
        {
            if (cls.Lookup("__init__", out var init))
            {
                var all = new Value[args.Length + 1];
                all[0] = Value.Obj(inst);
                args.CopyTo(new Span<Value>(all, 1, args.Length));
                var rv = CallValueKw(init, all, kwn);
                if (!rv.IsNone) throw Err.Type("__init__() should return None");
            }
            else if (args.Length > 0) throw Err.Type(cls.Name + "() takes no arguments");
            return Value.Obj(inst);
        }

        internal Value CallPyNested(PyFunction pf, Value self, bool hasSelf, ReadOnlySpan<Value> args, string[] kwn, FK kind, object aux)
        {
            var nf = NewFrame(pf);
            Bind(nf, pf, self, hasSelf, args, kwn);
            if (pf.Code.IsGenerator) return Value.Obj(NewGenerator(nf, pf));
            nf.Kind = kind;
            nf.Aux = aux;
            nf.RetDst = -1;
            return RunNested(nf);
        }

        /// <summary>Runs a freshly pushed frame to completion on the CLR stack (no suspension inside).</summary>
        internal Value RunNested(Frame nf)
        {
            if (ClrDepth >= MaxClrDepth) throw Err.Recursion();
            int baseDepth = Sp;
            Push(nf);
            NoSuspend++;
            ClrDepth++;
            var prev = Current;
            Current = this;
            try
            {
                var jit = nf.Code.Jit;
                if (jit != null && ClrDepth < MaxJitClrDepth)
                {
                    int st;
                    try { st = jit(this, nf); }
                    catch (Exception ex)
                    {
                        var se = ToScriptException(ex);
                        HandleException(se, baseDepth);
                        if (ReferenceEquals(se, ex)) throw;
                        throw se;
                    }
                    if (st == 0) { PopFrame(Result, baseDepth); return Result; }
                }
                Run(baseDepth);
                return Result;
            }
            finally
            {
                NoSuspend--;
                ClrDepth--;
                Current = prev;
            }
        }

        internal Value CallMethod(Value obj, string name, ReadOnlySpan<Value> args)
        {
            if (obj.O is PyInstance inst && !inst.Attrs.ContainsKey(name) && inst.Class.Lookup(name, out var cv) && cv.O is PyFunction pf)
                return CallPyNested(pf, obj, true, args, null, FK.Normal, null);
            return CallValue(Ops.GetAttr(obj, name), args, null);
        }

        internal Value CallMethod(Value obj, string name, Value[] args) => CallMethod(obj, name, new ReadOnlySpan<Value>(args));

        /// <summary>Advances a generator synchronously. False when exhausted.</summary>
        internal bool GenStepNested(PyGenerator g, Value send, out Value v)
        {
            if (g.Done) { v = default; return false; }
            if (g.Running) throw Err.Value("generator already executing");
            var gf = g.Frame;
            if (!g.Started && !send.IsNone) throw Err.Type("can't send non-None value to a just-started generator");
            g.Started = true;
            g.Running = true;
            gf.Kind = FK.GenNested;
            if (gf.SendDst >= 0) gf.R[gf.SendDst] = send;
            if (ClrDepth >= MaxClrDepth) throw Err.Recursion();
            int baseDepth = Sp;
            Push(gf);
            NoSuspend++;
            ClrDepth++;
            Yielded = false;
            var prev = Current;
            Current = this;
            try
            {
                Run(baseDepth);
            }
            finally
            {
                NoSuspend--;
                ClrDepth--;
                Current = prev;
                g.Running = false;
            }
            if (Yielded) { v = Result; Yielded = false; return true; }
            v = default;
            return false;
        }

        // ------------------------------------------------------------ simple (non-control) instructions
        /// <summary>Executes one non-control-flow instruction on <paramref name="r"/>. Shared by the interpreter and the JIT fallback path.</summary>
        internal void ExecSimple(Frame f, Value[] r, int[] code, int pc)
        {
            var fc = f.Code;
            var k = fc.Consts;
            switch ((Op)code[pc])
            {
                case Op.NOP: break;
                case Op.MOVE: r[code[pc + 1]] = r[code[pc + 2]]; break;
                case Op.LOADK: r[code[pc + 1]] = k[code[pc + 2]]; break;
                case Op.LOADGLOBAL:
                    {
                        var cells = f.Func.GCells;
                        int g = code[pc + 2];
                        var c = cells[g] ?? (cells[g] = f.Func.Module.GetCell(fc.GlobalNames[g]));
                        var v = c.V;
                        if (v.IsUnbound) { v = c.Builtin; if (v.IsUnbound) throw Err.Name(c.Name); }
                        r[code[pc + 1]] = v;
                        break;
                    }
                case Op.STOREGLOBAL:
                    {
                        var cells = f.Func.GCells;
                        int g = code[pc + 1];
                        var c = cells[g] ?? (cells[g] = f.Func.Module.GetCell(fc.GlobalNames[g]));
                        c.V = r[code[pc + 2]];
                        break;
                    }
                case Op.DELGLOBAL:
                    {
                        var c = f.Func.Module.GetCell(fc.GlobalNames[code[pc + 1]]);
                        if (c.V.IsUnbound) throw Err.Name(c.Name);
                        c.V = default;
                        break;
                    }
                case Op.LOADDEREF:
                    {
                        var v = f.Func.Closure[code[pc + 2]].V;
                        if (v.IsUnbound) throw new ScriptException(Builtins.NameError, "free variable '" + fc.FreeNames[code[pc + 2]] + "' referenced before assignment");
                        r[code[pc + 1]] = v;
                        break;
                    }
                case Op.STOREDEREF: f.Func.Closure[code[pc + 1]].V = r[code[pc + 2]]; break;
                case Op.LOADCELL:
                    {
                        var v = ((Cell)r[code[pc + 2]].O).V;
                        if (v.IsUnbound) throw Err.Unbound(CellName(fc, code[pc + 2]));
                        r[code[pc + 1]] = v;
                        break;
                    }
                case Op.STORECELL: ((Cell)r[code[pc + 1]].O).V = r[code[pc + 2]]; break;
                case Op.MKCELL: r[code[pc + 1]] = Value.Obj(new Cell(r[code[pc + 1]])); break;
                case Op.DELCELL:
                    {
                        var c = (Cell)r[code[pc + 1]].O;
                        if (c.V.IsUnbound) throw Err.Unbound(CellName(fc, code[pc + 1]));
                        c.V = default;
                        break;
                    }
                case Op.LOADNAME:
                    {
                        string name = fc.Names[code[pc + 2]];
                        if (f.Ns != null && f.Ns.TryGetValue(name, out var v)) { r[code[pc + 1]] = v; break; }
                        var c = f.Func.Module.GetCell(name);
                        v = c.V.IsUnbound ? c.Builtin : c.V;
                        if (v.IsUnbound) throw Err.Name(name);
                        r[code[pc + 1]] = v;
                        break;
                    }
                case Op.STORENAME:
                    {
                        string name = fc.Names[code[pc + 1]];
                        if (f.Ns != null) f.Ns[name] = r[code[pc + 2]];
                        else f.Func.Module.GetCell(name).V = r[code[pc + 2]];
                        break;
                    }
                case Op.DELNAME:
                    {
                        string name = fc.Names[code[pc + 1]];
                        if (f.Ns == null || !f.Ns.Remove(name)) throw Err.Name(name);
                        break;
                    }
                case Op.CHKBOUND:
                    if (r[code[pc + 1]].IsUnbound) throw Err.Unbound(fc.Names[code[pc + 2]]);
                    break;
                case Op.DELLOCAL: r[code[pc + 1]] = default; break;
                case Op.CHECKTYPE: r[code[pc + 1]] = CheckType(r[code[pc + 1]], code[pc + 2]); break;

                case Op.ADD: case Op.SUB: case Op.MUL: case Op.TRUEDIV: case Op.FLOORDIV: case Op.MOD: case Op.POW:
                case Op.LSHIFT: case Op.RSHIFT: case Op.BITAND: case Op.BITOR: case Op.BITXOR: case Op.MATMUL: case Op.IADD:
                case Op.LT: case Op.LE: case Op.GT: case Op.GE: case Op.EQ: case Op.NE: case Op.IN: case Op.NOTIN: case Op.IS: case Op.ISNOT:
                    r[code[pc + 1]] = Ops.Binary((Op)code[pc], RK(r, k, code[pc + 2]), RK(r, k, code[pc + 3]));
                    break;
                case Op.NEG: r[code[pc + 1]] = Ops.Neg(r[code[pc + 2]]); break;
                case Op.POS: r[code[pc + 1]] = Ops.Pos(r[code[pc + 2]]); break;
                case Op.NOT: r[code[pc + 1]] = Value.Bool(!Ops.Truthy(r[code[pc + 2]])); break;
                case Op.INVERT: r[code[pc + 1]] = Ops.Invert(r[code[pc + 2]]); break;

                case Op.GETATTR: r[code[pc + 1]] = Ops.GetAttr(r[code[pc + 2]], fc.Names[code[pc + 3]]); break;
                case Op.SETATTR: Ops.SetAttr(r[code[pc + 1]], fc.Names[code[pc + 2]], r[code[pc + 3]]); break;
                case Op.DELATTR: Ops.DelAttr(r[code[pc + 1]], fc.Names[code[pc + 2]]); break;
                case Op.GETITEM: r[code[pc + 1]] = Ops.GetItem(r[code[pc + 2]], r[code[pc + 3]]); break;
                case Op.SETITEM: Ops.SetItem(r[code[pc + 1]], r[code[pc + 2]], r[code[pc + 3]]); break;
                case Op.DELITEM: Ops.DelItem(r[code[pc + 1]], r[code[pc + 2]]); break;
                case Op.SLICE:
                    {
                        int lo = code[pc + 2], hi = code[pc + 3], st = code[pc + 4];
                        r[code[pc + 1]] = Value.Obj(new PySlice(lo >= 0 ? r[lo] : Value.None, hi >= 0 ? r[hi] : Value.None, st >= 0 ? r[st] : Value.None));
                        break;
                    }

                case Op.BUILDLIST:
                    {
                        int b = code[pc + 2], n = code[pc + 3];
                        var l = new List<Value>(Math.Max(n, 4));
                        for (int i = 0; i < n; i++) l.Add(r[b + i]);
                        r[code[pc + 1]] = Value.Obj(new PyList(l));
                        break;
                    }
                case Op.BUILDTUPLE:
                    {
                        int b = code[pc + 2], n = code[pc + 3];
                        if (n == 0) { r[code[pc + 1]] = Value.Obj(PyTuple.Empty); break; }
                        var a = new Value[n];
                        Array.Copy(r, b, a, 0, n);
                        r[code[pc + 1]] = Value.Obj(new PyTuple(a));
                        break;
                    }
                case Op.BUILDSET:
                    {
                        int b = code[pc + 2], n = code[pc + 3];
                        var s = new PySet();
                        for (int i = 0; i < n; i++) s.Items.Add(r[b + i]);
                        r[code[pc + 1]] = Value.Obj(s);
                        break;
                    }
                case Op.BUILDDICT:
                    {
                        int b = code[pc + 2], n = code[pc + 3];
                        var d = new PyDict(n);
                        for (int i = 0; i < n; i++) d.Set(r[b + 2 * i], r[b + 2 * i + 1]);
                        r[code[pc + 1]] = Value.Obj(d);
                        break;
                    }
                case Op.LISTAPPEND: ((PyList)r[code[pc + 1]].O).Items.Add(r[code[pc + 2]]); break;
                case Op.SETADD: ((PySet)r[code[pc + 1]].O).Items.Add(r[code[pc + 2]]); break;
                case Op.DICTSET: ((PyDict)r[code[pc + 1]].O).Set(r[code[pc + 2]], r[code[pc + 3]]); break;
                case Op.LISTEXTEND: ((PyList)r[code[pc + 1]].O).Items.AddRange(Ops.ToList(r[code[pc + 2]])); break;
                case Op.SETUPDATE: foreach (var x in Ops.Iterate(r[code[pc + 2]])) ((PySet)r[code[pc + 1]].O).Items.Add(x); break;
                case Op.DICTUPDATE:
                    {
                        var dst = (PyDict)r[code[pc + 1]].O;
                        var src = r[code[pc + 2]];
                        if (src.O is PyDict sd) foreach (var kv in sd.Items()) dst.Set(kv.Key, kv.Value);
                        else throw Err.Type("'" + Ops.TypeName(src) + "' object is not a mapping");
                        break;
                    }
                case Op.LISTTOTUPLE: r[code[pc + 1]] = Value.Obj(new PyTuple(((PyList)r[code[pc + 2]].O).Items.ToArray())); break;
                case Op.UNPACK:
                    {
                        int b = code[pc + 1], n = code[pc + 2];
                        Value src = r[code[pc + 3]];
                        if (src.O is PyTuple t && t.Items.Length == n) { Array.Copy(t.Items, 0, r, b, n); break; }
                        if (src.O is PyList l && l.Items.Count == n) { l.Items.CopyTo(0, r, b, n); break; }
                        var items = Ops.ToList(src);
                        if (items.Count != n)
                        {
                            if (items.Count > n) throw Err.Value("too many values to unpack (expected " + n + ")");
                            throw Err.Value("not enough values to unpack (expected " + n + ", got " + items.Count + ")");
                        }
                        items.CopyTo(0, r, b, n);
                        break;
                    }
                case Op.UNPACKEX:
                    {
                        int b = code[pc + 1], before = code[pc + 2], after = code[pc + 3];
                        var items = Ops.ToList(r[code[pc + 4]]);
                        if (items.Count < before + after)
                            throw Err.Value("not enough values to unpack (expected at least " + (before + after) + ", got " + items.Count + ")");
                        for (int i = 0; i < before; i++) r[b + i] = items[i];
                        int mid = items.Count - before - after;
                        r[b + before] = Value.Obj(new PyList(items.GetRange(before, mid)));
                        for (int i = 0; i < after; i++) r[b + before + 1 + i] = items[before + mid + i];
                        break;
                    }
                case Op.GETITER: r[code[pc + 1]] = Value.Obj(Ops.GetIter(r[code[pc + 2]])); break;

                case Op.MAKEFUNC:
                    {
                        var child = fc.Children[code[pc + 2]];
                        var pf = MakeFunction(f, child);
                        int db = code[pc + 3], nd = code[pc + 4], kb = code[pc + 5], nk = code[pc + 6];
                        if (nd > 0) { pf.Defaults = new Value[nd]; Array.Copy(r, db, pf.Defaults, 0, nd); }
                        if (nk > 0) { pf.KwDefaults = new Value[nk]; Array.Copy(r, kb, pf.KwDefaults, 0, nk); }
                        r[code[pc + 1]] = Value.Obj(pf);
                        break;
                    }
                case Op.SUPER:
                    {
                        var owner = f.Func.Owner ?? throw Err.Runtime("super(): __class__ cell not found");
                        r[code[pc + 1]] = Value.Obj(new PySuper(owner, r[code[pc + 2]]));
                        break;
                    }
                case Op.RAISE: throw MakeRaise(r[code[pc + 1]], default);
                case Op.RAISEFROM: throw MakeRaise(r[code[pc + 1]], r[code[pc + 2]]);
                case Op.RERAISE:
                    {
                        int s = code[pc + 1];
                        if (s < 0 || r[s].IsUnbound) throw Err.Runtime("No active exception to reraise");
                        var ev = r[s];
                        if (ev.O is PyInstance pi && pi.Clr != null) throw pi.Clr;
                        throw new ScriptException(ev);
                    }
                case Op.EXCMATCH: r[code[pc + 1]] = Value.Bool(ExcMatch(r[code[pc + 2]], r[code[pc + 3]])); break;
                case Op.FORMAT:
                    {
                        int sp = code[pc + 4];
                        r[code[pc + 1]] = Value.Str(Format.Convert(r[code[pc + 2]], code[pc + 3], sp >= 0 ? Ops.Str(r[sp]) : null));
                        break;
                    }
                case Op.BUILDSTR:
                    {
                        int b = code[pc + 2], n = code[pc + 3];
                        var sb = new StringBuilder();
                        for (int i = 0; i < n; i++) sb.Append(Ops.Str(r[b + i]));
                        r[code[pc + 1]] = Value.Str(sb.ToString());
                        break;
                    }
                case Op.IMPORT:
                    r[code[pc + 1]] = Builtins.Import(fc.Names[code[pc + 2]], this);
                    break;
                case Op.IMPORTFROM:
                    {
                        var m = r[code[pc + 2]];
                        string name = fc.Names[code[pc + 3]];
                        if (!Ops.TryGetAttr(m, name, out var v)) throw Err.Import("cannot import name '" + name + "'");
                        r[code[pc + 1]] = v;
                        break;
                    }
                case Op.ASSERTFAIL:
                    {
                        int s = code[pc + 1];
                        throw s >= 0 ? new ScriptException(Value.Obj(Builtins.NewExceptionArgs(Builtins.AssertionError, r[s])))
                                     : new ScriptException(Value.Obj(Builtins.NewExceptionArgs(Builtins.AssertionError)));
                    }
                default:
                    throw new InvalidOperationException("ExecSimple: unexpected opcode " + (Op)code[pc]);
            }
        }

        internal static Value CheckType(Value v, int t)
        {
            if (t == 1)
            {
                if (v.IsInt) return v;
                if (v.IsBool) return Value.Int(v.N);
                if (v.O is BigInteger) throw Err.Overflow("value does not fit a typed 'int' variable (64-bit)");
                throw Err.Type("expected 'int', got '" + Ops.TypeName(v) + "'");
            }
            if (v.IsFloat) return v;
            if (v.IsInt || v.IsBool) return Value.Float(v.N);
            if (v.O is BigInteger b) return Value.Float((double)b);
            throw Err.Type("expected 'float', got '" + Ops.TypeName(v) + "'");
        }

        ScriptException MakeRaise(Value v, Value cause)
        {
            if (v.O is PyClass cls)
            {
                if (!cls.IsException) throw Err.Type("exceptions must derive from BaseException");
                v = CallValue(v, ReadOnlySpan<Value>.Empty, null);
            }
            if (!(v.O is PyInstance inst) || !inst.Class.IsException) throw Err.Type("exceptions must derive from BaseException");
            if (!cause.IsUnbound)
            {
                if (cause.O is PyClass cc) cause = CallValue(cause, ReadOnlySpan<Value>.Empty, null);
                inst.Attrs["__cause__"] = cause;
            }
            var se = new ScriptException(v);
            inst.Clr = se;
            return se;
        }

        internal static bool ExcMatch(Value exc, Value t)
        {
            if (t.O is PyTuple tu)
            {
                foreach (var x in tu.Items) if (ExcMatch(exc, x)) return true;
                return false;
            }
            if (!(t.O is PyClass cls) || !cls.IsException)
                throw Err.Type("catching classes that do not inherit from BaseException is not allowed");
            return exc.O is PyInstance inst && inst.Class.IsSubclassOf(cls);
        }
    }
}
