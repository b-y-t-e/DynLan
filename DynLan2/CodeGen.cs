using System;
using System.Collections.Generic;
using System.Numerics;

namespace DynLan2
{
    /// <summary>AST → register bytecode, one code unit at a time (called lazily on first use).</summary>
    internal sealed class CodeGen
    {
        readonly FunctionCode _fc;
        readonly Scope _unit;
        Scope _cur;

        readonly List<int> _code = new List<int>(64);
        readonly List<int> _lines = new List<int>(64);
        readonly List<Value> _consts = new List<Value>();
        readonly Dictionary<(string, object), int> _constMap = new Dictionary<(string, object), int>();
        readonly List<string> _names = new List<string>();
        readonly Dictionary<string, int> _nameMap = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<string> _gnames = new List<string>();
        readonly Dictionary<string, int> _gnameMap = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<Handler> _handlers = new List<Handler>();
        readonly List<ChildRef> _children = new List<ChildRef>();
        readonly List<Loop> _loops = new List<Loop>();
        readonly List<Prot> _prots = new List<Prot>();
        readonly List<int> _excRegs = new List<int>();
        HashSet<Sym> _assigned = new HashSet<Sym>();

        int _nextReg, _maxReg;
        int _line;
        bool _hasYield, _hasClass;

        sealed class Loop
        {
            public int ContinueTarget = -1;
            public List<int> ContinuePatches = new List<int>();
            public List<int> BreakPatches = new List<int>();
            public int ProtDepth;
        }

        sealed class Prot
        {
            public bool IsFinally;
            public Action Body;
            public int SegStart;
            public List<(int, int)> Segs = new List<(int, int)>();
        }

        CodeGen(FunctionCode fc)
        {
            _fc = fc;
            _unit = fc.Scope;
            _cur = _unit;
        }

        public static void Compile(FunctionCode fc) => new CodeGen(fc).Run();

        ScriptSyntaxException Error(string msg, Node n = null) =>
            new ScriptSyntaxException(msg, _fc.Script.File, n?.Line ?? _line, 0);

        // ------------------------------------------------------------ driver
        void Run()
        {
            AllocLocals();
            var node = _unit.Node;
            _line = node.Line;
            // prologue: cells and parameter type checks
            foreach (var name in _unit.ParamNames)
            {
                var s = _unit.Syms[name];
                if (s.Kind == SymKind.Cell) E(Op.MKCELL, s.Reg);
                else if (s.TypeHint != 0) E(Op.CHECKTYPE, s.Reg, s.TypeHint);
                _assigned.Add(s);
            }
            foreach (var name in _unit.Bound.Items)
            {
                var s = _unit.Syms[name];
                if (s.Kind == SymKind.Cell && s.Owner == _unit && !s.IsParam) E(Op.MKCELL, s.Reg);
            }

            switch (node)
            {
                case ModuleNode m:
                    Stmts(m.Body);
                    E(Op.RETNONE);
                    break;
                case FuncDef fd:
                    Stmts(fd.Body);
                    E(Op.RETNONE);
                    break;
                case ClassDef cd:
                    Stmts(cd.Body);
                    E(Op.RETNONE);
                    break;
                case LambdaExpr l:
                    {
                        int r = Expr(l.Body);
                        E(Op.RET, r);
                        break;
                    }
                case CompExpr c when c.Kind == 'g':
                    GenExpBody(c);
                    E(Op.RETNONE);
                    break;
                default:
                    throw Error("cannot compile unit");
            }

            _fc.Code = _code.ToArray();
            _fc.Lines = _lines.ToArray();
            _fc.Consts = _consts.ToArray();
            _fc.Names = _names.ToArray();
            _fc.GlobalNames = _gnames.ToArray();
            _fc.Handlers = _handlers.ToArray();
            _fc.Children = _children.ToArray();
            _fc.NumRegs = Math.Max(_maxReg, 1);
            _fc.HasYield = _hasYield;
            _fc.HasHandlers = _handlers.Count > 0;
            _fc.HasClassDef = _hasClass;
            var types = new byte[_fc.NumRegs];
            foreach (var s in AllUnitSyms())
                if (s.Kind == SymKind.Local && s.TypeHint != 0 && s.Reg >= 0 && _unit.Kind == ScopeKind.Function)
                    types[s.Reg] = s.TypeHint;
            _fc.RegTypes = types;
        }

        IEnumerable<Sym> AllUnitSyms()
        {
            foreach (var s in _unit.Syms.Values) if (s.Owner == _unit) yield return s;
            foreach (var c in InlineComps(_unit))
                foreach (var s in c.Syms.Values) if (s.Owner == c) yield return s;
        }

        static IEnumerable<Scope> InlineComps(Scope s)
        {
            foreach (var c in s.Children)
                if (c.Kind == ScopeKind.Comp)
                {
                    yield return c;
                    foreach (var cc in InlineComps(c)) yield return cc;
                }
        }

        void AllocLocals()
        {
            int r = 0;
            foreach (var name in _unit.ParamNames) _unit.Syms[name].Reg = r++;
            foreach (var name in _unit.Bound.Items)
            {
                var s = _unit.Syms[name];
                if ((s.Kind == SymKind.Local || s.Kind == SymKind.Cell) && s.Owner == _unit && s.Reg < 0) s.Reg = r++;
            }
            foreach (var c in InlineComps(_unit))
                foreach (var name in c.Bound.Items)
                {
                    var s = c.Syms[name];
                    if ((s.Kind == SymKind.Local || s.Kind == SymKind.Cell) && s.Owner == c && s.Reg < 0) s.Reg = r++;
                }
            _nextReg = _maxReg = r;
        }

        // ------------------------------------------------------------ emit helpers
        int Pc => _code.Count;

        void W(int x) { _code.Add(x); _lines.Add(_line); }
        void E(Op op) { W((int)op); }
        void E(Op op, int a) { W((int)op); W(a); }
        void E(Op op, int a, int b) { W((int)op); W(a); W(b); }
        void E(Op op, int a, int b, int c) { W((int)op); W(a); W(b); W(c); }
        void E(Op op, int a, int b, int c, int d) { W((int)op); W(a); W(b); W(c); W(d); }
        void E(Op op, int a, int b, int c, int d, int e) { W((int)op); W(a); W(b); W(c); W(d); W(e); }
        void E(Op op, int a, int b, int c, int d, int e, int f) { W((int)op); W(a); W(b); W(c); W(d); W(e); W(f); }

        /// <summary>Emits a jump; returns the operand position to patch.</summary>
        int Jmp() { E(Op.JMP, -1); return Pc - 1; }
        int JmpIf(Op op, int c) { E(op, c, -1); return Pc - 1; }
        void Patch(int pos, int target) => _code[pos] = target;
        void PatchHere(List<int> ps) { foreach (var p in ps) _code[p] = Pc; ps.Clear(); }

        int Temp()
        {
            int r = _nextReg++;
            if (_nextReg > _maxReg) _maxReg = _nextReg;
            return r;
        }

        int Temps(int n)
        {
            int r = _nextReg;
            _nextReg += n;
            if (_nextReg > _maxReg) _maxReg = _nextReg;
            return r;
        }

        int D(int dst) => dst >= 0 ? dst : Temp();

        int K(Value v)
        {
            (string, object) key;
            if (v.IsUnbound) key = ("unbound", null);
            else if (v.O is Tag t) key = (t.Name, v.N);
            else if (v.O is string s) key = ("str", s);
            else if (v.O is BigInteger b) key = ("big", b);
            else { _consts.Add(v); return _consts.Count - 1; }
            if (_constMap.TryGetValue(key, out int i)) return i;
            _consts.Add(v);
            _constMap[key] = _consts.Count - 1;
            return _consts.Count - 1;
        }

        int NameIdx(string n)
        {
            if (_nameMap.TryGetValue(n, out int i)) return i;
            _names.Add(n);
            return _nameMap[n] = _names.Count - 1;
        }

        int GIdx(string n)
        {
            if (_gnameMap.TryGetValue(n, out int i)) return i;
            _gnames.Add(n);
            return _gnameMap[n] = _gnames.Count - 1;
        }

        Sym Lookup(string name)
        {
            if (_cur.Syms.TryGetValue(name, out var s)) return s;
            // names introduced only syntactically (e.g. "super" check) default to global
            return new Sym { Name = name, Kind = SymKind.Global, Owner = _cur };
        }

        // ------------------------------------------------------------ statements
        void Stmts(List<Stmt> body)
        {
            if (body == null) return;
            foreach (var s in body) Stmt(s);
        }

        void Stmt(Stmt s)
        {
            int mark = _nextReg;
            _line = s.Line;
            switch (s)
            {
                case ExprStmt e:
                    if (e.X is ConstExpr) break; // docstrings / bare constants
                    Expr(e.X);
                    break;
                case AssignStmt a: Assign(a); break;
                case AugAssignStmt a: AugAssign(a); break;
                case AnnAssignStmt a:
                    if (a.Value != null) Assign(new AssignStmt { Targets = new List<Expr> { a.Target }, Value = a.Value, Line = a.Line });
                    break;
                case IfStmt i: If(i); break;
                case WhileStmt w: While(w); break;
                case ForStmt f: For(f); break;
                case BreakStmt _:
                    {
                        if (_loops.Count == 0) throw Error("'break' outside loop", s);
                        var lp = _loops[_loops.Count - 1];
                        Unwind(lp.ProtDepth, () => lp.BreakPatches.Add(Jmp()));
                        break;
                    }
                case ContinueStmt _:
                    {
                        if (_loops.Count == 0) throw Error("'continue' not properly in loop", s);
                        var lp = _loops[_loops.Count - 1];
                        Unwind(lp.ProtDepth, () =>
                        {
                            if (lp.ContinueTarget >= 0) E(Op.JMP, lp.ContinueTarget);
                            else lp.ContinuePatches.Add(Jmp());
                        });
                        break;
                    }
                case PassStmt _: break;
                case ReturnStmt r:
                    {
                        if (_fc.IsGenerator && r.Value != null && !(r.Value is ConstExpr ce && ce.Value.IsNone))
                        {
                            int v0 = Expr(r.Value);
                            Unwind(0, () => E(Op.RET, v0));
                            break;
                        }
                        if (r.Value == null) { Unwind(0, () => E(Op.RETNONE)); break; }
                        int v = Expr(r.Value);
                        if (_prots.Count > 0 && v < _nextReg && IsVarReg(v))
                        {
                            // value lives in a variable that a finally block could change: copy first
                            int t = Temp();
                            E(Op.MOVE, t, v);
                            v = t;
                        }
                        Unwind(0, () => E(Op.RET, v));
                        break;
                    }
                case RaiseStmt r:
                    if (r.Exc == null)
                    {
                        E(Op.RERAISE, _excRegs.Count > 0 ? _excRegs[_excRegs.Count - 1] : -1);
                    }
                    else if (r.Cause != null)
                    {
                        int x = Expr(r.Exc);
                        int c = Expr(r.Cause);
                        E(Op.RAISEFROM, x, c);
                    }
                    else E(Op.RAISE, Expr(r.Exc));
                    break;
                case GlobalStmt _:
                case NonlocalStmt _:
                    break;
                case DelStmt d: foreach (var t in d.Targets) Del(t); break;
                case AssertStmt a:
                    {
                        var ok = new List<int>();
                        CondJump(a.Test, true, ok);
                        E(Op.ASSERTFAIL, a.Msg != null ? Expr(a.Msg) : -1);
                        PatchHere(ok);
                        break;
                    }
                case ImportStmt im:
                    foreach (var (m, asName) in im.Names)
                    {
                        int t = Temp();
                        E(Op.IMPORT, t, NameIdx(m));
                        StoreName(asName ?? m.Split('.')[0], t);
                    }
                    break;
                case FromImportStmt fi:
                    {
                        int m = Temp();
                        E(Op.IMPORT, m, NameIdx(fi.Module));
                        foreach (var (n, asName) in fi.Names)
                        {
                            int t = Temp();
                            E(Op.IMPORTFROM, t, m, NameIdx(n));
                            StoreName(asName ?? n, t);
                        }
                        break;
                    }
                case FuncDef fd:
                    {
                        var decs = new List<int>();
                        foreach (var d in fd.Decorators) decs.Add(Expr(d));
                        int f = Temp();
                        MakeFunction(fd.Scope, fd.Args, f);
                        for (int i = decs.Count - 1; i >= 0; i--) E(Op.CALL, f, decs[i], f, 1);
                        StoreName(fd.Name, f);
                        break;
                    }
                case ClassDef cd: ClassDefStmt(cd); break;
                case TryStmt t: Try(t); break;
                case WithStmt w: With(w, 0); break;
                default: throw Error("unsupported statement", s);
            }
            _nextReg = mark;
        }

        bool IsVarReg(int r)
        {
            foreach (var s in AllUnitSyms()) if (s.Reg == r) return true;
            return false;
        }

        void Assign(AssignStmt a)
        {
            if (a.Targets.Count == 1)
            {
                var t = a.Targets[0];
                if (t is NameExpr n)
                {
                    var sym = Lookup(n.Id);
                    if (sym.Kind == SymKind.Local && IsSingleWrite(a.Value))
                    {
                        Expr(a.Value, sym.Reg);
                        _assigned.Add(sym);
                        if (sym.TypeHint != 0) E(Op.CHECKTYPE, sym.Reg, sym.TypeHint);
                        return;
                    }
                }
                // a, b = b, a  (parallel assignment without building a tuple)
                if ((t is TupleExpr || t is ListExpr) && a.Value is TupleExpr vt)
                {
                    var items = t is TupleExpr tt ? tt.Items : ((ListExpr)t).Items;
                    if (items.Count == vt.Items.Count && !items.Exists(x => x is StarredExpr) && !vt.Items.Exists(x => x is StarredExpr))
                    {
                        int b = Temps(items.Count);
                        for (int i = 0; i < items.Count; i++) Expr(vt.Items[i], b + i);
                        for (int i = 0; i < items.Count; i++) StoreTarget(items[i], b + i);
                        return;
                    }
                }
            }
            int v = Expr(a.Value);
            foreach (var t in a.Targets) StoreTarget(t, v);
        }

        static bool IsSingleWrite(Expr e)
        {
            switch (e)
            {
                case ConstExpr _: case NameExpr _: case BinExpr _: case CallExpr _: case AttrExpr _:
                case IndexExpr _: case LambdaExpr _: case FStringExpr _: case YieldExpr y when !y.From:
                    return true;
                case UnaryExpr _: return true;
                case CompareExpr c: return c.Ops.Count == 1;
                case ListExpr l: return l.Items.Count <= 256 && !l.Items.Exists(x => x is StarredExpr);
                case TupleExpr t: return t.Items.Count <= 256 && !t.Items.Exists(x => x is StarredExpr);
                case SetExpr s: return s.Items.Count <= 256 && !s.Items.Exists(x => x is StarredExpr);
                case DictExpr d: return d.Keys.Count <= 128 && !d.Keys.Contains(null);
                default: return false;
            }
        }

        void AugAssign(AugAssignStmt a)
        {
            Op op = a.Op == "+" ? Op.IADD : BinOp(a.Op);
            switch (a.Target)
            {
                case NameExpr n:
                    {
                        var sym = Lookup(n.Id);
                        if (sym.Kind == SymKind.Local)
                        {
                            if (!_assigned.Contains(sym)) E(Op.CHKBOUND, sym.Reg, NameIdx(n.Id));
                            int v = Rk(a.Value);
                            E(op, sym.Reg, sym.Reg, v);
                            _assigned.Add(sym);
                            if (sym.TypeHint != 0) E(Op.CHECKTYPE, sym.Reg, sym.TypeHint);
                        }
                        else
                        {
                            int cur = LoadName(n.Id, -1);
                            int v = Rk(a.Value);
                            int t = Temp();
                            E(op, t, cur, v);
                            StoreName(n.Id, t);
                        }
                        break;
                    }
                case AttrExpr at:
                    {
                        int o = Expr(at.Obj);
                        int t = Temp();
                        int ni = NameIdx(at.Name);
                        E(Op.GETATTR, t, o, ni);
                        int v = Rk(a.Value);
                        E(op, t, t, v);
                        E(Op.SETATTR, o, ni, t);
                        break;
                    }
                case IndexExpr ix:
                    {
                        int o = Expr(ix.Obj);
                        int k = Index(ix.Index);
                        int t = Temp();
                        E(Op.GETITEM, t, o, k);
                        int v = Rk(a.Value);
                        E(op, t, t, v);
                        E(Op.SETITEM, o, k, t);
                        break;
                    }
                default: throw Error("illegal expression for augmented assignment", a);
            }
        }

        void If(IfStmt s)
        {
            var elseP = new List<int>();
            CondJump(s.Test, false, elseP);
            var pre = new HashSet<Sym>(_assigned);
            Stmts(s.Body);
            if (s.Else != null && s.Else.Count > 0)
            {
                var afterBody = _assigned;
                _assigned = pre;
                int j = Jmp();
                PatchHere(elseP);
                _line = s.Line;
                Stmts(s.Else);
                _assigned.IntersectWith(afterBody);
                Patch(j, Pc);
            }
            else
            {
                PatchHere(elseP);
                _assigned = pre;
            }
        }

        void While(WhileStmt w)
        {
            var pre = new HashSet<Sym>(_assigned);
            var lp = new Loop { ProtDepth = _prots.Count };
            bool infinite = w.Test is ConstExpr c && Ops.Truthy(c.Value);
            int jcond = infinite ? -1 : Jmp();
            int top = Pc;
            if (infinite) lp.ContinueTarget = top;
            _loops.Add(lp);
            Stmts(w.Body);
            _loops.RemoveAt(_loops.Count - 1);
            _assigned = new HashSet<Sym>(pre);
            _line = w.Line;
            if (infinite) E(Op.JMP, top);
            else
            {
                Patch(jcond, Pc);
                PatchHere(lp.ContinuePatches);
                var back = new List<int>();
                CondJump(w.Test, true, back);
                foreach (var p in back) Patch(p, top);
            }
            Stmts(w.Else);
            _assigned = pre;
            PatchHere(lp.BreakPatches);
        }

        bool IsRangeCall(Expr e, out List<Expr> args)
        {
            args = null;
            if (e is CallExpr c && c.Func is NameExpr n && n.Id == "range" && c.Keywords.Count == 0
                && c.Args.Count >= 1 && c.Args.Count <= 3 && !c.Args.Exists(a => a is StarredExpr)
                && Lookup("range").Kind == SymKind.Global)
            {
                args = c.Args;
                return true;
            }
            return false;
        }

        /// <summary>Register the loop writes into directly, or -1 if a separate store is needed.</summary>
        int DirectTarget(Expr target)
        {
            if (target is NameExpr n)
            {
                var s = Lookup(n.Id);
                if (s.Kind == SymKind.Local) return s.Reg;
            }
            return -1;
        }

        void ForTargetStore(Expr target, int direct, int tmp)
        {
            if (direct >= 0)
            {
                var s = Lookup(((NameExpr)target).Id);
                _assigned.Add(s);
                if (s.TypeHint != 0) E(Op.CHECKTYPE, s.Reg, s.TypeHint);
            }
            else StoreTarget(target, tmp);
        }

        void For(ForStmt f)
        {
            var pre = new HashSet<Sym>(_assigned);
            int top, exitPos;
            int direct = DirectTarget(f.Target);
            int tmp = direct >= 0 ? direct : Temp();
            if (IsRangeCall(f.Iter, out var args))
            {
                int it = Temps(3);
                int fr = Temp();
                E(Op.LOADGLOBAL, fr, GIdx("range"));
                int b = Temps(args.Count);
                for (int i = 0; i < args.Count; i++) Expr(args[i], b + i);
                E(Op.FORPREP_RANGE, it, fr, b, args.Count);
                top = Pc;
                E(Op.FORITER_RANGE, it, tmp, -1);
                exitPos = Pc - 1;
            }
            else
            {
                int it = Temp();
                int src = Expr(f.Iter);
                E(Op.GETITER, it, src);
                top = Pc;
                E(Op.FORITER, it, tmp, -1);
                exitPos = Pc - 1;
            }
            ForTargetStore(f.Target, direct, tmp);
            var lp = new Loop { ContinueTarget = top, ProtDepth = _prots.Count };
            _loops.Add(lp);
            Stmts(f.Body);
            _loops.RemoveAt(_loops.Count - 1);
            _line = f.Line;
            E(Op.JMP, top);
            Patch(exitPos, Pc);
            _assigned = new HashSet<Sym>(pre);
            Stmts(f.Else);
            _assigned = pre;
            PatchHere(lp.BreakPatches);
        }

        // ---- try / finally with segmented protected ranges
        void ProtBegin(Prot p) { p.SegStart = Pc; _prots.Add(p); }

        void ProtClose(Prot p)
        {
            if (Pc > p.SegStart) p.Segs.Add((p.SegStart, Pc));
            p.SegStart = Pc;
        }

        void ProtEnd(Prot p)
        {
            ProtClose(p);
            _prots.Remove(p);
        }

        /// <summary>Leaves protected regions down to <paramref name="depth"/>, running finally bodies, then emits the exit jump.</summary>
        void Unwind(int depth, Action exit)
        {
            if (_prots.Count <= depth) { exit(); return; }
            var saved = _prots.GetRange(depth, _prots.Count - depth);
            var savedLoops = new List<Loop>(_loops);
            for (int k = saved.Count - 1; k >= 0; k--)
            {
                var p = saved[k];
                ProtClose(p);
                _prots.RemoveAt(_prots.Count - 1);
                if (p.IsFinally) p.Body();
            }
            exit();
            foreach (var p in saved) { p.SegStart = Pc; _prots.Add(p); }
            _loops.Clear();
            _loops.AddRange(savedLoops);
        }

        void Try(TryStmt t)
        {
            if (t.Finally == null) { TryExcept(t); return; }
            int exc = Temp();
            var fin = new Prot { IsFinally = true };
            fin.Body = () =>
            {
                int mark = _nextReg;
                Stmts(t.Finally);
                _nextReg = mark;
            };
            ProtBegin(fin);
            TryExcept(t);
            ProtEnd(fin);
            Stmts(t.Finally);
            int j = Jmp();
            int handler = Pc;
            foreach (var (a, b) in fin.Segs) _handlers.Add(new Handler { Start = a, End = b, Target = handler, ExcReg = exc });
            _excRegs.Add(exc);
            Stmts(t.Finally);
            _excRegs.RemoveAt(_excRegs.Count - 1);
            E(Op.RERAISE, exc);
            Patch(j, Pc);
        }

        void TryExcept(TryStmt t)
        {
            if (t.Handlers.Count == 0) { Stmts(t.Body); return; }
            int exc = Temp();
            var p = new Prot();
            var pre = new HashSet<Sym>(_assigned);
            ProtBegin(p);
            Stmts(t.Body);
            ProtEnd(p);
            Stmts(t.Else);
            var ends = new List<int> { Jmp() };
            int handler = Pc;
            foreach (var (a, b) in p.Segs) _handlers.Add(new Handler { Start = a, End = b, Target = handler, ExcReg = exc });
            foreach (var h in t.Handlers)
            {
                _assigned = new HashSet<Sym>(pre);
                _line = h.Line;
                int next = -1;
                int mark = _nextReg;
                if (h.Type != null)
                {
                    int tr = Expr(h.Type);
                    int m = Temp();
                    E(Op.EXCMATCH, m, exc, tr);
                    next = JmpIf(Op.JF, m);
                }
                if (h.Name != null) StoreName(h.Name, exc);
                _excRegs.Add(exc);
                Stmts(h.Body);
                _excRegs.RemoveAt(_excRegs.Count - 1);
                _nextReg = mark;
                ends.Add(Jmp());
                if (next >= 0) Patch(next, Pc);
            }
            E(Op.RERAISE, exc);
            PatchHere(ends);
            _assigned = pre;
        }

        void With(WithStmt w, int index)
        {
            if (index >= w.Items.Count) { Stmts(w.Body); return; }
            var item = w.Items[index];
            int mgr = Temp();
            Expr(item.Context, mgr);
            int b = Temps(1);
            E(Op.MOVE, b, mgr);
            int entered = Temp();
            E(Op.CALLM, entered, b, NameIdx("__enter__"), 0);
            if (item.Target != null) StoreTarget(item.Target, entered);
            int exc = Temp();
            var fin = new Prot { IsFinally = true };
            fin.Body = () =>
            {
                int mark = _nextReg;
                int bb = Temps(4);
                E(Op.MOVE, bb, mgr);
                int none = K(Value.None);
                E(Op.LOADK, bb + 1, none);
                E(Op.LOADK, bb + 2, none);
                E(Op.LOADK, bb + 3, none);
                E(Op.CALLM, bb, bb, NameIdx("__exit__"), 3);
                _nextReg = mark;
            };
            ProtBegin(fin);
            With(w, index + 1);
            ProtEnd(fin);
            fin.Body();
            int j = Jmp();
            int handler = Pc;
            foreach (var (a, c) in fin.Segs) _handlers.Add(new Handler { Start = a, End = c, Target = handler, ExcReg = exc });
            fin.Body();
            E(Op.RERAISE, exc);
            Patch(j, Pc);
        }

        void ClassDefStmt(ClassDef cd)
        {
            _hasClass = true;
            var decs = new List<int>();
            foreach (var d in cd.Decorators) decs.Add(Expr(d));
            int b = Temps(cd.Bases.Count);
            for (int i = 0; i < cd.Bases.Count; i++)
            {
                if (cd.Bases[i] is StarredExpr) throw Error("starred bases are not supported", cd);
                Expr(cd.Bases[i], b + i);
            }
            int child = Child(cd.Scope);
            int d0 = Temp();
            E(Op.MAKECLASS, d0, child, b, cd.Bases.Count);
            for (int i = decs.Count - 1; i >= 0; i--) E(Op.CALL, d0, decs[i], d0, 1);
            StoreName(cd.Name, d0);
        }

        int Child(Scope scope)
        {
            var code = _fc.Script.GetCode(scope.CodeKey);
            var free = new int[scope.FreeNames.Count];
            for (int i = 0; i < free.Length; i++) free[i] = ClosureSource(scope.FreeNames[i]);
            _children.Add(new ChildRef { Code = code, FreeSources = free });
            return _children.Count - 1;
        }

        int ClosureSource(string name)
        {
            for (var s = _cur; s.Kind == ScopeKind.Comp; s = s.Parent)
                if (s.Bound.Contains(name))
                {
                    var sym = s.Syms[name];
                    if (sym.Kind != SymKind.Cell) throw Error("internal: closure variable '" + name + "' is not a cell");
                    return sym.Reg;
                }
            if (_unit.Syms.TryGetValue(name, out var us) && us.Kind == SymKind.Cell && us.Owner == _unit) return us.Reg;
            if (_unit.FreeSyms.TryGetValue(name, out var fs)) return ~fs.FreeIndex;
            throw Error("internal: cannot resolve closure variable '" + name + "'");
        }

        void MakeFunction(Scope scope, Params args, int dst)
        {
            var defs = args.Positional.FindAll(p => p.Default != null);
            int db = Temps(defs.Count);
            for (int i = 0; i < defs.Count; i++) Expr(defs[i].Default, db + i);
            int kb = Temps(args.KwOnly.Count);
            for (int i = 0; i < args.KwOnly.Count; i++)
            {
                if (args.KwOnly[i].Default != null) Expr(args.KwOnly[i].Default, kb + i);
                else E(Op.LOADK, kb + i, K(Value.Unbound));
            }
            int child = Child(scope);
            E(Op.MAKEFUNC, dst, child, db, defs.Count, kb, args.KwOnly.Count);
        }

        // ------------------------------------------------------------ names
        int LoadName(string name, int dst)
        {
            var s = Lookup(name);
            switch (s.Kind)
            {
                case SymKind.Local:
                    if (!_assigned.Contains(s)) E(Op.CHKBOUND, s.Reg, NameIdx(name));
                    if (dst >= 0 && dst != s.Reg) { E(Op.MOVE, dst, s.Reg); return dst; }
                    return s.Reg;
                case SymKind.Cell:
                    {
                        int d = D(dst);
                        E(Op.LOADCELL, d, s.Reg);
                        return d;
                    }
                case SymKind.Free:
                    {
                        int d = D(dst);
                        E(Op.LOADDEREF, d, s.FreeIndex);
                        return d;
                    }
                case SymKind.ClassNs:
                    {
                        int d = D(dst);
                        E(Op.LOADNAME, d, NameIdx(name));
                        return d;
                    }
                default:
                    {
                        int d = D(dst);
                        if (_unit.Kind == ScopeKind.Class) E(Op.LOADNAME, d, NameIdx(name));
                        else E(Op.LOADGLOBAL, d, GIdx(name));
                        return d;
                    }
            }
        }

        void StoreName(string name, int src)
        {
            var s = Lookup(name);
            switch (s.Kind)
            {
                case SymKind.Local:
                    if (src != s.Reg) E(Op.MOVE, s.Reg, src);
                    _assigned.Add(s);
                    if (s.TypeHint != 0) E(Op.CHECKTYPE, s.Reg, s.TypeHint);
                    break;
                case SymKind.Cell: E(Op.STORECELL, s.Reg, src); break;
                case SymKind.Free: E(Op.STOREDEREF, s.FreeIndex, src); break;
                case SymKind.ClassNs: E(Op.STORENAME, NameIdx(name), src); break;
                default: E(Op.STOREGLOBAL, GIdx(name), src); break;
            }
        }

        void StoreTarget(Expr t, int src)
        {
            switch (t)
            {
                case NameExpr n: StoreName(n.Id, src); break;
                case AttrExpr a:
                    {
                        int o = Expr(a.Obj);
                        E(Op.SETATTR, o, NameIdx(a.Name), src);
                        break;
                    }
                case IndexExpr ix:
                    {
                        int o = Expr(ix.Obj);
                        int k = Index(ix.Index);
                        E(Op.SETITEM, o, k, src);
                        break;
                    }
                case TupleExpr _:
                case ListExpr _:
                    {
                        var items = t is TupleExpr tt ? tt.Items : ((ListExpr)t).Items;
                        int star = items.FindIndex(x => x is StarredExpr);
                        int b = Temps(items.Count);
                        if (star < 0) E(Op.UNPACK, b, items.Count, src);
                        else
                        {
                            if (items.FindLastIndex(x => x is StarredExpr) != star) throw Error("multiple starred expressions in assignment", t);
                            E(Op.UNPACKEX, b, star, items.Count - star - 1, src);
                        }
                        for (int i = 0; i < items.Count; i++)
                            StoreTarget(items[i] is StarredExpr se ? se.Value : items[i], b + i);
                        break;
                    }
                default: throw Error("cannot assign to expression", t);
            }
        }

        void Del(Expr t)
        {
            switch (t)
            {
                case NameExpr n:
                    {
                        var s = Lookup(n.Id);
                        switch (s.Kind)
                        {
                            case SymKind.Local:
                                if (!_assigned.Contains(s)) E(Op.CHKBOUND, s.Reg, NameIdx(n.Id));
                                E(Op.DELLOCAL, s.Reg);
                                _assigned.Remove(s);
                                break;
                            case SymKind.Cell: E(Op.DELCELL, s.Reg); break;
                            case SymKind.ClassNs: E(Op.DELNAME, NameIdx(n.Id)); break;
                            case SymKind.Global: E(Op.DELGLOBAL, GIdx(n.Id)); break;
                            default: throw Error("cannot delete free variable '" + n.Id + "'", t);
                        }
                        break;
                    }
                case AttrExpr a: E(Op.DELATTR, Expr(a.Obj), NameIdx(a.Name)); break;
                case IndexExpr ix:
                    {
                        int o = Expr(ix.Obj);
                        E(Op.DELITEM, o, Index(ix.Index));
                        break;
                    }
                case TupleExpr tu: foreach (var x in tu.Items) Del(x); break;
                case ListExpr l: foreach (var x in l.Items) Del(x); break;
                default: throw Error("cannot delete expression", t);
            }
        }

        // ------------------------------------------------------------ conditions
        static Cmp? CmpOf(string op)
        {
            switch (op)
            {
                case "<": return Cmp.LT;
                case "<=": return Cmp.LE;
                case ">": return Cmp.GT;
                case ">=": return Cmp.GE;
                case "==": return Cmp.EQ;
                case "!=": return Cmp.NE;
                default: return null;
            }
        }

        /// <summary>Emits code that jumps (patch list) when truthiness of <paramref name="e"/> equals <paramref name="jumpIf"/>.</summary>
        void CondJump(Expr e, bool jumpIf, List<int> patches)
        {
            int mark = _nextReg;
            switch (e)
            {
                case UnaryExpr u when u.Op == "not":
                    CondJump(u.X, !jumpIf, patches);
                    break;
                case BoolExpr b when b.IsAnd:
                    if (!jumpIf) foreach (var v in b.Values) CondJump(v, false, patches);
                    else
                    {
                        var skip = new List<int>();
                        for (int i = 0; i < b.Values.Count - 1; i++) CondJump(b.Values[i], false, skip);
                        CondJump(b.Values[b.Values.Count - 1], true, patches);
                        PatchHere(skip);
                    }
                    break;
                case BoolExpr b:
                    if (jumpIf) foreach (var v in b.Values) CondJump(v, true, patches);
                    else
                    {
                        var skip = new List<int>();
                        for (int i = 0; i < b.Values.Count - 1; i++) CondJump(b.Values[i], true, skip);
                        CondJump(b.Values[b.Values.Count - 1], false, patches);
                        PatchHere(skip);
                    }
                    break;
                case CompareExpr c when c.Ops.Count == 1 && CmpOf(c.Ops[0]).HasValue:
                    {
                        int a = Rk(c.Left);
                        int bb = Rk(c.Rights[0]);
                        E(jumpIf ? Op.JCMP : Op.JNCMP, (int)CmpOf(c.Ops[0]).Value, a, bb, -1);
                        patches.Add(Pc - 1);
                        break;
                    }
                case ConstExpr k:
                    if (Ops.Truthy(k.Value) == jumpIf) patches.Add(Jmp());
                    break;
                default:
                    {
                        int r = Expr(e);
                        patches.Add(JmpIf(jumpIf ? Op.JT : Op.JF, r));
                        break;
                    }
            }
            _nextReg = mark;
        }

        // ------------------------------------------------------------ expressions
        static bool IsSimpleConst(Expr e, out Value v)
        {
            if (e is ConstExpr c) { v = c.Value; return true; }
            v = default;
            return false;
        }

        /// <summary>Register-or-constant operand.</summary>
        int Rk(Expr e)
        {
            if (IsSimpleConst(e, out var v)) return ~K(v);
            return Expr(e);
        }

        static Op BinOp(string op)
        {
            switch (op)
            {
                case "+": return Op.ADD;
                case "-": return Op.SUB;
                case "*": return Op.MUL;
                case "/": return Op.TRUEDIV;
                case "//": return Op.FLOORDIV;
                case "%": return Op.MOD;
                case "**": return Op.POW;
                case "<<": return Op.LSHIFT;
                case ">>": return Op.RSHIFT;
                case "&": return Op.BITAND;
                case "|": return Op.BITOR;
                case "^": return Op.BITXOR;
                case "@": return Op.MATMUL;
                default: throw new InvalidOperationException(op);
            }
        }

        static Op CmpOp(string op)
        {
            switch (op)
            {
                case "<": return Op.LT;
                case "<=": return Op.LE;
                case ">": return Op.GT;
                case ">=": return Op.GE;
                case "==": return Op.EQ;
                case "!=": return Op.NE;
                case "in": return Op.IN;
                case "not in": return Op.NOTIN;
                case "is": return Op.IS;
                case "is not": return Op.ISNOT;
                default: throw new InvalidOperationException(op);
            }
        }

        int Index(Expr idx)
        {
            if (idx is SliceExpr s) return Slice(s, -1);
            return Expr(idx);
        }

        int Slice(SliceExpr s, int dst)
        {
            int lo = s.Lo != null ? Expr(s.Lo) : -1;
            int hi = s.Hi != null ? Expr(s.Hi) : -1;
            int st = s.Step != null ? Expr(s.Step) : -1;
            int d = D(dst);
            E(Op.SLICE, d, lo, hi, st);
            return d;
        }

        int Expr(Expr e, int dst = -1)
        {
            int saveLine = _line;
            if (e.Line > 0) _line = e.Line;
            try
            {
                return ExprCore(e, dst);
            }
            finally { _line = saveLine; }
        }

        int ExprCore(Expr e, int dst)
        {
            switch (e)
            {
                case ConstExpr c:
                    {
                        int d = D(dst);
                        E(Op.LOADK, d, K(c.Value));
                        return d;
                    }
                case NameExpr n:
                    return LoadName(n.Id, dst);
                case BinExpr b:
                    {
                        int a = Rk(b.L);
                        int r = Rk(b.R);
                        int d = D(dst);
                        E(BinOp(b.Op), d, a, r);
                        return d;
                    }
                case UnaryExpr u:
                    {
                        int x = Expr(u.X);
                        int d = D(dst);
                        E(u.Op == "-" ? Op.NEG : u.Op == "+" ? Op.POS : u.Op == "~" ? Op.INVERT : Op.NOT, d, x);
                        return d;
                    }
                case CompareExpr c:
                    {
                        if (c.Ops.Count == 1)
                        {
                            int a = Rk(c.Left);
                            int r = Rk(c.Rights[0]);
                            int d = D(dst);
                            E(CmpOp(c.Ops[0]), d, a, r);
                            return d;
                        }
                        int d2 = D(dst);
                        var end = new List<int>();
                        int left = Expr(c.Left);
                        for (int i = 0; i < c.Ops.Count; i++)
                        {
                            int right = Expr(c.Rights[i]);
                            if (i < c.Ops.Count - 1 && right == d2) { int t = Temp(); E(Op.MOVE, t, right); right = t; }
                            E(CmpOp(c.Ops[i]), d2, left, right);
                            if (i < c.Ops.Count - 1) end.Add(JmpIf(Op.JF, d2));
                            left = right;
                        }
                        PatchHere(end);
                        return d2;
                    }
                case BoolExpr b:
                    {
                        int d = D(dst);
                        var end = new List<int>();
                        for (int i = 0; i < b.Values.Count; i++)
                        {
                            Expr(b.Values[i], d);
                            if (i < b.Values.Count - 1) end.Add(JmpIf(b.IsAnd ? Op.JF : Op.JT, d));
                        }
                        PatchHere(end);
                        return d;
                    }
                case IfExpr i:
                    {
                        int d = D(dst);
                        var elseP = new List<int>();
                        CondJump(i.Test, false, elseP);
                        Expr(i.Body, d);
                        int j = Jmp();
                        PatchHere(elseP);
                        Expr(i.Else, d);
                        Patch(j, Pc);
                        return d;
                    }
                case CallExpr c: return Call(c, dst);
                case AttrExpr a:
                    {
                        int o = Expr(a.Obj);
                        int d = D(dst);
                        E(Op.GETATTR, d, o, NameIdx(a.Name));
                        return d;
                    }
                case IndexExpr ix:
                    {
                        int o = Expr(ix.Obj);
                        int k = Index(ix.Index);
                        int d = D(dst);
                        E(Op.GETITEM, d, o, k);
                        return d;
                    }
                case SliceExpr s: return Slice(s, dst);
                case ListExpr l: return Sequence(l.Items, Op.BUILDLIST, dst);
                case TupleExpr t: return Sequence(t.Items, Op.BUILDTUPLE, dst);
                case SetExpr s: return Sequence(s.Items, Op.BUILDSET, dst);
                case DictExpr d: return Dict(d, dst);
                case CompExpr c:
                    return c.Kind == 'g' ? GenExp(c, dst) : Comp(c, dst);
                case LambdaExpr l:
                    {
                        int d = D(dst);
                        MakeFunction(l.Scope, l.Args, d);
                        return d;
                    }
                case YieldExpr y:
                    {
                        _hasYield = true;
                        if (y.From) return YieldFrom(y, dst);
                        int v = y.Value != null ? Expr(y.Value) : LoadConst(Value.None);
                        int d = D(dst);
                        E(Op.YIELD, d, v);
                        return d;
                    }
                case NamedExpr ne:
                    {
                        int v = Expr(ne.Value);
                        StoreName(ne.Target.Id, v);
                        if (dst >= 0 && dst != v) { E(Op.MOVE, dst, v); return dst; }
                        return v;
                    }
                case FStringExpr f: return FString(f, dst);
                case FormattedValue fv:
                    {
                        int v = Expr(fv.Value);
                        int sp = fv.Spec != null ? Expr(fv.Spec) : -1;
                        int d = D(dst);
                        E(Op.FORMAT, d, v, ConvCode(fv.Conv), sp);
                        return d;
                    }
                case StarredExpr _: throw Error("can't use starred expression here", e);
                default: throw Error("unsupported expression", e);
            }
        }

        static int ConvCode(char c) => c == 'r' ? 1 : c == 's' ? 2 : c == 'a' ? 3 : 0;

        int LoadConst(Value v)
        {
            int t = Temp();
            E(Op.LOADK, t, K(v));
            return t;
        }

        int FString(FStringExpr f, int dst)
        {
            if (f.Parts.Count == 1 && f.Parts[0] is FormattedValue)
                return Expr(f.Parts[0], dst);
            int b = Temps(f.Parts.Count);
            for (int i = 0; i < f.Parts.Count; i++) Expr(f.Parts[i], b + i);
            int d = D(dst);
            E(Op.BUILDSTR, d, b, f.Parts.Count);
            return d;
        }

        int Sequence(List<Expr> items, Op build, int dst)
        {
            bool star = items.Exists(x => x is StarredExpr);
            if (!star && items.Count <= 256)
            {
                int b = Temps(items.Count);
                for (int i = 0; i < items.Count; i++) Expr(items[i], b + i);
                int d = D(dst);
                E(build, d, b, items.Count);
                return d;
            }
            int t = Temp();
            bool isSet = build == Op.BUILDSET;
            E(isSet ? Op.BUILDSET : Op.BUILDLIST, t, 0, 0);
            int mark = _nextReg;
            foreach (var x in items)
            {
                if (x is StarredExpr s) E(isSet ? Op.SETUPDATE : Op.LISTEXTEND, t, Expr(s.Value));
                else E(isSet ? Op.SETADD : Op.LISTAPPEND, t, Expr(x));
                _nextReg = mark;
            }
            int dd = D(dst);
            if (build == Op.BUILDTUPLE) E(Op.LISTTOTUPLE, dd, t);
            else if (dd != t) E(Op.MOVE, dd, t);
            return dd;
        }

        int Dict(DictExpr dx, int dst)
        {
            if (!dx.Keys.Contains(null) && dx.Keys.Count <= 128)
            {
                int b = Temps(dx.Keys.Count * 2);
                for (int i = 0; i < dx.Keys.Count; i++)
                {
                    Expr(dx.Keys[i], b + 2 * i);
                    Expr(dx.Values[i], b + 2 * i + 1);
                }
                int d = D(dst);
                E(Op.BUILDDICT, d, b, dx.Keys.Count);
                return d;
            }
            int t = Temp();
            E(Op.BUILDDICT, t, 0, 0);
            int mark = _nextReg;
            for (int i = 0; i < dx.Keys.Count; i++)
            {
                if (dx.Keys[i] == null) E(Op.DICTUPDATE, t, Expr(dx.Values[i]));
                else
                {
                    int k = Expr(dx.Keys[i]);
                    int v = Expr(dx.Values[i]);
                    E(Op.DICTSET, t, k, v);
                }
                _nextReg = mark;
            }
            if (dst >= 0 && dst != t) { E(Op.MOVE, dst, t); return dst; }
            return t;
        }

        int Call(CallExpr c, int dst)
        {
            // zero-argument super()
            if (c.Func is NameExpr sn && sn.Id == "super" && c.Args.Count == 0 && c.Keywords.Count == 0
                && Lookup("super").Kind == SymKind.Global)
            {
                var host = _cur.Host;
                if (host.Kind == ScopeKind.Function && host.ParamNames.Count > 0)
                {
                    var save = _cur;
                    int self = LoadName(host.ParamNames[0], -1);
                    _cur = save;
                    int d0 = D(dst);
                    E(Op.SUPER, d0, self);
                    return d0;
                }
            }

            bool hasStar = c.Args.Exists(a => a is StarredExpr) || c.Keywords.Exists(k => k.Name == null);
            if (hasStar)
            {
                int f = Expr(c.Func);
                int lst = Temp();
                E(Op.BUILDLIST, lst, 0, 0);
                foreach (var a in c.Args)
                {
                    int mark = _nextReg;
                    if (a is StarredExpr s) E(Op.LISTEXTEND, lst, Expr(s.Value));
                    else E(Op.LISTAPPEND, lst, Expr(a));
                    _nextReg = mark;
                }
                int kw = -1;
                if (c.Keywords.Count > 0)
                {
                    kw = Temp();
                    E(Op.BUILDDICT, kw, 0, 0);
                    foreach (var k in c.Keywords)
                    {
                        int mark = _nextReg;
                        if (k.Name == null) E(Op.DICTUPDATE, kw, Expr(k.Value));
                        else
                        {
                            int kr = LoadConst(Value.Str(k.Name));
                            E(Op.DICTSET, kw, kr, Expr(k.Value));
                        }
                        _nextReg = mark;
                    }
                }
                int d = D(dst);
                E(Op.CALLEX, d, f, lst, kw);
                return d;
            }

            int nargs = c.Args.Count + c.Keywords.Count;
            int kwk = -1;
            if (c.Keywords.Count > 0)
            {
                var names = new string[c.Keywords.Count];
                for (int i = 0; i < names.Length; i++) names[i] = c.Keywords[i].Name;
                for (int i = 0; i < names.Length; i++)
                    for (int j = 0; j < i; j++)
                        if (names[i] == names[j]) throw Error("keyword argument repeated: " + names[i], c);
                _consts.Add(Value.Obj(names));
                kwk = _consts.Count - 1;
            }

            if (c.Func is AttrExpr at)
            {
                int b = Temps(nargs + 1);
                Expr(at.Obj, b);
                for (int i = 0; i < c.Args.Count; i++) Expr(c.Args[i], b + 1 + i);
                for (int i = 0; i < c.Keywords.Count; i++) Expr(c.Keywords[i].Value, b + 1 + c.Args.Count + i);
                int d = D(dst);
                _line = c.Line;
                if (kwk < 0) E(Op.CALLM, d, b, NameIdx(at.Name), nargs);
                else E(Op.CALLMKW, d, b, NameIdx(at.Name), nargs, kwk);
                return d;
            }
            else
            {
                int f = Expr(c.Func);
                int b = Temps(nargs);
                for (int i = 0; i < c.Args.Count; i++) Expr(c.Args[i], b + i);
                for (int i = 0; i < c.Keywords.Count; i++) Expr(c.Keywords[i].Value, b + c.Args.Count + i);
                int d = D(dst);
                _line = c.Line;
                if (kwk < 0) E(Op.CALL, d, f, b, nargs);
                else E(Op.CALLKW, d, f, b, nargs, kwk);
                return d;
            }
        }

        int Comp(CompExpr c, int dst)
        {
            int d = Temp();
            E(c.Kind == 'l' ? Op.BUILDLIST : c.Kind == 's' ? Op.BUILDSET : Op.BUILDDICT, d, 0, 0);
            int first = Expr(c.Gens[0].Iter);
            var save = _cur;
            var saveAssigned = new HashSet<Sym>(_assigned);
            _cur = c.Scope;
            foreach (var name in c.Scope.Bound.Items)
            {
                var s = c.Scope.Syms[name];
                if (s.Kind == SymKind.Cell && s.Owner == c.Scope) E(Op.MKCELL, s.Reg);
            }
            CompLoop(c, 0, first, cv =>
            {
                if (c.Kind == 'l') E(Op.LISTAPPEND, d, Expr(c.Elt));
                else if (c.Kind == 's') E(Op.SETADD, d, Expr(c.Elt));
                else
                {
                    int k = Expr(c.Elt);
                    int v = Expr(c.Value);
                    E(Op.DICTSET, d, k, v);
                }
            });
            _cur = save;
            _assigned = saveAssigned;
            if (dst >= 0 && dst != d) { E(Op.MOVE, dst, d); return dst; }
            return d;
        }

        void CompLoop(CompExpr c, int i, int iterSrc, Action<int> body)
        {
            var g = c.Gens[i];
            int it = Temp();
            int src = i == 0 ? iterSrc : Expr(g.Iter);
            E(Op.GETITER, it, src);
            int direct = DirectTarget(g.Target);
            int tmp = direct >= 0 ? direct : Temp();
            int top = Pc;
            E(Op.FORITER, it, tmp, -1);
            int exitPos = Pc - 1;
            ForTargetStore(g.Target, direct, tmp);
            var cont = new List<int>();
            foreach (var cond in g.Ifs) CondJump(cond, false, cont);
            int mark = _nextReg;
            if (i + 1 < c.Gens.Count) CompLoop(c, i + 1, -1, body);
            else body(0);
            _nextReg = mark;
            foreach (var p in cont) Patch(p, top);
            E(Op.JMP, top);
            Patch(exitPos, Pc);
        }

        int GenExp(CompExpr c, int dst)
        {
            int first = Expr(c.Gens[0].Iter);
            int it = Temp();
            E(Op.GETITER, it, first);
            int f = Temp();
            int child = Child(c.Scope);
            E(Op.MAKEFUNC, f, child, 0, 0, 0, 0);
            int d = D(dst);
            E(Op.CALL, d, f, it, 1);
            return d;
        }

        void GenExpBody(CompExpr c)
        {
            _hasYield = true;
            // parameter ".0" (register 0) already holds an iterator
            CompLoop(c, 0, 0, _ =>
            {
                int v = Expr(c.Elt);
                int t = Temp();
                E(Op.YIELD, t, v);
            });
        }

        int YieldFrom(YieldExpr y, int dst)
        {
            int it = Temp();
            int src = Expr(y.Value);
            E(Op.GETITER, it, src);
            int v = Temp();
            int top = Pc;
            E(Op.FORITER, it, v, -1);
            int exitPos = Pc - 1;
            int t = Temp();
            E(Op.YIELD, t, v);
            E(Op.JMP, top);
            Patch(exitPos, Pc);
            int d = D(dst);
            E(Op.LOADK, d, K(Value.None));
            return d;
        }
    }
}
