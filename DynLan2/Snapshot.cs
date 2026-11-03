using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DynLan2
{
    /// <summary>
    /// Binary serialization of a whole execution context: globals, heap-allocated call stack, generators,
    /// closures, iterators and user objects. Code is referenced by (script hash, code id) — compilation is
    /// deterministic, so the same source yields identical bytecode, registers and pcs on any machine.
    /// </summary>
    internal static class Snapshot
    {
        const string Magic = "DL2S";
        const int FormatVersion = 1;
        internal const int CompilerVersion = 2;

        enum K : byte
        {
            List = 1, Tuple, Dict, Set, Cell, Function, Class, BuiltinClass, Instance, BoundMethod, BuiltinFunction,
            BuiltinMethod, PyType, StaticMethod, ClassMethod, Property, Super, Range, Slice, Module, DictView, Frame,
            Generator, ListIter, ArrayIter, StrIter, RangeIter, DictIter, EnumerateIter, ZipIter, MapIter, FilterIter,
            InstanceIter, HostHandle, HostValue, Random, GetItemIter
        }

        enum V : byte { Unbound, None, Bool, Int, Float, Str, BigInt, Ref, NotImplemented }

        // ================================================================== save
        sealed class Writer
        {
            readonly ScriptContext _ctx;
            readonly Dictionary<object, int> _ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            readonly List<object> _objs = new List<object>();
            public Writer(ScriptContext ctx) { _ctx = ctx; }

            public int Id(object o)
            {
                if (_ids.TryGetValue(o, out int id)) return id;
                id = _objs.Count;
                _ids[o] = id;
                _objs.Add(o);
                return id;
            }

            public void Value(BinaryWriter w, Value v)
            {
                object o = v.O;
                if (o == null) { w.Write((byte)V.Unbound); return; }
                if (ReferenceEquals(o, DynLan2.Value.TNone)) { w.Write((byte)V.None); return; }
                if (ReferenceEquals(o, DynLan2.Value.TBool)) { w.Write((byte)V.Bool); w.Write(v.N != 0); return; }
                if (ReferenceEquals(o, DynLan2.Value.TInt)) { w.Write((byte)V.Int); w.Write(v.N); return; }
                if (ReferenceEquals(o, DynLan2.Value.TFloat)) { w.Write((byte)V.Float); w.Write(v.N); return; }
                if (ReferenceEquals(o, Builtins.NotImplementedObj)) { w.Write((byte)V.NotImplemented); return; }
                if (o is string s) { w.Write((byte)V.Str); w.Write(s); return; }
                if (o is BigInteger b) { w.Write((byte)V.BigInt); var bytes = b.ToByteArray(); w.Write(bytes.Length); w.Write(bytes); return; }
                w.Write((byte)V.Ref);
                w.Write(Id(o));
            }

            public void Ref(BinaryWriter w, object o) => w.Write(o == null ? -1 : Id(o));

            public void Values(BinaryWriter w, IList<Value> vs)
            {
                if (vs == null) { w.Write(-1); return; }
                w.Write(vs.Count);
                foreach (var v in vs) Value(w, v);
            }

            static void Str(BinaryWriter w, string s)
            {
                w.Write(s != null);
                if (s != null) w.Write(s);
            }

            void Map(BinaryWriter w, Dictionary<string, Value> d)
            {
                if (d == null) { w.Write(-1); return; }
                w.Write(d.Count);
                foreach (var kv in d) { w.Write(kv.Key); Value(w, kv.Value); }
            }

            public byte[] Save(bool includeSource)
            {
                var vm = _ctx.Vm;
                if (_ctx.IsRunning || vm.ClrDepth > 0 || vm.NoSuspend > 0) throw new SnapshotException("cannot save state while the script is running");
                var roots = new MemoryStream();
                var rw = new BinaryWriter(roots, Encoding.UTF8);
                // globals
                var cells = _ctx.Module.Order;
                rw.Write(cells.Count);
                foreach (var c in cells) { rw.Write(c.Name); Value(rw, c.V); }
                // stack
                rw.Write(vm.Sp);
                for (int i = 0; i < vm.Sp; i++) Ref(rw, vm.Frames[i]);
                rw.Write(_ctx.Started);
                rw.Write(_ctx.Completed);
                Ref(rw, vm.Random);

                // object bodies (the table grows while we write)
                var kinds = new List<byte>();
                var shell = new MemoryStream();
                var sw = new BinaryWriter(shell, Encoding.UTF8);
                var body = new MemoryStream();
                var bw = new BinaryWriter(body, Encoding.UTF8);
                for (int i = 0; i < _objs.Count; i++) WriteObject(_objs[i], kinds, sw, bw);

                var outMs = new MemoryStream();
                using (var z = new DeflateStream(outMs, CompressionLevel.Fastest, true))
                using (var w = new BinaryWriter(z, Encoding.UTF8))
                {
                    w.Write(FormatVersion);
                    w.Write(CompilerVersion);
                    w.Write(_ctx.Script.Name);
                    w.Write(_ctx.Script.Hash);
                    w.Write(includeSource);
                    if (includeSource) w.Write(_ctx.Script.Source);
                    w.Write(kinds.Count);
                    w.Write(kinds.ToArray());
                    w.Write((int)shell.Length); w.Write(shell.GetBuffer(), 0, (int)shell.Length);
                    w.Write((int)roots.Length); w.Write(roots.GetBuffer(), 0, (int)roots.Length);
                    w.Write((int)body.Length); w.Write(body.GetBuffer(), 0, (int)body.Length);
                }
                var result = new MemoryStream();
                result.Write(Encoding.ASCII.GetBytes(Magic), 0, 4);
                outMs.Position = 0;
                outMs.CopyTo(result);
                return result.ToArray();
            }

            void WriteObject(object o, List<byte> kinds, BinaryWriter sw, BinaryWriter w)
            {
                switch (o)
                {
                    case PyList l: kinds.Add((byte)K.List); Values(w, l.Items); break;
                    case PyTuple t: kinds.Add((byte)K.Tuple); sw.Write(t.Items.Length); Values(w, t.Items); break;
                    case PyDict d:
                        kinds.Add((byte)K.Dict);
                        w.Write(d.Count);
                        foreach (var kv in d.Items()) { Value(w, kv.Key); Value(w, kv.Value); }
                        break;
                    case PySet s:
                        kinds.Add((byte)K.Set);
                        w.Write(s.Frozen);
                        Values(w, s.Items.ToList());
                        break;
                    case Cell c: kinds.Add((byte)K.Cell); Value(w, c.V); break;
                    case PyFunction f:
                        kinds.Add((byte)K.Function);
                        CheckOwnCode(f.Code);
                        w.Write(f.Code.Key);
                        w.Write(f.Name);
                        Values(w, f.Defaults);
                        Values(w, f.KwDefaults);
                        w.Write(f.Closure?.Length ?? -1);
                        if (f.Closure != null) foreach (var c in f.Closure) Ref(w, c);
                        Ref(w, f.Owner);
                        Map(w, f.Attrs);
                        break;
                    case PyClass cls when cls.IsBuiltin:
                        kinds.Add((byte)K.BuiltinClass);
                        sw.Write(cls.Name);
                        break;
                    case PyClass cls:
                        kinds.Add((byte)K.Class);
                        w.Write(cls.Name);
                        w.Write(cls.Bases.Length);
                        foreach (var b in cls.Bases) Ref(w, b);
                        Map(w, cls.Dict);
                        break;
                    case PyInstance inst:
                        kinds.Add((byte)K.Instance);
                        Ref(w, inst.Class);
                        Map(w, inst.Attrs);
                        break;
                    case BoundMethod bm: kinds.Add((byte)K.BoundMethod); Value(w, bm.Self); Value(w, bm.Func); break;
                    case BuiltinFunction bf: kinds.Add((byte)K.BuiltinFunction); sw.Write(bf.Key); break;
                    case BuiltinMethod bmm: kinds.Add((byte)K.BuiltinMethod); Value(w, bmm.Self); w.Write(bmm.Name); break;
                    case PyType pt: kinds.Add((byte)K.PyType); sw.Write(pt.Name); break;
                    case PyStaticMethod sm: kinds.Add((byte)K.StaticMethod); Value(w, sm.Fn); break;
                    case PyClassMethod cm: kinds.Add((byte)K.ClassMethod); Value(w, cm.Fn); break;
                    case PyProperty p: kinds.Add((byte)K.Property); Value(w, p.Get); Value(w, p.Set); Value(w, p.Del); break;
                    case PySuper sp: kinds.Add((byte)K.Super); Ref(w, sp.Start); Value(w, sp.Self); break;
                    case PyRange r: kinds.Add((byte)K.Range); w.Write(r.Start); w.Write(r.Stop); w.Write(r.Step); break;
                    case PySlice sl: kinds.Add((byte)K.Slice); Value(w, sl.Start); Value(w, sl.Stop); Value(w, sl.Step); break;
                    case PyModule m:
                        kinds.Add((byte)K.Module);
                        if (!Builtins.Modules.ContainsKey(m.Name)) throw new SnapshotException("unknown module '" + m.Name + "'");
                        sw.Write(m.Name);
                        break;
                    case DictView dv: kinds.Add((byte)K.DictView); Ref(w, dv.D); w.Write(dv.Mode); break;
                    case Frame fr:
                        kinds.Add((byte)K.Frame);
                        CheckOwnCode(fr.Code);
                        w.Write(fr.Code.Key);
                        Ref(w, fr.Func);
                        Values(w, fr.R);
                        w.Write(fr.Pc);
                        w.Write(fr.PcExact);
                        w.Write(fr.RetDst);
                        w.Write((byte)fr.Kind);
                        w.Write(fr.ExitPc);
                        w.Write(fr.SendDst);
                        if (fr.Aux is ClassInfo ci)
                        {
                            w.Write((byte)2);
                            w.Write(ci.Name);
                            w.Write(ci.Bases.Length);
                            foreach (var b in ci.Bases) Ref(w, b);
                        }
                        else if (fr.Aux != null) { w.Write((byte)1); Ref(w, fr.Aux); }
                        else w.Write((byte)0);
                        Map(w, fr.Ns);
                        break;
                    case PyGenerator g:
                        kinds.Add((byte)K.Generator);
                        Ref(w, g.Frame); w.Write(g.Running); w.Write(g.Done); w.Write(g.Started); w.Write(g.Name ?? "");
                        break;
                    case ListIter li: kinds.Add((byte)K.ListIter); Ref(w, li.L); w.Write(li.I); break;
                    case ArrayIter ai: kinds.Add((byte)K.ArrayIter); Values(w, ai.A); w.Write(ai.I); break;
                    case StrIter si: kinds.Add((byte)K.StrIter); w.Write(si.S); w.Write(si.I); break;
                    case RangeIter ri: kinds.Add((byte)K.RangeIter); w.Write(ri.Cur); w.Write(ri.Stop); w.Write(ri.Step); break;
                    case DictIter di: kinds.Add((byte)K.DictIter); Ref(w, di.D); w.Write(di.I); w.Write(di.Mode); w.Write(di.StartCount); break;
                    case EnumerateIter ei: kinds.Add((byte)K.EnumerateIter); Ref(w, ei.Inner); w.Write(ei.N); break;
                    case ZipIter zi: kinds.Add((byte)K.ZipIter); w.Write(zi.Its.Length); foreach (var x in zi.Its) Ref(w, x); break;
                    case MapIter mi: kinds.Add((byte)K.MapIter); Value(w, mi.Fn); w.Write(mi.Its.Length); foreach (var x in mi.Its) Ref(w, x); break;
                    case FilterIter fi: kinds.Add((byte)K.FilterIter); Value(w, fi.Fn); Ref(w, fi.It); break;
                    case InstanceIter ii: kinds.Add((byte)K.InstanceIter); Value(w, ii.Obj); break;
                    case Ops.GetItemIter gi: kinds.Add((byte)K.GetItemIter); Value(w, gi.Obj); w.Write(gi.I); break;
                    case PyRandom rnd: kinds.Add((byte)K.Random); w.Write(rnd.S0); w.Write(rnd.S1); w.Write(rnd.S2); w.Write(rnd.S3); break;
                    default:
                        if (_ctx.HostNames.TryGetValue(o, out var hn))
                        {
                            kinds.Add((byte)K.HostHandle);
                            sw.Write(hn);
                            break;
                        }
                        if (HostValues.TryWrite(o, sw))
                        {
                            kinds.Add((byte)K.HostValue);
                            break;
                        }
                        throw new SnapshotException("cannot save object of type '" + o.GetType().FullName +
                            "'. Expose host objects with ScriptContext.BindHost(name, obj) so they are saved by name.");
                }
            }

            void CheckOwnCode(FunctionCode code)
            {
                if (code.Script != _ctx.Script)
                    throw new SnapshotException("state references code from another script ('" + code.Script.Name + "'); only the main script can be saved");
            }
        }

        static class HostValues
        {
            public static bool TryWrite(object o, BinaryWriter w)
            {
                switch (o)
                {
                    case DateTime d: w.Write((byte)1); w.Write(d.ToBinary()); return true;
                    case TimeSpan t: w.Write((byte)2); w.Write(t.Ticks); return true;
                    case decimal m: w.Write((byte)3); w.Write(m); return true;
                    case Guid g: w.Write((byte)4); w.Write(g.ToByteArray()); return true;
                    case DateTimeOffset dto: w.Write((byte)5); w.Write(dto.Ticks); w.Write(dto.Offset.Ticks); return true;
                    case Enum e: w.Write((byte)6); w.Write(e.GetType().AssemblyQualifiedName); w.Write(Convert.ToInt64(e)); return true;
                }
                return false;
            }

            public static object Read(BinaryReader r)
            {
                switch (r.ReadByte())
                {
                    case 1: return DateTime.FromBinary(r.ReadInt64());
                    case 2: return new TimeSpan(r.ReadInt64());
                    case 3: return r.ReadDecimal();
                    case 4: return new Guid(r.ReadBytes(16));
                    case 5: { long t = r.ReadInt64(); return new DateTimeOffset(t, new TimeSpan(r.ReadInt64())); }
                    case 6:
                        {
                            var type = Type.GetType(r.ReadString()) ?? throw new SnapshotException("enum type not found");
                            return Enum.ToObject(type, r.ReadInt64());
                        }
                }
                throw new SnapshotException("corrupted host value");
            }
        }

        public static byte[] Save(ScriptContext ctx, bool includeSource) => new Writer(ctx).Save(includeSource);

        // ================================================================== load
        sealed class Reader
        {
            readonly object[] _objs;
            readonly Script _script;
            public Reader(object[] objs, Script script) { _objs = objs; _script = script; }

            public Value Value(BinaryReader r)
            {
                switch ((V)r.ReadByte())
                {
                    case V.Unbound: return default;
                    case V.None: return DynLan2.Value.None;
                    case V.Bool: return DynLan2.Value.Bool(r.ReadBoolean());
                    case V.Int: return DynLan2.Value.Int(r.ReadInt64());
                    case V.Float: return new Value(DynLan2.Value.TFloat, r.ReadInt64());
                    case V.Str: return DynLan2.Value.Str(r.ReadString());
                    case V.BigInt: { int n = r.ReadInt32(); return DynLan2.Value.BigInt(new BigInteger(r.ReadBytes(n))); }
                    case V.Ref: return DynLan2.Value.Obj(_objs[r.ReadInt32()]);
                    case V.NotImplemented: return new Value(Builtins.NotImplementedObj, 0);
                }
                throw new SnapshotException("corrupted value");
            }

            public T Ref<T>(BinaryReader r) where T : class
            {
                int id = r.ReadInt32();
                return id < 0 ? null : (T)_objs[id];
            }

            public Value[] Values(BinaryReader r)
            {
                int n = r.ReadInt32();
                if (n < 0) return null;
                var a = new Value[n];
                for (int i = 0; i < n; i++) a[i] = Value(r);
                return a;
            }

            public Dictionary<string, Value> Map(BinaryReader r)
            {
                int n = r.ReadInt32();
                if (n < 0) return null;
                var d = new Dictionary<string, Value>(n, StringComparer.Ordinal);
                for (int i = 0; i < n; i++) { string k = r.ReadString(); d[k] = Value(r); }
                return d;
            }

            public FunctionCode Code(long key)
            {
                var c = _script.GetCode(key);
                c.EnsureCompiled();
                return c;
            }
        }

        static T Uninit<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        public static ScriptContext Load(ScriptEngine engine, byte[] data, IDictionary<string, object> hostBindings, string sourceOverride)
        {
            try { return LoadCore(engine, data, hostBindings, sourceOverride); }
            finally { ClearDeferredContainers(); }
        }

        static ScriptContext LoadCore(ScriptEngine engine, byte[] data, IDictionary<string, object> hostBindings, string sourceOverride)
        {
            if (data == null || data.Length < 4 || Encoding.ASCII.GetString(data, 0, 4) != Magic) throw new SnapshotException("not a DynLan2 state");
            using (var z = new DeflateStream(new MemoryStream(data, 4, data.Length - 4), CompressionMode.Decompress))
            using (var r = new BinaryReader(z, Encoding.UTF8))
            {
                if (r.ReadInt32() != FormatVersion) throw new SnapshotException("unsupported state format version");
                if (r.ReadInt32() != CompilerVersion) throw new SnapshotException("state was saved by an incompatible compiler version");
                string name = r.ReadString();
                string hash = r.ReadString();
                bool hasSource = r.ReadBoolean();
                string source = hasSource ? r.ReadString() : sourceOverride;
                if (source == null) throw new SnapshotException("state was saved without source; pass the script source to Restore()");
                if (Script.HashOf(source) != hash) throw new SnapshotException("script source does not match the saved state");
                var script = engine.Compile(source, name);
                var ctx = new ScriptContext(engine, script);
                if (hostBindings != null)
                    foreach (var kv in hostBindings) BindRestoredHost(ctx, kv.Key, kv.Value);

                int count = r.ReadInt32();
                var kinds = r.ReadBytes(count);
                var shell = new BinaryReader(new MemoryStream(r.ReadBytes(r.ReadInt32())), Encoding.UTF8);
                var roots = new BinaryReader(new MemoryStream(r.ReadBytes(r.ReadInt32())), Encoding.UTF8);
                var body = new BinaryReader(new MemoryStream(r.ReadBytes(r.ReadInt32())), Encoding.UTF8);

                // 1. shells
                var objs = new object[count];
                for (int i = 0; i < count; i++)
                {
                    switch ((K)kinds[i])
                    {
                        case K.List: objs[i] = new PyList(); break;
                        case K.Tuple: objs[i] = new PyTuple(new Value[shell.ReadInt32()]); break;
                        case K.Dict: objs[i] = new PyDict(); break;
                        case K.Set: objs[i] = new PySet(); break;
                        case K.Cell: objs[i] = new Cell(); break;
                        case K.Function: objs[i] = Uninit<PyFunction>(); break;
                        case K.Class: objs[i] = Uninit<PyClass>(); break;
                        case K.BuiltinClass:
                            {
                                string cn = shell.ReadString();
                                objs[i] = Builtins.ClassRegistry.TryGetValue(cn, out var bc) ? bc : throw new SnapshotException("unknown built-in class " + cn);
                                break;
                            }
                        case K.Instance: objs[i] = Uninit<PyInstance>(); break;
                        case K.BoundMethod: objs[i] = new BoundMethod(default, default); break;
                        case K.BuiltinFunction:
                            {
                                string key = shell.ReadString();
                                if (!Builtins.Registry.TryGetValue(key, out var bf))
                                {
                                    // unbound type methods (e.g. str.upper) are created on demand
                                    if (key.StartsWith("unbound:"))
                                    {
                                        var parts = key.Substring(8).Split('.');
                                        Ops.TryGetAttr(Value.Obj(Builtins.TypeRegistry[parts[0]]), parts[1], out _);
                                        bf = Builtins.Registry[key];
                                    }
                                    else throw new SnapshotException("unknown built-in function " + key);
                                }
                                objs[i] = bf;
                                break;
                            }
                        case K.BuiltinMethod: objs[i] = new BuiltinMethod(default, null, null); break;
                        case K.PyType:
                            {
                                string tn = shell.ReadString();
                                objs[i] = Builtins.TypeRegistry.TryGetValue(tn, out var pt) ? pt : throw new SnapshotException("unknown type " + tn);
                                break;
                            }
                        case K.StaticMethod: objs[i] = new PyStaticMethod(default); break;
                        case K.ClassMethod: objs[i] = new PyClassMethod(default); break;
                        case K.Property: objs[i] = new PyProperty(default, default, default); break;
                        case K.Super: objs[i] = new PySuper(null, default); break;
                        case K.Range: objs[i] = new PyRange(0, 0, 1); break;
                        case K.Slice: objs[i] = new PySlice(default, default, default); break;
                        case K.Module:
                            {
                                string mn = shell.ReadString();
                                objs[i] = Builtins.Modules[mn];
                                break;
                            }
                        case K.DictView: objs[i] = new DictView(null, 0); break;
                        case K.Frame: objs[i] = new Frame(); break;
                        case K.Generator: objs[i] = new PyGenerator(null, null); break;
                        case K.ListIter: objs[i] = new ListIter(null); break;
                        case K.ArrayIter: objs[i] = new ArrayIter(null); break;
                        case K.StrIter: objs[i] = new StrIter(null); break;
                        case K.RangeIter: objs[i] = new RangeIter(0, 0, 1); break;
                        case K.DictIter: objs[i] = Uninit<DictIter>(); break;
                        case K.EnumerateIter: objs[i] = new EnumerateIter(null, 0); break;
                        case K.ZipIter: objs[i] = new ZipIter(null); break;
                        case K.MapIter: objs[i] = new MapIter(default, null); break;
                        case K.FilterIter: objs[i] = new FilterIter(default, null); break;
                        case K.InstanceIter: objs[i] = new InstanceIter(default); break;
                        case K.GetItemIter: objs[i] = new Ops.GetItemIter(default); break;
                        case K.Random: objs[i] = new PyRandom(0); break;
                        case K.HostHandle:
                            {
                                string hn = shell.ReadString();
                                if (hn.StartsWith("module:"))
                                {
                                    string moduleName = hn.Substring(7);
                                    objs[i] = hostBindings != null && (hostBindings.TryGetValue(moduleName, out var mo) || hostBindings.TryGetValue(hn, out mo)) ? mo
                                        : throw new SnapshotException("host module binding '" + moduleName + "' was not provided to Restore()");
                                    break;
                                }
                                if (hostBindings == null || !hostBindings.TryGetValue(hn, out var ho))
                                    throw new SnapshotException("host binding '" + hn + "' was not provided to Restore()");
                                objs[i] = ho;
                                break;
                            }
                        case K.HostValue: objs[i] = HostValues.Read(shell); break;
                        default: throw new SnapshotException("corrupted state (kind " + kinds[i] + ")");
                    }
                }

                var rd = new Reader(objs, script);
                var classes = new List<PyClass>();
                // 2. bodies
                for (int i = 0; i < count; i++)
                {
                    var w = body;
                    switch ((K)kinds[i])
                    {
                        case K.List: ((PyList)objs[i]).Items.AddRange(rd.Values(w)); break;
                        case K.Tuple: { var src = rd.Values(w); Array.Copy(src, ((PyTuple)objs[i]).Items, src.Length); break; }
                        case K.Dict:
                            {
                                var d = (PyDict)objs[i];
                                int n = w.ReadInt32();
                                var pending = new List<(Value, Value)>(n);
                                for (int j = 0; j < n; j++) pending.Add((rd.Value(w), rd.Value(w)));
                                deferredDicts.Add((d, pending));
                                break;
                            }
                        case K.Set:
                            {
                                var s = (PySet)objs[i];
                                s.Frozen = w.ReadBoolean();
                                deferredSets.Add((s, rd.Values(w)));
                                break;
                            }
                        case K.Cell: ((Cell)objs[i]).V = rd.Value(w); break;
                        case K.Function:
                            {
                                var f = (PyFunction)objs[i];
                                f.Code = rd.Code(w.ReadInt64());
                                f.Module = ctx.Module;
                                f.Name = w.ReadString();
                                f.Defaults = rd.Values(w);
                                f.KwDefaults = rd.Values(w);
                                int nc = w.ReadInt32();
                                if (nc >= 0) { f.Closure = new Cell[nc]; for (int j = 0; j < nc; j++) f.Closure[j] = rd.Ref<Cell>(w); }
                                f.Owner = rd.Ref<PyClass>(w);
                                f.Attrs = rd.Map(w);
                                f.GCells = new GlobalCell[f.Code.GlobalNames.Length];
                                break;
                            }
                        case K.Class:
                            {
                                var c = (PyClass)objs[i];
                                c.Name = w.ReadString();
                                int nb = w.ReadInt32();
                                c.Bases = new PyClass[nb];
                                for (int j = 0; j < nb; j++) c.Bases[j] = rd.Ref<PyClass>(w);
                                c.Dict = rd.Map(w);
                                classes.Add(c);
                                break;
                            }
                        case K.Instance:
                            {
                                var inst = (PyInstance)objs[i];
                                inst.Class = rd.Ref<PyClass>(w);
                                inst.Attrs = rd.Map(w);
                                break;
                            }
                        case K.BoundMethod: { var b = (BoundMethod)objs[i]; b.Self = rd.Value(w); b.Func = rd.Value(w); break; }
                        case K.BuiltinMethod:
                            {
                                var bm = (BuiltinMethod)objs[i];
                                bm.Self = rd.Value(w);
                                bm.Name = w.ReadString();
                                bm.Fn = Builtins.FindMethod(bm.Self, bm.Name) ?? throw new SnapshotException("unknown built-in method " + bm.Name);
                                break;
                            }
                        case K.StaticMethod: ((PyStaticMethod)objs[i]).Fn = rd.Value(w); break;
                        case K.ClassMethod: ((PyClassMethod)objs[i]).Fn = rd.Value(w); break;
                        case K.Property: { var p = (PyProperty)objs[i]; p.Get = rd.Value(w); p.Set = rd.Value(w); p.Del = rd.Value(w); break; }
                        case K.Super: { var s = (PySuper)objs[i]; s.Start = rd.Ref<PyClass>(w); s.Self = rd.Value(w); break; }
                        case K.Range: { var rg = (PyRange)objs[i]; rg.Start = w.ReadInt64(); rg.Stop = w.ReadInt64(); rg.Step = w.ReadInt64(); break; }
                        case K.Slice: { var sl = (PySlice)objs[i]; sl.Start = rd.Value(w); sl.Stop = rd.Value(w); sl.Step = rd.Value(w); break; }
                        case K.DictView: { var dv = (DictView)objs[i]; dv.D = rd.Ref<PyDict>(w); dv.Mode = w.ReadInt32(); break; }
                        case K.Frame:
                            {
                                var fr = (Frame)objs[i];
                                fr.Code = rd.Code(w.ReadInt64());
                                fr.Func = rd.Ref<PyFunction>(w);
                                fr.R = rd.Values(w);
                                fr.Pc = w.ReadInt32();
                                fr.PcExact = w.ReadBoolean();
                                fr.RetDst = w.ReadInt32();
                                fr.Kind = (FK)w.ReadByte();
                                fr.ExitPc = w.ReadInt32();
                                fr.SendDst = w.ReadInt32();
                                byte aux = w.ReadByte();
                                if (aux == 2)
                                {
                                    var ci = new ClassInfo { Name = w.ReadString() };
                                    int nb = w.ReadInt32();
                                    ci.Bases = new PyClass[nb];
                                    for (int j = 0; j < nb; j++) ci.Bases[j] = rd.Ref<PyClass>(w);
                                    fr.Aux = ci;
                                }
                                else if (aux == 1) fr.Aux = rd.Ref<object>(w);
                                fr.Ns = rd.Map(w);
                                if (fr.R.Length != fr.Code.NumRegs) throw new SnapshotException("state does not match compiled code (register count)");
                                break;
                            }
                        case K.Generator:
                            {
                                var g = (PyGenerator)objs[i];
                                g.Frame = rd.Ref<Frame>(w); g.Running = w.ReadBoolean(); g.Done = w.ReadBoolean(); g.Started = w.ReadBoolean(); g.Name = w.ReadString();
                                break;
                            }
                        case K.ListIter: { var li = (ListIter)objs[i]; li.L = rd.Ref<PyList>(w); li.I = w.ReadInt32(); break; }
                        case K.ArrayIter: { var ai = (ArrayIter)objs[i]; ai.A = rd.Values(w); ai.I = w.ReadInt32(); break; }
                        case K.StrIter: { var si = (StrIter)objs[i]; si.S = w.ReadString(); si.I = w.ReadInt32(); break; }
                        case K.RangeIter: { var ri = (RangeIter)objs[i]; ri.Cur = w.ReadInt64(); ri.Stop = w.ReadInt64(); ri.Step = w.ReadInt64(); break; }
                        case K.DictIter: { var di = (DictIter)objs[i]; di.D = rd.Ref<PyDict>(w); di.I = w.ReadInt32(); di.Mode = w.ReadInt32(); di.StartCount = w.ReadInt32(); break; }
                        case K.EnumerateIter: { var ei = (EnumerateIter)objs[i]; ei.Inner = rd.Ref<PyIterator>(w); ei.N = w.ReadInt64(); break; }
                        case K.ZipIter:
                            {
                                var zi = (ZipIter)objs[i];
                                int n = w.ReadInt32();
                                zi.Its = new PyIterator[n];
                                for (int j = 0; j < n; j++) zi.Its[j] = rd.Ref<PyIterator>(w);
                                break;
                            }
                        case K.MapIter:
                            {
                                var mi = (MapIter)objs[i];
                                mi.Fn = rd.Value(w);
                                int n = w.ReadInt32();
                                mi.Its = new PyIterator[n];
                                for (int j = 0; j < n; j++) mi.Its[j] = rd.Ref<PyIterator>(w);
                                break;
                            }
                        case K.FilterIter: { var fi = (FilterIter)objs[i]; fi.Fn = rd.Value(w); fi.It = rd.Ref<PyIterator>(w); break; }
                        case K.InstanceIter: ((InstanceIter)objs[i]).Obj = rd.Value(w); break;
                        case K.GetItemIter: { var gi = (Ops.GetItemIter)objs[i]; gi.Obj = rd.Value(w); gi.I = w.ReadInt64(); break; }
                        case K.Random: { var rn = (PyRandom)objs[i]; rn.S0 = w.ReadUInt64(); rn.S1 = w.ReadUInt64(); rn.S2 = w.ReadUInt64(); rn.S3 = w.ReadUInt64(); break; }
                    }
                }

                // 3. class MROs (bases may appear later in the table)
                var done = new HashSet<PyClass>();
                void Fix(PyClass c)
                {
                    if (c.IsBuiltin || !done.Add(c)) return;
                    foreach (var b in c.Bases) Fix(b);
                    c.Mro = PyClass.ComputeMro(c, c.Bases);
                    c.IsException = c.Bases.Any(b => b.IsException);
                }
                foreach (var c in classes) Fix(c);
                System.Threading.Interlocked.Increment(ref PyClass.Stamp);

                // 4. hashed containers last (hashes may depend on restored objects)
                var prevVm = VM.Current;
                VM.Current = ctx.Vm;
                try
                {
                    foreach (var (d, pending) in deferredDicts) foreach (var (k, v) in pending) d.Set(k, v);
                    foreach (var (s, items) in deferredSets) foreach (var x in items) s.Items.Add(x);
                }
                finally { VM.Current = prevVm; }

                // 5. roots
                int ng = roots.ReadInt32();
                for (int i = 0; i < ng; i++)
                {
                    string gname = roots.ReadString();
                    ctx.Module.GetCell(gname).V = rd.Value(roots);
                }
                int sp = roots.ReadInt32();
                for (int i = 0; i < sp; i++) ctx.Vm.Push(rd.Ref<Frame>(roots));
                ctx.Started = roots.ReadBoolean();
                ctx.Completed = roots.ReadBoolean();
                var rnd2 = rd.Ref<PyRandom>(roots);
                if (rnd2 != null) ctx.Vm.Random = rnd2;
                return ctx;
            }
        }

        const string ModulePrefix = "module:";

        /// <summary>Binds a host object passed to Restore; "module:name" keys are also importable as modules.</summary>
        static void BindRestoredHost(ScriptContext ctx, string key, object value)
        {
            if (key.StartsWith(ModulePrefix, StringComparison.Ordinal))
            {
                ctx.AddModule(key.Substring(ModulePrefix.Length), value);
                return;
            }
            ctx.HostBindings[key] = value;
            ctx.AllowType(value);
            if (value != null) ctx.HostNames[value] = key;
        }

        static void ClearDeferredContainers()
        {
            deferredDicts.Clear();
            deferredSets.Clear();
        }

        [ThreadStatic] static List<(PyDict, List<(Value, Value)>)> _dd;
        [ThreadStatic] static List<(PySet, Value[])> _ds;
        static List<(PyDict, List<(Value, Value)>)> deferredDicts => _dd ??= new List<(PyDict, List<(Value, Value)>)>();
        static List<(PySet, Value[])> deferredSets => _ds ??= new List<(PySet, Value[])>();
    }
}
