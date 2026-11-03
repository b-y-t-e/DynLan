using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace DynLan2
{
    /// <summary>Recursive-descent parser for the supported Python subset.</summary>
    internal sealed class Parser
    {
        readonly Token[] _t;
        readonly string _file;
        int _p;

        static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "False","None","True","and","as","assert","async","await","break","class","continue","def","del",
            "elif","else","except","finally","for","from","global","if","import","in","is","lambda","nonlocal",
            "not","or","pass","raise","return","try","while","with","yield"
        };

        static readonly HashSet<string> AugOps = new HashSet<string>
        { "+=","-=","*=","/=","//=","%=","**=",">>=","<<=","&=","|=","^=","@=" };

        public Parser(List<Token> tokens, string file) { _t = tokens.ToArray(); _file = file; }

        public static ModuleNode ParseModule(string src, string file)
        {
            var p = new Parser(Lexer.Tokenize(src, file), file);
            return p.Module();
        }

        public static Expr ParseExpression(string src, string file)
        {
            var p = new Parser(Lexer.Tokenize(src, file), file);
            while (p.Is(TK.Newline)) p._p++;
            var e = p.TestList();
            while (p.Is(TK.Newline)) p._p++;
            if (!p.Is(TK.EOF)) throw p.Error("invalid syntax");
            return e;
        }

        // ------------------------------------------------------------ token helpers
        ref Token Cur => ref _t[_p];
        bool Is(TK k) => _t[_p].Kind == k;
        bool IsOp(string op) { ref var t = ref _t[_p]; return t.Kind == TK.Op && (object)t.S == op || (t.Kind == TK.Op && t.S == op); }
        bool IsKw(string kw) { ref var t = ref _t[_p]; return t.Kw && t.S == kw; }
        bool PeekOp(int off, string op) { ref var t = ref _t[Math.Min(_p + off, _t.Length - 1)]; return t.Kind == TK.Op && t.S == op; }

        ScriptSyntaxException Error(string msg) => new ScriptSyntaxException(msg, _file, Cur.Line, Cur.Col);

        ref Token Next() => ref _t[_p++];

        bool AcceptOp(string op) { if (IsOp(op)) { _p++; return true; } return false; }
        bool AcceptKw(string kw) { if (IsKw(kw)) { _p++; return true; } return false; }

        void ExpectOp(string op)
        {
            if (!AcceptOp(op)) throw Error("expected '" + op + "'");
        }

        void ExpectKw(string kw)
        {
            if (!AcceptKw(kw)) throw Error("expected '" + kw + "'");
        }

        string ExpectName()
        {
            if (!Is(TK.Name) || Cur.Kw) throw Error("expected name");
            return Next().S;
        }

        T At<T>(T n, int line) where T : Node { n.Line = line; return n; }

        // ------------------------------------------------------------ statements
        ModuleNode Module()
        {
            var body = new List<Stmt>();
            while (!Is(TK.EOF))
            {
                if (Is(TK.Newline)) { _p++; continue; }
                if (Is(TK.Indent)) throw Error("unexpected indent");
                Statement(body);
            }
            return new ModuleNode { Body = body, Line = 1 };
        }

        void Statement(List<Stmt> into)
        {
            if (Is(TK.Name))
            {
                switch (Cur.S)
                {
                    case "if": into.Add(If()); return;
                    case "while": into.Add(While()); return;
                    case "for": into.Add(For()); return;
                    case "try": into.Add(Try()); return;
                    case "def": into.Add(FuncDef(new List<Expr>())); return;
                    case "class": into.Add(ClassDef(new List<Expr>())); return;
                    case "with": into.Add(With()); return;
                    case "async": throw Error("async is not supported");
                }
            }
            if (IsOp("@")) { into.Add(Decorated()); return; }
            SimpleStatements(into);
        }

        void SimpleStatements(List<Stmt> into)
        {
            while (true)
            {
                into.Add(SmallStatement());
                if (AcceptOp(";"))
                {
                    if (Is(TK.Newline)) break;
                    continue;
                }
                break;
            }
            if (!Is(TK.Newline) && !Is(TK.EOF)) throw Error("invalid syntax");
            if (Is(TK.Newline)) _p++;
        }

        List<Stmt> Block()
        {
            ExpectOp(":");
            var body = new List<Stmt>();
            if (!Is(TK.Newline))
            {
                SimpleStatements(body);
                return body;
            }
            _p++;
            while (Is(TK.Newline)) _p++;
            if (!Is(TK.Indent)) throw Error("expected an indented block");
            _p++;
            while (!Is(TK.Dedent) && !Is(TK.EOF))
            {
                if (Is(TK.Newline)) { _p++; continue; }
                Statement(body);
            }
            if (Is(TK.Dedent)) _p++;
            return body;
        }

        Stmt SmallStatement()
        {
            int line = Cur.Line;
            if (Is(TK.Name))
            {
                switch (Cur.S)
                {
                    case "pass": _p++; return At(new PassStmt(), line);
                    case "break": _p++; return At(new BreakStmt(), line);
                    case "continue": _p++; return At(new ContinueStmt(), line);
                    case "return":
                        _p++;
                        return At(new ReturnStmt { Value = AtStmtEnd() ? null : TestListStar() }, line);
                    case "raise":
                        {
                            _p++;
                            var r = new RaiseStmt();
                            if (!AtStmtEnd())
                            {
                                r.Exc = Test();
                                if (AcceptKw("from")) r.Cause = Test();
                            }
                            return At(r, line);
                        }
                    case "global":
                        {
                            _p++;
                            var names = new List<string> { ExpectName() };
                            while (AcceptOp(",")) names.Add(ExpectName());
                            return At(new GlobalStmt { Names = names }, line);
                        }
                    case "nonlocal":
                        {
                            _p++;
                            var names = new List<string> { ExpectName() };
                            while (AcceptOp(",")) names.Add(ExpectName());
                            return At(new NonlocalStmt { Names = names }, line);
                        }
                    case "del":
                        {
                            _p++;
                            var targets = new List<Expr> { Expr() };
                            while (AcceptOp(",") && !AtStmtEnd()) targets.Add(Expr());
                            return At(new DelStmt { Targets = targets }, line);
                        }
                    case "assert":
                        {
                            _p++;
                            var a = new AssertStmt { Test = Test() };
                            if (AcceptOp(",")) a.Msg = Test();
                            return At(a, line);
                        }
                    case "import":
                        {
                            _p++;
                            var names = new List<(string, string)>();
                            do
                            {
                                string m = DottedName();
                                string asName = AcceptKw("as") ? ExpectName() : null;
                                names.Add((m, asName));
                            } while (AcceptOp(","));
                            return At(new ImportStmt { Names = names }, line);
                        }
                    case "from":
                        {
                            _p++;
                            string m = DottedName();
                            ExpectKw("import");
                            var names = new List<(string, string)>();
                            bool paren = AcceptOp("(");
                            if (AcceptOp("*")) throw Error("'from module import *' is not supported");
                            do
                            {
                                if (paren && IsOp(")")) break;
                                string n = ExpectName();
                                string asName = AcceptKw("as") ? ExpectName() : null;
                                names.Add((n, asName));
                            } while (AcceptOp(","));
                            if (paren) ExpectOp(")");
                            return At(new FromImportStmt { Module = m, Names = names }, line);
                        }
                }
            }
            return ExprStatement();
        }

        string DottedName()
        {
            var sb = new StringBuilder(ExpectName());
            while (AcceptOp(".")) sb.Append('.').Append(ExpectName());
            return sb.ToString();
        }

        bool AtStmtEnd() => Is(TK.Newline) || Is(TK.EOF) || IsOp(";");

        Stmt ExprStatement()
        {
            int line = Cur.Line;
            Expr first = IsKw("yield") ? YieldExpr() : TestListStar();

            if (IsOp(":") && !PeekOp(0, ":="))
            {
                _p++;
                var ann = Test();
                Expr val = null;
                if (AcceptOp("=")) val = IsKw("yield") ? YieldExpr() : TestListStar();
                CheckTarget(first);
                return At(new AnnAssignStmt { Target = first, Annotation = ann, Value = val }, line);
            }
            if (Cur.Kind == TK.Op && AugOps.Contains(Cur.S))
            {
                string op = Next().S;
                op = op.Substring(0, op.Length - 1);
                var val = IsKw("yield") ? YieldExpr() : TestList();
                if (!(first is NameExpr || first is AttrExpr || first is IndexExpr))
                    throw Error("illegal expression for augmented assignment");
                return At(new AugAssignStmt { Target = first, Op = op, Value = val }, line);
            }
            if (IsOp("="))
            {
                var targets = new List<Expr> { first };
                Expr value = null;
                while (AcceptOp("="))
                {
                    value = IsKw("yield") ? YieldExpr() : TestListStar();
                    if (IsOp("=")) targets.Add(value);
                }
                foreach (var t in targets) CheckTarget(t);
                return At(new AssignStmt { Targets = targets, Value = value }, line);
            }
            return At(new ExprStmt { X = first }, line);
        }

        void CheckTarget(Expr e)
        {
            switch (e)
            {
                case NameExpr _: case AttrExpr _: case IndexExpr _: return;
                case TupleExpr t: foreach (var x in t.Items) CheckTarget(x); return;
                case ListExpr l: foreach (var x in l.Items) CheckTarget(x); return;
                case StarredExpr s: CheckTarget(s.Value); return;
                default: throw new ScriptSyntaxException("cannot assign to expression", _file, e.Line, 0);
            }
        }

        Stmt If()
        {
            int line = Cur.Line;
            _p++;
            var s = new IfStmt { Test = NamedExprTest() };
            s.Body = Block();
            if (IsKw("elif"))
            {
                s.Else = new List<Stmt> { If() };
            }
            else if (AcceptKw("else"))
            {
                s.Else = Block();
            }
            return At(s, line);
        }

        Stmt While()
        {
            int line = Cur.Line;
            _p++;
            var s = new WhileStmt { Test = NamedExprTest() };
            s.Body = Block();
            if (AcceptKw("else")) s.Else = Block();
            return At(s, line);
        }

        Stmt For()
        {
            int line = Cur.Line;
            _p++;
            var target = ExprList();
            ExpectKw("in");
            var iter = TestList();
            CheckTarget(target);
            var s = new ForStmt { Target = target, Iter = iter };
            s.Body = Block();
            if (AcceptKw("else")) s.Else = Block();
            return At(s, line);
        }

        Stmt Try()
        {
            int line = Cur.Line;
            _p++;
            var s = new TryStmt { Body = Block() };
            while (IsKw("except"))
            {
                int hl = Cur.Line;
                _p++;
                var h = new ExceptHandler();
                if (!IsOp(":"))
                {
                    h.Type = Test();
                    if (AcceptKw("as")) h.Name = ExpectName();
                    else if (AcceptOp(",")) throw Error("use 'except X as name'");
                }
                h.Body = Block();
                s.Handlers.Add(At(h, hl));
            }
            if (AcceptKw("else"))
            {
                if (s.Handlers.Count == 0) throw Error("else without except");
                s.Else = Block();
            }
            if (AcceptKw("finally")) s.Finally = Block();
            if (s.Handlers.Count == 0 && s.Finally == null) throw Error("expected 'except' or 'finally' block");
            return At(s, line);
        }

        Stmt With()
        {
            int line = Cur.Line;
            _p++;
            var items = new List<WithItem>();
            do
            {
                var item = new WithItem { Context = Test() };
                if (AcceptKw("as")) { item.Target = Expr(); CheckTarget(item.Target); }
                items.Add(item);
            } while (AcceptOp(","));
            return At(new WithStmt { Items = items, Body = Block() }, line);
        }

        Stmt Decorated()
        {
            var decorators = new List<Expr>();
            while (AcceptOp("@"))
            {
                decorators.Add(NamedExprTest());
                if (!Is(TK.Newline)) throw Error("expected newline after decorator");
                _p++;
            }
            if (IsKw("def")) return FuncDef(decorators);
            if (IsKw("class")) return ClassDef(decorators);
            throw Error("expected 'def' or 'class' after decorator");
        }

        Stmt FuncDef(List<Expr> decorators)
        {
            int line = Cur.Line;
            _p++;
            var f = new FuncDef { Name = ExpectName(), Decorators = decorators };
            ExpectOp("(");
            f.Args = ParamList(")", true);
            ExpectOp(")");
            if (AcceptOp("->")) f.Returns = Test();
            f.Body = Block();
            return At(f, line);
        }

        Params ParamList(string close, bool annotations)
        {
            var ps = new Params();
            bool seenDefault = false;
            bool kwOnly = false;
            while (!IsOp(close))
            {
                if (AcceptOp("**"))
                {
                    ps.KwArg = OneParam(annotations, false);
                    AcceptOp(",");
                    break;
                }
                if (AcceptOp("*"))
                {
                    kwOnly = true;
                    if (!IsOp(",") && !IsOp(close)) ps.VarArg = OneParam(annotations, false);
                }
                else if (AcceptOp("/"))
                {
                    // positional-only marker: accepted and ignored
                }
                else
                {
                    var p = OneParam(annotations, true);
                    if (kwOnly) ps.KwOnly.Add(p);
                    else
                    {
                        if (p.Default != null) seenDefault = true;
                        else if (seenDefault) throw Error("non-default argument follows default argument");
                        ps.Positional.Add(p);
                    }
                }
                if (!AcceptOp(",")) break;
            }
            return ps;
        }

        Param OneParam(bool annotations, bool allowDefault)
        {
            var p = new Param { Name = ExpectName() };
            if (annotations && AcceptOp(":")) p.Annotation = Test();
            if (allowDefault && AcceptOp("=")) p.Default = Test();
            return p;
        }

        Stmt ClassDef(List<Expr> decorators)
        {
            int line = Cur.Line;
            _p++;
            var c = new ClassDef { Name = ExpectName(), Decorators = decorators, Bases = new List<Expr>(), Keywords = new List<Keyword>() };
            if (AcceptOp("("))
            {
                var (args, kws) = ArgList();
                c.Bases = args;
                c.Keywords = kws;
                ExpectOp(")");
            }
            c.Body = Block();
            return At(c, line);
        }

        // ------------------------------------------------------------ expressions
        Expr TestList()
        {
            int line = Cur.Line;
            var first = Test();
            if (!IsOp(",")) return first;
            var items = new List<Expr> { first };
            while (AcceptOp(","))
            {
                if (EndOfList()) break;
                items.Add(Test());
            }
            return At(new TupleExpr { Items = items }, line);
        }

        Expr TestListStar()
        {
            int line = Cur.Line;
            var first = StarOrTest();
            if (!IsOp(","))
            {
                if (first is StarredExpr) throw Error("can't use starred expression here");
                return first;
            }
            var items = new List<Expr> { first };
            while (AcceptOp(","))
            {
                if (EndOfList()) break;
                items.Add(StarOrTest());
            }
            return At(new TupleExpr { Items = items }, line);
        }

        bool EndOfList() =>
            Is(TK.Newline) || Is(TK.EOF) || IsOp("=") || IsOp(")") || IsOp("]") || IsOp("}") || IsOp(":") || IsOp(";")
            || (Cur.Kind == TK.Op && AugOps.Contains(Cur.S)) || IsKw("in");

        Expr StarOrTest()
        {
            if (IsOp("*"))
            {
                int line = Cur.Line;
                _p++;
                return At(new StarredExpr { Value = Expr() }, line);
            }
            return NamedExprTest();
        }

        Expr ExprList()
        {
            int line = Cur.Line;
            var first = StarOrExpr();
            if (!IsOp(",")) return first;
            var items = new List<Expr> { first };
            while (AcceptOp(","))
            {
                if (IsKw("in") || IsOp("=") || Is(TK.Newline)) break;
                items.Add(StarOrExpr());
            }
            return At(new TupleExpr { Items = items }, line);
        }

        Expr StarOrExpr()
        {
            if (IsOp("*"))
            {
                int line = Cur.Line;
                _p++;
                return At(new StarredExpr { Value = Expr() }, line);
            }
            return Expr();
        }

        Expr NamedExprTest()
        {
            int line = Cur.Line;
            var e = Test();
            if (IsOp(":="))
            {
                if (!(e is NameExpr n)) throw Error("cannot use assignment expressions with this target");
                _p++;
                return At(new NamedExpr { Target = n, Value = Test() }, line);
            }
            return e;
        }

        Expr Test()
        {
            if (IsKw("lambda")) return Lambda(false);
            int line = Cur.Line;
            var e = OrTest();
            if (IsKw("if"))
            {
                // ternary; but not inside comprehension 'if' (handled by caller using OrTest)
                int save = _p;
                _p++;
                var cond = OrTest();
                if (AcceptKw("else"))
                {
                    var other = Test();
                    return At(new IfExpr { Test = cond, Body = e, Else = other }, line);
                }
                _p = save;
            }
            return e;
        }

        Expr TestNoCond()
        {
            if (IsKw("lambda")) return Lambda(true);
            return OrTest();
        }

        Expr Lambda(bool noCond)
        {
            int line = Cur.Line;
            _p++;
            var args = ParamList(":", false);
            ExpectOp(":");
            var body = noCond ? TestNoCond() : Test();
            return At(new LambdaExpr { Args = args, Body = body }, line);
        }

        Expr OrTest()
        {
            int line = Cur.Line;
            var e = AndTest();
            if (!IsKw("or")) return e;
            var vals = new List<Expr> { e };
            while (AcceptKw("or")) vals.Add(AndTest());
            return At(new BoolExpr { IsAnd = false, Values = vals }, line);
        }

        Expr AndTest()
        {
            int line = Cur.Line;
            var e = NotTest();
            if (!IsKw("and")) return e;
            var vals = new List<Expr> { e };
            while (AcceptKw("and")) vals.Add(NotTest());
            return At(new BoolExpr { IsAnd = true, Values = vals }, line);
        }

        Expr NotTest()
        {
            if (IsKw("not"))
            {
                int line = Cur.Line;
                _p++;
                return At(new UnaryExpr { Op = "not", X = NotTest() }, line);
            }
            return Comparison();
        }

        Expr Comparison()
        {
            int line = Cur.Line;
            var left = Expr();
            List<string> ops = null;
            List<Expr> rights = null;
            while (true)
            {
                string op = null;
                if (Cur.Kind == TK.Op)
                {
                    switch (Cur.S)
                    {
                        case "<": case ">": case "==": case "!=": case "<=": case ">=": op = Cur.S; _p++; break;
                    }
                }
                else if (IsKw("in")) { op = "in"; _p++; }
                else if (IsKw("not") && _t[_p + 1].Kind == TK.Name && _t[_p + 1].S == "in") { op = "not in"; _p += 2; }
                else if (IsKw("is"))
                {
                    _p++;
                    op = AcceptKw("not") ? "is not" : "is";
                }
                if (op == null) break;
                (ops ??= new List<string>()).Add(op);
                (rights ??= new List<Expr>()).Add(Expr());
            }
            if (ops == null) return left;
            return At(new CompareExpr { Left = left, Ops = ops, Rights = rights }, line);
        }

        Expr Expr() => BinOps(0);

        /// <summary>Precedence climbing over | ^ &amp; shifts + - * / // % @ (all left-associative).</summary>
        Expr BinOps(int minLevel)
        {
            int line = Cur.Line;
            var left = Factor();
            while (true)
            {
                ref var t = ref _t[_p];
                if (t.Kind != TK.Op || t.Bin == 0 || t.Bin - 1 < minLevel) return left;
                int level = t.Bin - 1;
                string op = t.S;
                _p++;
                var right = BinOps(level + 1);
                left = At(new BinExpr { Op = op, L = left, R = right }, line);
            }
        }

        Expr Factor()
        {
            if (Cur.Kind == TK.Op && (Cur.S == "-" || Cur.S == "+" || Cur.S == "~"))
            {
                int line = Cur.Line;
                string op = Next().S;
                var x = Factor();
                if (op == "-" && x is ConstExpr c)
                {
                    if (c.Value.IsInt && c.Value.AsInt != long.MinValue) return At(new ConstExpr { Value = Value.Int(-c.Value.AsInt) }, line);
                    if (c.Value.IsFloat) return At(new ConstExpr { Value = Value.Float(-c.Value.AsFloat) }, line);
                    if (c.Value.IsBigInt) return At(new ConstExpr { Value = Value.BigInt(-c.Value.AsBig) }, line);
                }
                return At(new UnaryExpr { Op = op, X = x }, line);
            }
            return Power();
        }

        Expr Power()
        {
            int line = Cur.Line;
            if (IsKw("await")) throw Error("await is not supported");
            var e = AtomExpr();
            if (AcceptOp("**"))
            {
                var r = Factor();
                return At(new BinExpr { Op = "**", L = e, R = r }, line);
            }
            return e;
        }

        Expr AtomExpr()
        {
            var e = Atom();
            while (true)
            {
                int line = Cur.Line;
                if (AcceptOp("("))
                {
                    var (args, kws) = ArgList();
                    ExpectOp(")");
                    e = At(new CallExpr { Func = e, Args = args, Keywords = kws }, line);
                }
                else if (AcceptOp("["))
                {
                    var idx = SubscriptList();
                    ExpectOp("]");
                    e = At(new IndexExpr { Obj = e, Index = idx }, line);
                }
                else if (AcceptOp("."))
                {
                    var name = Cur.Kind == TK.Name ? Next().S : throw Error("expected attribute name");
                    e = At(new AttrExpr { Obj = e, Name = name }, line);
                }
                else return e;
            }
        }

        (List<Expr>, List<Keyword>) ArgList()
        {
            var args = new List<Expr>();
            var kws = new List<Keyword>();
            while (!IsOp(")"))
            {
                int line = Cur.Line;
                if (AcceptOp("**"))
                {
                    kws.Add(new Keyword { Name = null, Value = Test() });
                }
                else if (AcceptOp("*"))
                {
                    if (kws.Count > 0 && kws.Exists(k => k.Name == null)) throw Error("iterable argument unpacking follows keyword argument unpacking");
                    args.Add(At(new StarredExpr { Value = Test() }, line));
                }
                else if (Is(TK.Name) && PeekOp(1, "=") && !Cur.Kw)
                {
                    string name = Next().S;
                    _p++;
                    kws.Add(new Keyword { Name = name, Value = Test() });
                }
                else
                {
                    var e = NamedExprTest();
                    if (IsKw("for"))
                    {
                        e = At(new CompExpr { Kind = 'g', Elt = e, Gens = CompFor() }, line);
                    }
                    if (kws.Count > 0 && !(e is StarredExpr)) throw Error("positional argument follows keyword argument");
                    args.Add(e);
                }
                if (!AcceptOp(",")) break;
            }
            return (args, kws);
        }

        Expr SubscriptList()
        {
            int line = Cur.Line;
            var first = Subscript();
            if (!IsOp(",")) return first;
            var items = new List<Expr> { first };
            while (AcceptOp(","))
            {
                if (IsOp("]")) break;
                items.Add(Subscript());
            }
            return At(new TupleExpr { Items = items }, line);
        }

        Expr Subscript()
        {
            int line = Cur.Line;
            Expr lo = null, hi = null, step = null;
            if (!IsOp(":"))
            {
                lo = NamedExprTest();
                if (!IsOp(":")) return lo;
            }
            ExpectOp(":");
            if (!IsOp("]") && !IsOp(":") && !IsOp(",")) hi = Test();
            if (AcceptOp(":"))
            {
                if (!IsOp("]") && !IsOp(",")) step = Test();
            }
            return At(new SliceExpr { Lo = lo, Hi = hi, Step = step }, line);
        }

        List<Comprehension> CompFor()
        {
            var gens = new List<Comprehension>();
            while (IsKw("for"))
            {
                _p++;
                var c = new Comprehension { Target = ExprList() };
                CheckTarget(c.Target);
                ExpectKw("in");
                c.Iter = OrTest();
                while (IsKw("if"))
                {
                    _p++;
                    c.Ifs.Add(TestNoCond());
                }
                gens.Add(c);
                if (IsKw("async")) throw Error("async comprehensions are not supported");
            }
            return gens;
        }

        Expr YieldExpr()
        {
            int line = Cur.Line;
            _p++;
            if (AcceptKw("from")) return At(new YieldExpr { Value = Test(), From = true }, line);
            if (Is(TK.Newline) || IsOp(")") || IsOp("]") || IsOp("}") || IsOp("=") || IsOp(";") || Is(TK.EOF))
                return At(new YieldExpr(), line);
            return At(new YieldExpr { Value = TestListStar() }, line);
        }

        Expr Atom()
        {
            var t = Cur;
            int line = t.Line;
            switch (t.Kind)
            {
                case TK.Int:
                    _p++;
                    return At(new ConstExpr { Value = t.V is long l ? Value.Int(l) : Value.BigInt((BigInteger)t.V) }, line);
                case TK.Float:
                    _p++;
                    return At(new ConstExpr { Value = Value.Float((double)t.V) }, line);
                case TK.Str:
                case TK.FStr:
                    return Strings();
                case TK.Name:
                    switch (t.S)
                    {
                        case "None": _p++; return At(new ConstExpr { Value = Value.None }, line);
                        case "True": _p++; return At(new ConstExpr { Value = Value.True }, line);
                        case "False": _p++; return At(new ConstExpr { Value = Value.False }, line);
                        case "yield": throw Error("'yield' must be parenthesized here");
                        case "lambda": return Lambda(false);
                    }
                    if (t.Kw) throw Error("invalid syntax near '" + t.S + "'");
                    _p++;
                    return At(new NameExpr { Id = t.S }, line);
                case TK.Op:
                    switch (t.S)
                    {
                        case "(":
                            {
                                _p++;
                                if (AcceptOp(")")) return At(new TupleExpr { Items = new List<Expr>() }, line);
                                if (IsKw("yield")) { var y = YieldExpr(); ExpectOp(")"); return y; }
                                var first = StarOrTest();
                                if (IsKw("for"))
                                {
                                    var g = At(new CompExpr { Kind = 'g', Elt = first, Gens = CompFor() }, line);
                                    ExpectOp(")");
                                    return g;
                                }
                                if (AcceptOp(")"))
                                {
                                    if (first is StarredExpr) throw Error("can't use starred expression here");
                                    return first;
                                }
                                var items = new List<Expr> { first };
                                while (AcceptOp(","))
                                {
                                    if (IsOp(")")) break;
                                    items.Add(StarOrTest());
                                }
                                ExpectOp(")");
                                return At(new TupleExpr { Items = items }, line);
                            }
                        case "[":
                            {
                                _p++;
                                if (AcceptOp("]")) return At(new ListExpr { Items = new List<Expr>() }, line);
                                var first = StarOrTest();
                                if (IsKw("for"))
                                {
                                    var c = At(new CompExpr { Kind = 'l', Elt = first, Gens = CompFor() }, line);
                                    ExpectOp("]");
                                    return c;
                                }
                                var items = new List<Expr> { first };
                                while (AcceptOp(","))
                                {
                                    if (IsOp("]")) break;
                                    items.Add(StarOrTest());
                                }
                                ExpectOp("]");
                                return At(new ListExpr { Items = items }, line);
                            }
                        case "{":
                            return DictOrSet();
                        case "...":
                            _p++;
                            return At(new ConstExpr { Value = Value.None }, line);
                    }
                    break;
            }
            throw Error("invalid syntax");
        }

        Expr DictOrSet()
        {
            int line = Cur.Line;
            _p++;
            if (AcceptOp("}")) return At(new DictExpr { Keys = new List<Expr>(), Values = new List<Expr>() }, line);
            if (IsOp("**"))
            {
                _p++;
                var d = new DictExpr { Keys = new List<Expr> { null }, Values = new List<Expr> { Expr() } };
                DictRest(d);
                return At(d, line);
            }
            var first = StarOrTest();
            if (AcceptOp(":"))
            {
                var v = Test();
                if (IsKw("for"))
                {
                    var c = At(new CompExpr { Kind = 'd', Elt = first, Value = v, Gens = CompFor() }, line);
                    ExpectOp("}");
                    return c;
                }
                var d = new DictExpr { Keys = new List<Expr> { first }, Values = new List<Expr> { v } };
                DictRest(d);
                return At(d, line);
            }
            if (IsKw("for"))
            {
                var c = At(new CompExpr { Kind = 's', Elt = first, Gens = CompFor() }, line);
                ExpectOp("}");
                return c;
            }
            var items = new List<Expr> { first };
            while (AcceptOp(","))
            {
                if (IsOp("}")) break;
                items.Add(StarOrTest());
            }
            ExpectOp("}");
            return At(new SetExpr { Items = items }, line);
        }

        void DictRest(DictExpr d)
        {
            while (AcceptOp(","))
            {
                if (IsOp("}")) break;
                if (AcceptOp("**")) { d.Keys.Add(null); d.Values.Add(Expr()); continue; }
                d.Keys.Add(Test());
                ExpectOp(":");
                d.Values.Add(Test());
            }
            ExpectOp("}");
        }

        // ------------------------------------------------------------ strings / f-strings
        Expr Strings()
        {
            int line = Cur.Line;
            var parts = new List<Expr>();
            var lit = new StringBuilder();
            bool anyF = false;
            while (Is(TK.Str) || Is(TK.FStr))
            {
                var t = Next();
                if (t.Kind == TK.Str) { lit.Append(t.S); continue; }
                anyF = true;
                ParseFString(t.S, t.Raw, t.Line, parts, lit);
            }
            if (!anyF) return At(new ConstExpr { Value = Value.Str(lit.ToString()) }, line);
            if (lit.Length > 0) parts.Add(new ConstExpr { Value = Value.Str(lit.ToString()), Line = line });
            return At(new FStringExpr { Parts = parts }, line);
        }

        void ParseFString(string s, bool raw, int line, List<Expr> parts, StringBuilder lit)
        {
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '{')
                {
                    if (i + 1 < s.Length && s[i + 1] == '{') { lit.Append('{'); i += 2; continue; }
                    if (lit.Length > 0) { parts.Add(new ConstExpr { Value = Value.Str(lit.ToString()), Line = line }); lit.Clear(); }
                    i++;
                    int start = i;
                    int depth = 0;
                    char inStr = '\0';
                    int exprEnd = -1, convPos = -1, specPos = -1;
                    for (; i < s.Length; i++)
                    {
                        char ch = s[i];
                        if (inStr != '\0') { if (ch == inStr) inStr = '\0'; continue; }
                        if (ch == '\'' || ch == '"') { inStr = ch; continue; }
                        if (ch == '(' || ch == '[' || ch == '{') { depth++; continue; }
                        if ((ch == ')' || ch == ']') && depth > 0) { depth--; continue; }
                        if (ch == '}' && depth > 0) { depth--; continue; }
                        if (depth == 0)
                        {
                            if (ch == '!' && convPos < 0 && specPos < 0 && i + 1 < s.Length && s[i + 1] != '=') { if (exprEnd < 0) exprEnd = i; convPos = i + 1; continue; }
                            if (ch == ':' && specPos < 0) { if (exprEnd < 0) exprEnd = i; specPos = i + 1; break; }
                            if (ch == '}') { if (exprEnd < 0) exprEnd = i; break; }
                        }
                    }
                    if (i >= s.Length) throw new ScriptSyntaxException("f-string: expecting '}'", _file, line, 0);
                    string exprText = s.Substring(start, exprEnd - start);
                    char conv = '\0';
                    if (convPos >= 0) conv = s[convPos];
                    Expr spec = null;
                    if (specPos >= 0)
                    {
                        // spec runs until the matching '}' (may contain nested {...})
                        int d2 = 0;
                        int j = specPos;
                        for (; j < s.Length; j++)
                        {
                            if (s[j] == '{') d2++;
                            else if (s[j] == '}') { if (d2 == 0) break; d2--; }
                        }
                        if (j >= s.Length) throw new ScriptSyntaxException("f-string: expecting '}'", _file, line, 0);
                        string specText = s.Substring(specPos, j - specPos);
                        var specParts = new List<Expr>();
                        var specLit = new StringBuilder();
                        ParseFString(specText, true, line, specParts, specLit);
                        if (specLit.Length > 0) specParts.Add(new ConstExpr { Value = Value.Str(specLit.ToString()), Line = line });
                        spec = specParts.Count == 1 && specParts[0] is ConstExpr ? specParts[0] : new FStringExpr { Parts = specParts, Line = line };
                        i = j;
                    }
                    bool debug = false;
                    string trimmed = exprText.TrimEnd();
                    if (trimmed.EndsWith("=") && !trimmed.EndsWith("==") && !trimmed.EndsWith("!=") && !trimmed.EndsWith("<=") && !trimmed.EndsWith(">="))
                    {
                        debug = true;
                        parts.Add(new ConstExpr { Value = Value.Str(exprText), Line = line });
                        exprText = trimmed.Substring(0, trimmed.Length - 1);
                        if (conv == '\0' && spec == null) conv = 'r';
                    }
                    if (exprText.Trim().Length == 0) throw new ScriptSyntaxException("f-string: empty expression not allowed", _file, line, 0);
                    Expr value;
                    try
                    {
                        value = ParseExpression("(" + exprText + ")", _file);
                    }
                    catch (ScriptSyntaxException e)
                    {
                        throw new ScriptSyntaxException("f-string: " + e.RawMessage, _file, line, 0);
                    }
                    SetLines(value, line);
                    _ = debug;
                    parts.Add(new FormattedValue { Value = value, Conv = conv, Spec = spec, Line = line });
                    i++; // skip '}'
                    continue;
                }
                if (c == '}')
                {
                    if (i + 1 < s.Length && s[i + 1] == '}') { lit.Append('}'); i += 2; continue; }
                    throw new ScriptSyntaxException("f-string: single '}' is not allowed", _file, line, 0);
                }
                if (c == '\\' && !raw && i + 1 < s.Length)
                {
                    i++;
                    Lexer.DecodeEscape(s, ref i, lit);
                    continue;
                }
                lit.Append(c);
                i++;
            }
        }

        static void SetLines(Expr e, int line)
        {
            // expressions parsed from an f-string start at line 1; re-anchor them
            new LineFixer(line).Visit(e);
        }

        sealed class LineFixer
        {
            readonly int _line;
            public LineFixer(int line) { _line = line; }
            public void Visit(Expr e)
            {
                if (e == null) return;
                e.Line = _line;
                switch (e)
                {
                    case BinExpr b: Visit(b.L); Visit(b.R); break;
                    case UnaryExpr u: Visit(u.X); break;
                    case CallExpr c: Visit(c.Func); foreach (var a in c.Args) Visit(a); foreach (var k in c.Keywords) Visit(k.Value); break;
                    case AttrExpr a: Visit(a.Obj); break;
                    case IndexExpr i: Visit(i.Obj); Visit(i.Index); break;
                    case CompareExpr c: Visit(c.Left); foreach (var r in c.Rights) Visit(r); break;
                    case BoolExpr b: foreach (var v in b.Values) Visit(v); break;
                    case IfExpr i: Visit(i.Test); Visit(i.Body); Visit(i.Else); break;
                }
            }
        }
    }
}
