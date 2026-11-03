using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace DynLan2
{
    internal delegate Value BuiltinFn(VM vm, ReadOnlySpan<Value> args, KwArgs kw);
    internal delegate Value MethodFn(VM vm, Value self, ReadOnlySpan<Value> args, KwArgs kw);

    public sealed class KwArgs
    {
        public readonly string[] Names;
        public readonly Value[] Values;

        public KwArgs(string[] names, Value[] values) { Names = names; Values = values; }

        public int Count => Names.Length;

        public bool TryGet(string name, out Value v)
        {
            for (int i = 0; i < Names.Length; i++)
                if (Names[i] == name) { v = Values[i]; return true; }
            v = default;
            return false;
        }

        public Value Get(string name, Value def) => TryGet(name, out var v) ? v : def;

        internal void CheckOnly(string fn, params string[] allowed)
        {
            foreach (var n in Names)
                if (Array.IndexOf(allowed, n) < 0)
                    throw Err.Type(fn + "() got an unexpected keyword argument '" + n + "'");
        }
    }

    // ------------------------------------------------------------------ containers

    public sealed class PyList
    {
        internal List<Value> Items;
        public PyList() { Items = new List<Value>(); }
        public PyList(int capacity) { Items = new List<Value>(capacity); }
        internal PyList(List<Value> items) { Items = items; }
        public int Count => Items.Count;
        public Value this[int i] { get => Items[i]; set => Items[i] = value; }
    }

    public sealed class PyTuple
    {
        internal static readonly PyTuple Empty = new PyTuple(Array.Empty<Value>());
        internal Value[] Items;
        internal PyTuple(Value[] items) { Items = items; }
        public int Count => Items.Length;
        public Value this[int i] => Items[i];
    }

    /// <summary>Insertion-ordered dictionary (Python 3.7+ semantics).</summary>
    public sealed class PyDict
    {
        internal Dictionary<Value, int> Index;
        internal Value[] Keys;
        internal Value[] Vals;
        internal int Used;     // entries incl. deleted
        internal int Version;  // bumped on structural change

        public PyDict(int capacity = 4)
        {
            Index = new Dictionary<Value, int>(capacity, ValueEq.Instance);
            Keys = new Value[Math.Max(capacity, 4)];
            Vals = new Value[Math.Max(capacity, 4)];
        }

        public int Count => Index.Count;

        public bool TryGet(Value key, out Value v)
        {
            if (Index.TryGetValue(key, out int i)) { v = Vals[i]; return true; }
            v = default;
            return false;
        }

        public bool ContainsKey(Value key) => Index.ContainsKey(key);

        public void Set(Value key, Value v)
        {
            if (Index.TryGetValue(key, out int i)) { Vals[i] = v; return; }
            if (Used == Keys.Length) Grow();
            Keys[Used] = key;
            Vals[Used] = v;
            Index[key] = Used;
            Used++;
            Version++;
        }

        public bool Remove(Value key, out Value old)
        {
            if (!Index.TryGetValue(key, out int i)) { old = default; return false; }
            old = Vals[i];
            Index.Remove(key);
            Keys[i] = default;
            Vals[i] = default;
            Version++;
            if (Used > 16 && Index.Count * 2 < Used) Compact();
            return true;
        }

        public void Clear()
        {
            Index.Clear();
            Array.Clear(Keys, 0, Used);
            Array.Clear(Vals, 0, Used);
            Used = 0;
            Version++;
        }

        void Grow()
        {
            if (Index.Count * 2 < Used) { Compact(); if (Used < Keys.Length) return; }
            Array.Resize(ref Keys, Keys.Length * 2);
            Array.Resize(ref Vals, Vals.Length * 2);
        }

        void Compact()
        {
            int j = 0;
            for (int i = 0; i < Used; i++)
            {
                if (Keys[i].IsUnbound) continue;
                Keys[j] = Keys[i];
                Vals[j] = Vals[i];
                Index[Keys[j]] = j;
                j++;
            }
            for (int i = j; i < Used; i++) { Keys[i] = default; Vals[i] = default; }
            Used = j;
        }

        public IEnumerable<KeyValuePair<Value, Value>> Items()
        {
            for (int i = 0; i < Used; i++)
                if (!Keys[i].IsUnbound)
                    yield return new KeyValuePair<Value, Value>(Keys[i], Vals[i]);
        }

        public Value this[string key]
        {
            get => TryGet(Value.Str(key), out var v) ? v : throw Err.Key(Value.Str(key));
            set => Set(Value.Str(key), value);
        }

        internal PyDict Copy()
        {
            var d = new PyDict(Count);
            foreach (var kv in Items()) d.Set(kv.Key, kv.Value);
            return d;
        }
    }

    public sealed class PySet
    {
        internal HashSet<Value> Items;
        internal bool Frozen;
        public PySet() { Items = new HashSet<Value>(ValueEq.Instance); }
        internal PySet(IEnumerable<Value> items) { Items = new HashSet<Value>(items, ValueEq.Instance); }
        public int Count => Items.Count;
    }

    // ------------------------------------------------------------------ functions & classes

    public sealed class Cell
    {
        internal Value V;
        internal Cell() { }
        internal Cell(Value v) { V = v; }
    }

    internal sealed class GlobalCell
    {
        internal readonly string Name;
        internal Value V;
        internal Value Builtin;
        internal GlobalCell(string name, Value builtin) { Name = name; Builtin = builtin; }
    }

    public sealed class PyFunction
    {
        internal FunctionCode Code;
        internal ModuleInstance Module;
        internal Value[] Defaults;     // for the last N positional params
        internal Value[] KwDefaults;   // aligned with kw-only params (Unbound = required)
        internal Cell[] Closure;
        internal PyClass Owner;
        internal GlobalCell[] GCells;
        internal Dictionary<string, Value> Attrs;
        public string Name;

        internal PyFunction(FunctionCode code, ModuleInstance module)
        {
            Code = code;
            Module = module;
            Name = code.Name;
            GCells = code.GlobalNames.Length == 0 ? Array.Empty<GlobalCell>() : new GlobalCell[code.GlobalNames.Length];
        }
    }

    public sealed class PyClass
    {
        internal static int Stamp;

        public string Name;
        internal PyClass[] Bases;
        internal PyClass[] Mro;
        internal Dictionary<string, Value> Dict = new Dictionary<string, Value>(StringComparer.Ordinal);
        internal bool IsException;
        internal bool IsBuiltin;   // built-in exception classes / object (serialized by name)

        Dictionary<string, Value> _cache;
        int _cacheStamp = -1;
        int _plainStamp = -1;
        bool _plain;

        /// <summary>True when attribute assignment needs no hooks (no __setattr__, no properties/descriptors in the MRO).</summary>
        internal bool PlainSetAttr
        {
            get
            {
                if (_plainStamp == Stamp) return _plain;
                bool plain = true;
                foreach (var c in Mro)
                {
                    if (c.Dict.ContainsKey("__setattr__")) { plain = false; break; }
                    foreach (var v in c.Dict.Values)
                        if (v.O is PyProperty) { plain = false; break; }
                    if (!plain) break;
                }
                _plain = plain;
                _plainStamp = Stamp;
                return plain;
            }
        }

        internal PyClass(string name, PyClass[] bases)
        {
            Name = name;
            Bases = bases;
            Mro = ComputeMro(this, bases);
            foreach (var b in bases) if (b.IsException) IsException = true;
        }

        internal bool Lookup(string name, out Value v)
        {
            if (_cacheStamp != Stamp) { _cache?.Clear(); _cacheStamp = Stamp; }
            if (_cache != null && _cache.TryGetValue(name, out v)) return !v.IsUnbound;
            v = default;
            bool found = false;
            foreach (var c in Mro)
                if (c.Dict.TryGetValue(name, out v)) { found = true; break; }
            (_cache ??= new Dictionary<string, Value>(StringComparer.Ordinal))[name] = found ? v : default;
            return found;
        }

        internal bool LookupAfter(PyClass start, string name, out Value v)
        {
            int i = Array.IndexOf(Mro, start);
            for (int k = i + 1; k < Mro.Length; k++)
                if (Mro[k].Dict.TryGetValue(name, out v)) return true;
            v = default;
            return false;
        }

        internal void Set(string name, Value v)
        {
            Dict[name] = v;
            Interlocked.Increment(ref Stamp);
        }

        internal bool IsSubclassOf(PyClass other) => Array.IndexOf(Mro, other) >= 0;

        internal static PyClass[] ComputeMro(PyClass cls, PyClass[] bases)
        {
            // C3 linearization
            var seqs = new List<List<PyClass>>();
            foreach (var b in bases) seqs.Add(new List<PyClass>(b.Mro));
            seqs.Add(new List<PyClass>(bases));
            var result = new List<PyClass> { cls };
            while (true)
            {
                seqs.RemoveAll(s => s.Count == 0);
                if (seqs.Count == 0) break;
                PyClass cand = null;
                foreach (var s in seqs)
                {
                    var head = s[0];
                    bool inTail = false;
                    foreach (var s2 in seqs)
                        if (s2.IndexOf(head) > 0) { inTail = true; break; }
                    if (!inTail) { cand = head; break; }
                }
                if (cand == null) throw Err.Type("Cannot create a consistent method resolution order (MRO)");
                result.Add(cand);
                foreach (var s in seqs) if (s.Count > 0 && s[0] == cand) s.RemoveAt(0);
            }
            return result.ToArray();
        }
    }

    public sealed class PyInstance
    {
        internal PyClass Class;
        internal Dictionary<string, Value> Attrs = new Dictionary<string, Value>(StringComparer.Ordinal);
        internal ScriptException Clr;   // for exception instances: the CLR exception carrying the traceback
        internal PyInstance(PyClass cls) { Class = cls; }
    }

    internal sealed class BoundMethod
    {
        internal Value Self;
        internal Value Func;
        internal BoundMethod(Value self, Value func) { Self = self; Func = func; }
    }

    internal sealed class BuiltinFunction
    {
        internal readonly string Name;
        internal readonly string Key;   // unique registry key, used by snapshots
        internal readonly BuiltinFn Fn;
        internal BuiltinFunction(string key, string name, BuiltinFn fn) { Key = key; Name = name; Fn = fn; }
    }

    /// <summary>Method of a built-in type bound to its receiver, e.g. <c>[].append</c>.</summary>
    internal sealed class BuiltinMethod
    {
        internal Value Self;
        internal string Name;
        internal MethodFn Fn;
        internal BuiltinMethod(Value self, string name, MethodFn fn) { Self = self; Name = name; Fn = fn; }
    }

    /// <summary>Built-in type object: int, str, list ...</summary>
    internal sealed class PyType
    {
        internal readonly string Name;
        internal readonly BuiltinFn Ctor;
        internal readonly Func<Value, bool> Check;
        internal PyType(string name, BuiltinFn ctor, Func<Value, bool> check) { Name = name; Ctor = ctor; Check = check; }
    }

    internal sealed class PyStaticMethod { internal Value Fn; internal PyStaticMethod(Value f) { Fn = f; } }
    internal sealed class PyClassMethod { internal Value Fn; internal PyClassMethod(Value f) { Fn = f; } }

    internal sealed class PyProperty
    {
        internal Value Get, Set, Del;
        internal PyProperty(Value g, Value s, Value d) { Get = g; Set = s; Del = d; }
    }

    internal sealed class PySuper
    {
        internal PyClass Start;
        internal Value Self;
        internal PySuper(PyClass start, Value self) { Start = start; Self = self; }
    }

    internal sealed class PyRange
    {
        internal long Start, Stop, Step;
        internal PyRange(long start, long stop, long step) { Start = start; Stop = stop; Step = step; }

        internal long Length
        {
            get
            {
                if (Step > 0) return Start < Stop ? (Stop - Start - 1) / Step + 1 : 0;
                return Start > Stop ? (Start - Stop - 1) / (-Step) + 1 : 0;
            }
        }

        internal long At(long i) => Start + i * Step;

        internal bool Contains(long v)
        {
            if (Step > 0) { if (v < Start || v >= Stop) return false; }
            else { if (v > Start || v <= Stop) return false; }
            return (v - Start) % Step == 0;
        }
    }

    internal sealed class PySlice
    {
        internal Value Start, Stop, Step;
        internal PySlice(Value a, Value b, Value c) { Start = a; Stop = b; Step = c; }
    }

    /// <summary>Built-in module (math, time, random).</summary>
    internal sealed class PyModule
    {
        internal readonly string Name;
        internal readonly Dictionary<string, Value> Dict = new Dictionary<string, Value>(StringComparer.Ordinal);
        internal PyModule(string name) { Name = name; }
    }

    internal sealed class DictView
    {
        internal PyDict D;
        internal int Mode; // 0 keys, 1 values, 2 items
        internal DictView(PyDict d, int mode) { D = d; Mode = mode; }
    }

    /// <summary>Seedable, serializable random generator (xoshiro256**).</summary>
    internal sealed class PyRandom
    {
        internal ulong S0, S1, S2, S3;
        internal PyRandom(long seed) { Seed(seed); }

        internal void Seed(long seed)
        {
            ulong x = (ulong)seed;
            S0 = SplitMix(ref x); S1 = SplitMix(ref x); S2 = SplitMix(ref x); S3 = SplitMix(ref x);
        }

        static ulong SplitMix(ref ulong x)
        {
            ulong z = (x += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        internal ulong NextU64()
        {
            ulong result = Rotl(S1 * 5, 7) * 9;
            ulong t = S1 << 17;
            S2 ^= S0; S3 ^= S1; S1 ^= S2; S0 ^= S3; S2 ^= t; S3 = Rotl(S3, 45);
            return result;
        }

        internal double NextDouble() => (NextU64() >> 11) * (1.0 / (1UL << 53));

        internal long NextRange(long lo, long hiExclusive)
        {
            ulong span = (ulong)(hiExclusive - lo);
            if (span == 0) throw Err.Value("empty range for randrange()");
            return lo + (long)(NextU64() % span);
        }
    }

    // ------------------------------------------------------------------ iterators

    internal abstract class PyIterator
    {
        internal abstract bool Next(VM vm, out Value v);
    }

    internal sealed class ListIter : PyIterator
    {
        internal PyList L; internal int I;
        internal ListIter(PyList l) { L = l; }
        internal override bool Next(VM vm, out Value v)
        {
            if (I < L.Items.Count) { v = L.Items[I++]; return true; }
            v = default; return false;
        }
    }

    internal sealed class ArrayIter : PyIterator
    {
        internal Value[] A; internal int I;
        internal ArrayIter(Value[] a) { A = a; }
        internal override bool Next(VM vm, out Value v)
        {
            if (I < A.Length) { v = A[I++]; return true; }
            v = default; return false;
        }
    }

    internal sealed class StrIter : PyIterator
    {
        internal string S; internal int I;
        internal StrIter(string s) { S = s; }
        internal override bool Next(VM vm, out Value v)
        {
            if (I < S.Length)
            {
                if (char.IsHighSurrogate(S[I]) && I + 1 < S.Length) { v = Value.Str(S.Substring(I, 2)); I += 2; }
                else v = Value.Str(Builtins.CharStr(S[I++]));
                return true;
            }
            v = default; return false;
        }
    }

    internal sealed class RangeIter : PyIterator
    {
        internal long Cur, Stop, Step;
        internal RangeIter(long cur, long stop, long step) { Cur = cur; Stop = stop; Step = step; }
        internal override bool Next(VM vm, out Value v)
        {
            if (Step > 0 ? Cur < Stop : Cur > Stop) { v = Value.Int(Cur); Cur += Step; return true; }
            v = default; return false;
        }
    }

    internal sealed class DictIter : PyIterator
    {
        internal PyDict D; internal int I; internal int Mode; internal int StartCount;
        internal DictIter(PyDict d, int mode) { D = d; Mode = mode; StartCount = d.Count; }
        internal override bool Next(VM vm, out Value v)
        {
            if (D.Count != StartCount) throw Err.Runtime("dictionary changed size during iteration");
            while (I < D.Used)
            {
                int i = I++;
                if (D.Keys[i].IsUnbound) continue;
                v = Mode == 0 ? D.Keys[i] : Mode == 1 ? D.Vals[i] : Value.Obj(new PyTuple(new[] { D.Keys[i], D.Vals[i] }));
                return true;
            }
            v = default; return false;
        }
    }

    internal sealed class EnumerateIter : PyIterator
    {
        internal PyIterator Inner; internal long N;
        internal EnumerateIter(PyIterator inner, long start) { Inner = inner; N = start; }
        internal override bool Next(VM vm, out Value v)
        {
            if (Inner.Next(vm, out var x)) { v = Value.Obj(new PyTuple(new[] { Value.Int(N++), x })); return true; }
            v = default; return false;
        }
    }

    internal sealed class ZipIter : PyIterator
    {
        internal PyIterator[] Its;
        internal ZipIter(PyIterator[] its) { Its = its; }
        internal override bool Next(VM vm, out Value v)
        {
            if (Its.Length == 0) { v = default; return false; }
            var items = new Value[Its.Length];
            for (int i = 0; i < Its.Length; i++)
                if (!Its[i].Next(vm, out items[i])) { v = default; return false; }
            v = Value.Obj(new PyTuple(items));
            return true;
        }
    }

    internal sealed class MapIter : PyIterator
    {
        internal Value Fn; internal PyIterator[] Its;
        internal MapIter(Value fn, PyIterator[] its) { Fn = fn; Its = its; }
        internal override bool Next(VM vm, out Value v)
        {
            var args = new Value[Its.Length];
            for (int i = 0; i < Its.Length; i++)
                if (!Its[i].Next(vm, out args[i])) { v = default; return false; }
            v = vm.CallValue(Fn, args, null);
            return true;
        }
    }

    internal sealed class FilterIter : PyIterator
    {
        internal Value Fn; internal PyIterator It;
        internal FilterIter(Value fn, PyIterator it) { Fn = fn; It = it; }
        internal override bool Next(VM vm, out Value v)
        {
            while (It.Next(vm, out v))
            {
                bool ok = Fn.IsNone ? Ops.Truthy(v) : Ops.Truthy(vm.CallValue(Fn, new[] { v }, null));
                if (ok) return true;
            }
            return false;
        }
    }

    /// <summary>Iterator protocol implemented by a script class (__next__).</summary>
    internal sealed class InstanceIter : PyIterator
    {
        internal Value Obj;
        internal InstanceIter(Value obj) { Obj = obj; }
        internal override bool Next(VM vm, out Value v)
        {
            try
            {
                v = vm.CallMethod(Obj, "__next__", ReadOnlySpan<Value>.Empty);
                return true;
            }
            catch (ScriptException e) when (e.IsInstanceOf(Builtins.StopIteration))
            {
                v = default;
                return false;
            }
        }
    }

    internal sealed class HostIter : PyIterator
    {
        internal IEnumerator E;
        internal HostIter(IEnumerator e) { E = e; }
        internal override bool Next(VM vm, out Value v)
        {
            if (E.MoveNext()) { v = Interop.ToValue(E.Current); return true; }
            v = default; return false;
        }
    }

    internal sealed class PyGenerator : PyIterator
    {
        internal Frame Frame;
        internal bool Running;
        internal bool Done;
        internal bool Started;
        internal string Name;

        internal PyGenerator(Frame f, string name) { Frame = f; Name = name; }

        internal override bool Next(VM vm, out Value v) => vm.GenStepNested(this, Value.None, out v);
    }
}
