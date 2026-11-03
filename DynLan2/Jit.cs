using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using static System.Linq.Expressions.Expression;

namespace DynLan2
{
    /// <summary>
    /// Tier-2 compiler: register bytecode → LINQ expression tree → IL (RyuJIT).
    /// Registers become CLR locals (typed <c>long</c>/<c>double</c> when proven), so hot code runs without
    /// dispatch or boxing. At every safe point the compiled code can spill its locals back into the heap
    /// frame and return, after which the interpreter continues — so suspension and snapshots keep working.
    /// </summary>
    internal static class Jit
    {
        internal const int MaxCodeLength = 60000;

        internal static void Request(FunctionCode fc, VM vm)
        {
            if (fc.JitState != 0) return;
            if (vm.Options.Jit == JitMode.Off) return; // per-engine choice: never mark the shared FunctionCode
            if (Interlocked.CompareExchange(ref fc.JitState, 1, 0) != 0) return;
            if (!Eligible(fc)) { fc.JitState = 3; return; }
            if (vm.Options.Jit == JitMode.Eager) CompileNow(fc);
            else ThreadPool.UnsafeQueueUserWorkItem(_ => CompileNow(fc), null);
        }

        internal static bool Eligible(FunctionCode fc) =>
            fc.Compiled && !fc.IsGenerator && !fc.IsClassBody && !fc.HasHandlers && !fc.HasYield && !fc.HasClassDef
            && fc.Code.Length < MaxCodeLength;

        internal static Exception LastError;

        static void CompileNow(FunctionCode fc)
        {
            try
            {
                var fn = new JitCompiler(fc).Compile();
                fc.Jit = fn;
                fc.JitState = 2;
            }
            catch (Exception e)
            {
                LastError = e;
                fc.JitState = 3;
            }
        }
    }

    /// <summary>Runtime helpers called from JIT-compiled code.</summary>
    internal static class JitOps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Add(Value a, Value b)
        {
            if (a.IsInt && b.IsInt)
            {
                long x = a.N, y = b.N, s = x + y;
                if (((x ^ s) & (y ^ s)) >= 0) return Value.Int(s);
            }
            else if (a.IsFloat && b.IsFloat) return Value.Float(a.AsFloat + b.AsFloat);
            return Ops.Add(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value IAdd(Value a, Value b)
        {
            if (a.IsInt && b.IsInt)
            {
                long x = a.N, y = b.N, s = x + y;
                if (((x ^ s) & (y ^ s)) >= 0) return Value.Int(s);
            }
            else if (a.IsFloat && b.IsFloat) return Value.Float(a.AsFloat + b.AsFloat);
            return Ops.IAdd(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Sub(Value a, Value b)
        {
            if (a.IsInt && b.IsInt)
            {
                long x = a.N, y = b.N, s = x - y;
                if (((x ^ y) & (x ^ s)) >= 0) return Value.Int(s);
            }
            else if (a.IsFloat && b.IsFloat) return Value.Float(a.AsFloat - b.AsFloat);
            return Ops.Sub(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Mul(Value a, Value b)
        {
            if (a.IsInt && b.IsInt) return Ops.MulInt(a.N, b.N);
            if (a.IsFloat && b.IsFloat) return Value.Float(a.AsFloat * b.AsFloat);
            return Ops.Mul(a, b);
        }

        public static Value Mod(Value a, Value b) => a.IsInt && b.IsInt ? Value.Int(Ops.ModLong(a.N, b.N)) : Ops.Mod(a, b);
        public static Value FloorDiv(Value a, Value b) => Ops.FloorDiv(a, b);

        public static long FloorDivLL(long a, long b)
        {
            if (a == long.MinValue && b == -1) throw Err.Overflow("integer overflow in floor division");
            return Ops.FloorDivLong(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MulOv(long a, long b, out long r)
        {
            long hi = Math.BigMul(a, b, out r);
            return hi != (r >> 63);
        }

        public static double FloorDivDD(double a, double b)
        {
            if (b == 0) throw Err.ZeroDiv("float floor division by zero");
            return Math.Floor(a / b);
        }

        public static double ModDD(double a, double b)
        {
            if (b == 0) throw Err.ZeroDiv("float modulo");
            double r = a % b;
            if (r != 0 && ((r < 0) != (b < 0))) r += b;
            return r;
        }

        public static bool IsIntOrUnbound(Value v) => v.IsUnbound || v.IsInt;
        public static bool IsFloatOrUnbound(Value v) => v.IsUnbound || v.IsFloat;

        public static double DivDD(double a, double b, bool isFloat)
        {
            if (b == 0) throw Err.ZeroDiv(isFloat ? "float division by zero" : "division by zero");
            return a / b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CmpOp(int c, Value a, Value b)
        {
            if (a.IsInt && b.IsInt)
            {
                long x = a.N, y = b.N;
                switch ((Cmp)c)
                {
                    case Cmp.LT: return x < y;
                    case Cmp.LE: return x <= y;
                    case Cmp.GT: return x > y;
                    case Cmp.GE: return x >= y;
                    case Cmp.EQ: return x == y;
                    default: return x != y;
                }
            }
            return Ops.CompareOp((Cmp)c, a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Truthy(Value v) => Ops.Truthy(v);

        public static bool IsInt(Value v) => v.IsInt;
        public static bool IsUnbound(Value v) => v.IsUnbound;
        public static void ThrowUnbound(string name) => throw Err.Unbound(name);

        public static long LoadL(Value v) => v.IsUnbound ? 0 : v.IsInt || v.IsBool ? v.N : VM.CheckType(v, 1).N;
        public static double LoadD(Value v) => v.IsUnbound ? 0 : v.IsFloat ? v.AsFloat : VM.CheckType(v, 2).AsFloat;
        public static long ToLongTyped(Value v) => v.IsInt ? v.N : VM.CheckType(v, 1).N;
        public static double ToDoubleTyped(Value v) => v.IsFloat ? v.AsFloat : VM.CheckType(v, 2).AsFloat;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value LoadGlobal(Frame f, int g)
        {
            var c = f.Func.GCells[g] ?? (f.Func.GCells[g] = f.Func.Module.GetCell(f.Code.GlobalNames[g]));
            var v = c.V;
            if (v.IsUnbound) { v = c.Builtin; if (v.IsUnbound) throw Err.Name(c.Name); }
            return v;
        }

        public static void StoreGlobal(Frame f, int g, Value v)
        {
            var c = f.Func.GCells[g] ?? (f.Func.GCells[g] = f.Func.Module.GetCell(f.Code.GlobalNames[g]));
            c.V = v;
        }

        public static Value LoadDeref(Frame f, int i)
        {
            var v = f.Func.Closure[i].V;
            if (v.IsUnbound) throw new ScriptException(Builtins.NameError, "free variable '" + f.Code.FreeNames[i] + "' referenced before assignment");
            return v;
        }

        public static void StoreDeref(Frame f, int i, Value v) => f.Func.Closure[i].V = v;

        public static Value LoadCell(Value cell)
        {
            var v = ((Cell)cell.O).V;
            if (v.IsUnbound) throw Err.Unbound("<closure variable>");
            return v;
        }

        public static void StoreCell(Value cell, Value v) => ((Cell)cell.O).V = v;
        public static Value MkCell(Value v) => Value.Obj(new Cell(v));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value GetAttr(Value o, string name)
        {
            if (o.O is PyInstance inst && inst.Attrs.TryGetValue(name, out var v)) return v;
            return Ops.GetAttr(o, name);
        }

        public static void SetAttr(Value o, string name, Value v)
        {
            if (o.O is PyInstance inst && inst.Class.PlainSetAttr)
                inst.Attrs[name] = v;
            else Ops.SetAttr(o, name, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value GetItem(Value o, Value k)
        {
            if (o.O is PyList l && k.IsInt && (ulong)k.N < (ulong)l.Items.Count) return l.Items[(int)k.N];
            return Ops.GetItem(o, k);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value GetItemL(Value o, long k)
        {
            if (o.O is PyList l && (ulong)k < (ulong)l.Items.Count) return l.Items[(int)k];
            return Ops.GetItem(o, Value.Int(k));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetItem(Value o, Value k, Value v)
        {
            if (o.O is PyList l && k.IsInt && (ulong)k.N < (ulong)l.Items.Count) { l.Items[(int)k.N] = v; return; }
            Ops.SetItem(o, k, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetItemL(Value o, long k, Value v)
        {
            if (o.O is PyList l && (ulong)k < (ulong)l.Items.Count) { l.Items[(int)k] = v; return; }
            Ops.SetItem(o, Value.Int(k), v);
        }

        public static void ListAppend(Value l, Value v) => ((PyList)l.O).Items.Add(v);
        public static void DictSet(Value d, Value k, Value v) => ((PyDict)d.O).Set(k, v);
        public static void SetAdd(Value s, Value v) => ((PySet)s.O).Items.Add(v);
        public static Value BuildList(Value[] items) => Value.Obj(new PyList(new List<Value>(items)));
        public static Value BuildTuple(Value[] items) => Value.Obj(items.Length == 0 ? PyTuple.Empty : new PyTuple(items));

        public static Value[] Unpack(Value v, int n)
        {
            if (v.O is PyTuple t && t.Items.Length == n) return t.Items;
            var items = Ops.ToList(v);
            if (items.Count != n)
            {
                if (items.Count > n) throw Err.Value("too many values to unpack (expected " + n + ")");
                throw Err.Value("not enough values to unpack (expected " + n + ", got " + items.Count + ")");
            }
            return items.ToArray();
        }

        public static Value FormatV(Value v, int conv, Value spec) => Value.Str(Format.Convert(v, conv, spec.IsUnbound ? null : Ops.Str(spec)));

        public static Value BuildStr(Value[] parts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts) sb.Append(Ops.Str(p));
            return Value.Str(sb.ToString());
        }

        public static Value GetIter(Value v) => Value.Obj(Ops.GetIter(v));

        public static bool ForIter(VM vm, Value it, out Value v)
        {
            object o = it.O;
            if (o is ListIter li)
            {
                if (li.I < li.L.Items.Count) { v = li.L.Items[li.I++]; return true; }
                v = default;
                return false;
            }
            if (o is PyGenerator g) return vm.GenStepNested(g, Value.None, out v);
            return ((PyIterator)o).Next(vm, out v);
        }

        public static bool RangePrep(Value fn, Value a, Value b, Value c, int n, out long cur, out long stop, out long step)
        {
            cur = 0; stop = 0; step = 1;
            if (!ReferenceEquals(fn.O, Builtins.RangeType)) return false;
            if (!(a.IsInt || a.IsBool)) return false;
            if (n >= 2 && !(b.IsInt || b.IsBool)) return false;
            if (n == 3 && !(c.IsInt || c.IsBool)) return false;
            if (n == 1) stop = a.N;
            else { cur = a.N; stop = b.N; if (n == 3) step = c.N; }
            if (step == 0) throw Err.Value("range() arg 3 must not be zero");
            return true;
        }

        public static void NoteDeopt(FunctionCode fc)
        {
            if (Interlocked.Increment(ref fc.Deopts) > 4)
            {
                fc.Jit = null;
                fc.JitState = 3;
            }
        }

        /// <summary>
        /// Called every <c>budget</c> loop back-edges. With a step limit active it is called on every back-edge
        /// (exact accounting); otherwise only the interrupt flag is polled, every 1024 iterations.
        /// </summary>
        public static bool Safepoint(VM vm, ref int budget)
        {
            if (vm.Fuel != long.MaxValue)
            {
                budget = 1;
                if (vm.Interrupt | (--vm.Fuel <= 0)) return vm.TrySuspendInternal();
                return false;
            }
            budget = 1024;
            return vm.Interrupt && vm.TrySuspendInternal();
        }

        public static int Call(VM vm, Frame caller, Value fn, int argBase, int argc, string[] kwn, int resumePc, int dst)
        {
            try
            {
                return vm.JitInvoke(caller, fn, default, false, new ReadOnlySpan<Value>(vm.ArgStack, argBase, argc), kwn, resumePc, dst);
            }
            finally { vm.ArgSp = argBase; }
        }

        public static int CallMethod(VM vm, Frame caller, string name, int argBase, int argc, string[] kwn, int resumePc, int dst)
        {
            try
            {
                var all = new ReadOnlySpan<Value>(vm.ArgStack, argBase, argc);
                var obj = all[0];
                var args = all.Slice(1);
                if (obj.O is PyInstance inst)
                {
                    if (!inst.Attrs.ContainsKey(name) && inst.Class.Lookup(name, out var cv) && cv.O is PyFunction pf)
                        return vm.JitInvoke(caller, Value.Obj(pf), obj, true, args, kwn, resumePc, dst);
                }
                else if (!(obj.O is PyClass))
                {
                    var mf = Builtins.FindMethod(obj, name);
                    if (mf != null)
                    {
                        int np = args.Length - (kwn?.Length ?? 0);
                        vm.Result = mf(vm, obj, args.Slice(0, np), kwn == null ? null : new KwArgs(kwn, args.Slice(np).ToArray()));
                        return vm.AfterSync();
                    }
                }
                return vm.JitInvoke(caller, Ops.GetAttr(obj, name), default, false, args, kwn, resumePc, dst);
            }
            finally { vm.ArgSp = argBase; }
        }

        public static int CallEx(VM vm, Frame caller, Value fn, Value args, Value kw, int resumePc, int dst)
        {
            var list = Ops.ToList(args);
            string[] kwn = null;
            if (!kw.IsUnbound)
            {
                var kd = kw.O as PyDict ?? throw Err.Type("argument after ** must be a mapping");
                kwn = new string[kd.Count];
                int i = 0;
                foreach (var kv in kd.Items())
                {
                    kwn[i++] = kv.Key.O as string ?? throw Err.Type("keywords must be strings");
                    list.Add(kv.Value);
                }
            }
            return vm.JitInvoke(caller, fn, default, false, list.ToArray(), kwn, resumePc, dst);
        }
    }
}
