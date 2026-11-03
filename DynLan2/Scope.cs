using System;
using System.Collections.Generic;

namespace DynLan2
{
    internal enum ScopeKind : byte { Module, Function, Class, Comp }
    internal enum SymKind : byte { Local, Cell, Free, Global, ClassNs }

    internal sealed class Sym
    {
        public string Name;
        public SymKind Kind;
        public int Reg = -1;
        public int FreeIndex = -1;
        public byte TypeHint;   // 0 none, 1 int, 2 float
        public bool IsParam;
        public Scope Owner;
        public override string ToString() => Name + ":" + Kind;
    }

    /// <summary>Insertion-ordered set; keeps compilation deterministic across processes (needed for snapshots).</summary>
    internal sealed class OrderedSet
    {
        readonly HashSet<string> _set = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<string> Items = new List<string>();
        public bool Add(string s) { if (!_set.Add(s)) return false; Items.Add(s); return true; }
        public bool Contains(string s) => _set.Contains(s);
    }

    internal sealed class Scope
    {
        public ScopeKind Kind;
        public Scope Parent;
        public string Name;
        public Node Node;
        public long CodeKey = -1;
        public bool Lazy;          // function body not analyzed yet (analyzed on first compile)
        public bool IsGenerator;
        public bool IsLambda;
        public bool IsGenExp;

        public readonly OrderedSet Bound = new OrderedSet();
        public readonly OrderedSet Used = new OrderedSet();
        public readonly OrderedSet Globals = new OrderedSet();
        public readonly OrderedSet Nonlocals = new OrderedSet();
        public readonly List<string> ParamNames = new List<string>();
        public readonly Dictionary<string, byte> Annot = new Dictionary<string, byte>(StringComparer.Ordinal);

        public readonly Dictionary<string, Sym> Syms = new Dictionary<string, Sym>(StringComparer.Ordinal);
        public readonly Dictionary<string, Sym> FreeSyms = new Dictionary<string, Sym>(StringComparer.Ordinal);
        public readonly List<string> FreeNames = new List<string>();
        public readonly List<Scope> Children = new List<Scope>();

        public bool IsCodeUnit => Kind != ScopeKind.Comp;

        public Scope Host
        {
            get { var s = this; while (s.Kind == ScopeKind.Comp) s = s.Parent; return s; }
        }

        public Sym AddFree(string name)
        {
            if (FreeSyms.TryGetValue(name, out var f)) return f;
            f = new Sym { Name = name, Kind = SymKind.Free, FreeIndex = FreeNames.Count, Owner = this };
            FreeNames.Add(name);
            FreeSyms[name] = f;
            return f;
        }
    }

    /// <summary>
    /// Pass 1 collects bindings per scope, pass 2 resolves every name to Local / Cell / Free / Global / ClassNs.
    /// Runs once over the whole module (cheap); bytecode generation is lazy, per function.
    /// </summary>
    internal sealed class ScopeAnalyzer
    {
        /// <summary>Code keys: (root index &lt;&lt; KeyShift) | local index. Deterministic regardless of compile order.</summary>
        internal const int KeyShift = 20;
        internal const long LocalMask = (1L << KeyShift) - 1;

        readonly string _file;
        readonly Action<Scope> _register;
        readonly List<Scope> _all = new List<Scope>();
        readonly bool _moduleMode;
        long _nextRoot;
        long _rootKey;
        long _nextLocal = 1;
        Scope _cur;

        ScopeAnalyzer(string file, Action<Scope> register, bool moduleMode)
        {
            _file = file;
            _register = register;
            _moduleMode = moduleMode;
        }

        /// <summary>
        /// Analyzes module-level code. Bodies of functions whose enclosing scopes are only module/classes are
        /// independent of everything else, so they are left for <see cref="AnalyzeRoot"/> (lazy, on first call).
        /// </summary>
        public static void AnalyzeModule(ModuleNode m, string file, Action<Scope> register)
        {
            var a = new ScopeAnalyzer(file, register, true);
            var s = a.NewScope(ScopeKind.Module, null, "<module>", m);
            m.Scope = s;
            a._cur = s;
            a.Stmts(m.Body);
            foreach (var sc in a._all) if (!sc.Lazy) a.ResolveAll(sc);
        }

        public static void AnalyzeRoot(Scope root, string file, Action<Scope> register)
        {
            var a = new ScopeAnalyzer(file, register, false) { _rootKey = root.CodeKey };
            root.Lazy = false;
            a._all.Add(root);
            a._cur = root;
            a.Stmts(((FuncDef)root.Node).Body);
            foreach (var sc in a._all) a.ResolveAll(sc);
        }

        Scope NewScope(ScopeKind kind, Scope parent, string name, Node node)
        {
            var s = new Scope { Kind = kind, Parent = parent, Name = name, Node = node };
            parent?.Children.Add(s);
            _all.Add(s);
            if (kind != ScopeKind.Comp)
            {
                s.CodeKey = _moduleMode ? (_nextRoot++) << KeyShift : _rootKey | _nextLocal++;
                _register(s);
            }
            return s;
        }

        ScriptSyntaxException Error(string msg, Node n) => new ScriptSyntaxException(msg, _file, n?.Line ?? 0, 0);

        // ------------------------------------------------------------ pass 1
        void Stmts(List<Stmt> body) { if (body != null) foreach (var s in body) Stmt(s); }

        void Bind(string name) => _cur.Bound.Add(name);

        static byte HintOf(Expr ann)
        {
            if (ann is NameExpr n) return n.Id == "int" ? (byte)1 : n.Id == "float" ? (byte)2 : (byte)0;
            if (ann is ConstExpr c && c.Value.IsStr) return c.Value.AsStr == "int" ? (byte)1 : c.Value.AsStr == "float" ? (byte)2 : (byte)0;
            return 0;
        }

        void Stmt(Stmt s)
        {
            switch (s)
            {
                case ExprStmt e: Expr(e.X); break;
                case AssignStmt a:
                    Expr(a.Value);
                    foreach (var t in a.Targets) Target(t);
                    break;
                case AugAssignStmt a:
                    Expr(a.Value);
                    if (a.Target is NameExpr an) { _cur.Used.Add(an.Id); Bind(an.Id); }
                    else Expr(a.Target);
                    break;
                case AnnAssignStmt a:
                    if (a.Value != null) Expr(a.Value);
                    if (a.Target is NameExpr n)
                    {
                        if (_cur.Kind == ScopeKind.Function)
                        {
                            byte h = HintOf(a.Annotation);
                            if (h != 0) _cur.Annot[n.Id] = h;
                        }
                        Bind(n.Id);
                    }
                    else Target(a.Target);
                    break;
                case IfStmt i: Expr(i.Test); Stmts(i.Body); Stmts(i.Else); break;
                case WhileStmt w: Expr(w.Test); Stmts(w.Body); Stmts(w.Else); break;
                case ForStmt f: Expr(f.Iter); Target(f.Target); Stmts(f.Body); Stmts(f.Else); break;
                case ReturnStmt r:
                    if (_cur.Kind != ScopeKind.Function) throw Error("'return' outside function", s);
                    if (r.Value != null) Expr(r.Value);
                    break;
                case RaiseStmt r: if (r.Exc != null) Expr(r.Exc); if (r.Cause != null) Expr(r.Cause); break;
                case GlobalStmt g:
                    foreach (var gn in g.Names)
                    {
                        if (_cur.Bound.Contains(gn) && _cur.Kind == ScopeKind.Function) throw Error("name '" + gn + "' is assigned to before global declaration", s);
                        _cur.Globals.Add(gn);
                    }
                    break;
                case NonlocalStmt nl:
                    if (_cur.Kind == ScopeKind.Module) throw Error("nonlocal declaration not allowed at module level", s);
                    foreach (var nn in nl.Names) _cur.Nonlocals.Add(nn);
                    break;
                case DelStmt d: foreach (var t in d.Targets) DelTarget(t); break;
                case AssertStmt a: Expr(a.Test); if (a.Msg != null) Expr(a.Msg); break;
                case ImportStmt im:
                    foreach (var (m, asName) in im.Names) Bind(asName ?? m.Split('.')[0]);
                    break;
                case FromImportStmt fi:
                    foreach (var (fn0, asName) in fi.Names) Bind(asName ?? fn0);
                    break;
                case FuncDef fd:
                    foreach (var d in fd.Decorators) Expr(d);
                    Defaults(fd.Args);
                    Bind(fd.Name);
                    if (_moduleMode)
                    {
                        // independent body: analyze lazily (the stub carries the signature only)
                        var stub = new Scope { Kind = ScopeKind.Function, Parent = _cur, Name = fd.Name, Node = fd, Lazy = true };
                        Params(fd.Args, stub);
                        _cur.Children.Add(stub);
                        _all.Add(stub);
                        stub.CodeKey = (_nextRoot++) << KeyShift;
                        fd.Scope = stub;
                        _register(stub);
                    }
                    else
                    {
                        var sc = NewScope(ScopeKind.Function, _cur, fd.Name, fd);
                        fd.Scope = sc;
                        var save = _cur;
                        _cur = sc;
                        Params(fd.Args, sc);
                        Stmts(fd.Body);
                        _cur = save;
                    }
                    break;
                case ClassDef cd:
                    foreach (var d in cd.Decorators) Expr(d);
                    foreach (var b in cd.Bases) Expr(b);
                    foreach (var k in cd.Keywords) Expr(k.Value);
                    Bind(cd.Name);
                    {
                        var sc = NewScope(ScopeKind.Class, _cur, cd.Name, cd);
                        cd.Scope = sc;
                        var save = _cur;
                        _cur = sc;
                        Stmts(cd.Body);
                        _cur = save;
                    }
                    break;
                case TryStmt t:
                    Stmts(t.Body);
                    foreach (var h in t.Handlers)
                    {
                        if (h.Type != null) Expr(h.Type);
                        if (h.Name != null) Bind(h.Name);
                        Stmts(h.Body);
                    }
                    Stmts(t.Else);
                    Stmts(t.Finally);
                    break;
                case WithStmt w:
                    foreach (var it in w.Items)
                    {
                        Expr(it.Context);
                        if (it.Target != null) Target(it.Target);
                    }
                    Stmts(w.Body);
                    break;
                case PassStmt _: case BreakStmt _: case ContinueStmt _: break;
                default: throw Error("unsupported statement", s);
            }
        }

        void Defaults(Params p)
        {
            foreach (var x in p.Positional) if (x.Default != null) Expr(x.Default);
            foreach (var x in p.KwOnly) if (x.Default != null) Expr(x.Default);
        }

        void Params(Params p, Scope sc)
        {
            void Add(Param x)
            {
                if (sc.ParamNames.Contains(x.Name)) throw Error("duplicate argument '" + x.Name + "' in function definition", sc.Node);
                sc.ParamNames.Add(x.Name);
                sc.Bound.Add(x.Name);
                byte h = x.Annotation != null ? HintOf(x.Annotation) : (byte)0;
                if (h != 0) sc.Annot[x.Name] = h;
            }
            foreach (var x in p.Positional) Add(x);
            if (p.VarArg != null) Add(p.VarArg);
            foreach (var x in p.KwOnly) Add(x);
            if (p.KwArg != null) Add(p.KwArg);
        }

        void Target(Expr t)
        {
            switch (t)
            {
                case NameExpr n: Bind(n.Id); break;
                case TupleExpr tu: foreach (var x in tu.Items) Target(x); break;
                case ListExpr l: foreach (var x in l.Items) Target(x); break;
                case StarredExpr s: Target(s.Value); break;
                case AttrExpr a: Expr(a.Obj); break;
                case IndexExpr i: Expr(i.Obj); Expr(i.Index); break;
                default: throw Error("cannot assign to expression", t);
            }
        }

        void DelTarget(Expr t)
        {
            switch (t)
            {
                case NameExpr n: Bind(n.Id); _cur.Used.Add(n.Id); break;
                case TupleExpr tu: foreach (var x in tu.Items) DelTarget(x); break;
                case ListExpr l: foreach (var x in l.Items) DelTarget(x); break;
                case AttrExpr a: Expr(a.Obj); break;
                case IndexExpr i: Expr(i.Obj); Expr(i.Index); break;
                default: throw Error("cannot delete expression", t);
            }
        }

        void Expr(Expr e)
        {
            switch (e)
            {
                case null: return;
                case NameExpr n: _cur.Used.Add(n.Id); return;
                case ConstExpr _: return;
                case BinExpr b: Expr(b.L); Expr(b.R); return;
                case UnaryExpr u: Expr(u.X); return;
                case BoolExpr b: foreach (var v in b.Values) Expr(v); return;
                case CompareExpr c: Expr(c.Left); foreach (var r in c.Rights) Expr(r); return;
                case IfExpr i: Expr(i.Test); Expr(i.Body); Expr(i.Else); return;
                case CallExpr c:
                    Expr(c.Func);
                    foreach (var a in c.Args) Expr(a);
                    foreach (var k in c.Keywords) Expr(k.Value);
                    if (c.Func is NameExpr fn && fn.Id == "super" && c.Args.Count == 0) MarkSuperUse();
                    return;
                case AttrExpr a: Expr(a.Obj); return;
                case IndexExpr i: Expr(i.Obj); Expr(i.Index); return;
                case SliceExpr s: Expr(s.Lo); Expr(s.Hi); Expr(s.Step); return;
                case ListExpr l: foreach (var x in l.Items) Expr(x); return;
                case TupleExpr t: foreach (var x in t.Items) Expr(x); return;
                case SetExpr s: foreach (var x in s.Items) Expr(x); return;
                case DictExpr d:
                    for (int i = 0; i < d.Values.Count; i++) { Expr(d.Keys[i]); Expr(d.Values[i]); }
                    return;
                case StarredExpr s: Expr(s.Value); return;
                case YieldExpr y:
                    {
                        var host = _cur;
                        if (host.Kind != ScopeKind.Function) throw Error("'yield' outside function", e);
                        host.IsGenerator = true;
                        Expr(y.Value);
                        return;
                    }
                case NamedExpr ne:
                    Expr(ne.Value);
                    {
                        var s = _cur;
                        while (s.Kind == ScopeKind.Comp) s = s.Parent;
                        s.Bound.Add(ne.Target.Id);
                        _cur.Used.Add(ne.Target.Id);
                    }
                    return;
                case FStringExpr f: foreach (var p in f.Parts) Expr(p); return;
                case FormattedValue fv: Expr(fv.Value); Expr(fv.Spec); return;
                case LambdaExpr l:
                    Defaults(l.Args);
                    {
                        var sc = NewScope(ScopeKind.Function, _cur, "<lambda>", l);
                        sc.IsLambda = true;
                        l.Scope = sc;
                        var save = _cur;
                        _cur = sc;
                        Params(l.Args, sc);
                        Expr(l.Body);
                        _cur = save;
                    }
                    return;
                case CompExpr c:
                    {
                        Expr(c.Gens[0].Iter);
                        Scope sc;
                        if (c.Kind == 'g')
                        {
                            sc = NewScope(ScopeKind.Function, _cur, "<genexpr>", c);
                            sc.IsGenerator = true;
                            sc.IsGenExp = true;
                            sc.ParamNames.Add(".0");
                            sc.Bound.Add(".0");
                        }
                        else sc = NewScope(ScopeKind.Comp, _cur, "<comp>", c);
                        c.Scope = sc;
                        var save = _cur;
                        _cur = sc;
                        for (int i = 0; i < c.Gens.Count; i++)
                        {
                            var g = c.Gens[i];
                            if (i > 0) Expr(g.Iter);
                            Target(g.Target);
                            foreach (var cond in g.Ifs) Expr(cond);
                        }
                        Expr(c.Elt);
                        Expr(c.Value);
                        _cur = save;
                    }
                    return;
                default: throw Error("unsupported expression", e);
            }
        }

        void MarkSuperUse()
        {
            // zero-arg super() needs the first parameter of the enclosing method
            var s = _cur;
            while (s != null && s.Kind == ScopeKind.Comp) s = s.Parent;
            if (s != null && s.Kind == ScopeKind.Function && s.ParamNames.Count > 0) s.Used.Add(s.ParamNames[0]);
        }

        // ------------------------------------------------------------ pass 2
        void ResolveAll(Scope s)
        {
            foreach (var n in s.ParamNames) Resolve(s, n);
            foreach (var n in s.Bound.Items) Resolve(s, n);
            foreach (var n in s.Used.Items) Resolve(s, n);
            foreach (var n in s.Nonlocals.Items)
                if (s.Kind == ScopeKind.Function || s.Kind == ScopeKind.Class) Resolve(s, n);
        }

        Sym Resolve(Scope s, string name)
        {
            if (s.Syms.TryGetValue(name, out var sym)) return sym;
            switch (s.Kind)
            {
                case ScopeKind.Module:
                    sym = new Sym { Name = name, Kind = SymKind.Global, Owner = s };
                    break;
                case ScopeKind.Class:
                    if (s.Globals.Contains(name)) sym = new Sym { Name = name, Kind = SymKind.Global, Owner = s };
                    else if (s.Nonlocals.Contains(name))
                        sym = FindFree(s.Parent, name, s) ?? throw Error("no binding for nonlocal '" + name + "' found", s.Node);
                    else if (s.Bound.Contains(name)) sym = new Sym { Name = name, Kind = SymKind.ClassNs, Owner = s };
                    else sym = FindFree(s.Parent, name, s) ?? new Sym { Name = name, Kind = SymKind.Global, Owner = s };
                    break;
                case ScopeKind.Function:
                    if (s.Globals.Contains(name)) sym = new Sym { Name = name, Kind = SymKind.Global, Owner = s };
                    else if (s.Nonlocals.Contains(name))
                        sym = FindFree(s.Parent, name, s) ?? throw Error("no binding for nonlocal '" + name + "' found", s.Node);
                    else if (s.Bound.Contains(name))
                    {
                        sym = new Sym { Name = name, Kind = SymKind.Local, Owner = s, IsParam = s.ParamNames.Contains(name) };
                        if (s.Annot.TryGetValue(name, out byte h)) sym.TypeHint = h;
                    }
                    else sym = FindFree(s.Parent, name, s) ?? new Sym { Name = name, Kind = SymKind.Global, Owner = s };
                    break;
                case ScopeKind.Comp:
                    if (s.Bound.Contains(name)) sym = new Sym { Name = name, Kind = SymKind.Local, Owner = s };
                    else
                    {
                        var p = s.Parent;
                        if (p.Kind == ScopeKind.Class)
                            sym = FindFree(p.Parent, name, p) ?? new Sym { Name = name, Kind = SymKind.Global, Owner = p };
                        else sym = Resolve(p, name);
                    }
                    break;
            }
            s.Syms[name] = sym;
            return sym;
        }

        /// <summary>Looks for <paramref name="name"/> bound in an enclosing function / comprehension; marks it as a cell.</summary>
        Sym FindFree(Scope start, string name, Scope requester)
        {
            var chain = new List<Scope>();
            Sym owner = null;
            for (var q = start; q != null; q = q.Parent)
            {
                if (q.Kind == ScopeKind.Module) return null;
                if (q.Kind == ScopeKind.Class) { chain.Add(q); continue; }
                if (q.Kind == ScopeKind.Comp)
                {
                    if (q.Bound.Contains(name)) { owner = Resolve(q, name); break; }
                    continue;
                }
                // function
                if (q.Globals.Contains(name)) return null;
                if (q.Bound.Contains(name) && !q.Nonlocals.Contains(name)) { owner = Resolve(q, name); break; }
                chain.Add(q);
            }
            if (owner == null) return null;
            if (owner.Kind == SymKind.Local) owner.Kind = SymKind.Cell;
            foreach (var c in chain)
            {
                var f = c.AddFree(name);
                if (c.Kind == ScopeKind.Function && !c.Syms.ContainsKey(name)) c.Syms[name] = f;
            }
            return requester.AddFree(name);
        }
    }
}
