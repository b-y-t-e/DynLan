using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace DynLan2
{
    /// <summary>
    /// Bytecode → expression tree → IL.
    ///
    /// Flow-sensitive type inference assigns every register, at every program point, a representation:
    /// boxed <see cref="Value"/>, unboxed <c>long</c> or unboxed <c>double</c>. Integer arithmetic on unboxed
    /// longs is overflow-checked; on overflow the code deoptimizes (spills to the heap frame and lets the
    /// interpreter redo the instruction with arbitrary-precision ints), so Python semantics are preserved.
    /// Variables annotated <c>x: int</c> / <c>x: float</c> are always unboxed (overflow raises OverflowError).
    ///
    /// At safe points (loop back-edges, calls) the code can materialize all registers into the frame and
    /// return; the interpreter then continues — which is what makes suspension and snapshots work.
    /// </summary>
    internal sealed class JitCompiler
    {
        const byte V_ = 0, L_ = 1, D_ = 2, U_ = 3;

        readonly FunctionCode _fc;
        readonly int[] _code;
        readonly Value[] _consts;
        readonly int _n;
        readonly ParameterExpression _vm = Parameter(typeof(VM), "vm");
        readonly ParameterExpression _f = Parameter(typeof(Frame), "f");
        readonly ParameterExpression _arr = Variable(typeof(Value[]), "R");
        readonly ParameterExpression _pc = Variable(typeof(int), "pc");
        readonly ParameterExpression _st = Variable(typeof(int), "st");
        readonly ParameterExpression _tmp = Variable(typeof(Value), "tmp");
        readonly ParameterExpression _tl = Variable(typeof(long), "tl");
        readonly ParameterExpression _tmpArr = Variable(typeof(Value[]), "tmpArr");
        readonly ParameterExpression _argBase = Variable(typeof(int), "argBase");
        readonly ParameterExpression _argArr = Variable(typeof(Value[]), "argArr");
        readonly ParameterExpression _budget = Variable(typeof(int), "budget");
        readonly ParameterExpression[] _rv, _rl, _rd;
        readonly byte[] _forced;
        readonly bool[] _never;
        readonly LabelTarget _ret = Label(typeof(int), "ret");
        readonly LabelTarget _spill = Label("spill");
        readonly Dictionary<int, LabelTarget> _labels = new Dictionary<int, LabelTarget>();
        readonly SortedSet<int> _entries = new SortedSet<int> { 0 };
        readonly SortedSet<int> _leaders = new SortedSet<int> { 0 };
        readonly Dictionary<int, byte[]> _in = new Dictionary<int, byte[]>();
        readonly List<int> _starts = new List<int>();
        byte[] _s; // current state during emission

        static readonly MethodInfo MInt = typeof(Value).GetMethod(nameof(Value.Int));
        static readonly MethodInfo MFloat = typeof(Value).GetMethod(nameof(Value.Float));
        static readonly MethodInfo MBool = typeof(Value).GetMethod(nameof(Value.Bool));
        static readonly FieldInfo FNone = typeof(Value).GetField(nameof(Value.None));

        static MethodInfo J(string name) => typeof(JitOps).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingMethodException("JitOps." + name);

        static MethodInfo OpsM(string name, params Type[] types) =>
            typeof(Ops).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, types, null)
            ?? throw new MissingMethodException("Ops." + name);

        public JitCompiler(FunctionCode fc)
        {
            _fc = fc;
            _code = fc.Code;
            _consts = fc.Consts;
            _n = fc.NumRegs;
            _rv = new ParameterExpression[_n];
            _rl = new ParameterExpression[_n];
            _rd = new ParameterExpression[_n];
            _forced = new byte[_n];
            _never = new bool[_n];
            for (int r = 0; r < _n; r++) _rv[r] = Variable(typeof(Value), "r" + r);
        }

        ParameterExpression RL(int r) => _rl[r] ??= Variable(typeof(long), "l" + r);
        ParameterExpression RD(int r) => _rd[r] ??= Variable(typeof(double), "d" + r);
        LabelTarget Lbl(int pc) => _labels.TryGetValue(pc, out var l) ? l : _labels[pc] = Label("L" + pc);

        // ================================================================ analysis
        static byte Join(byte a, byte b)
        {
            if (a == U_) return b;
            if (b == U_) return a;
            return a == b ? a : V_;
        }

        byte KindOf(int o, byte[] s)
        {
            if (o < 0)
            {
                var v = _consts[~o];
                return v.IsInt ? L_ : v.IsFloat ? D_ : V_;
            }
            var t = s[o];
            return t == U_ ? V_ : t;
        }

        static bool Num(byte k) => k == L_ || k == D_;

        /// <summary>Representation of the single result register of an instruction.</summary>
        byte ResultKind(int pc, byte[] s)
        {
            var c = _code;
            var op = (Op)c[pc];
            switch (op)
            {
                case Op.MOVE: return KindOf(c[pc + 2], s);
                case Op.LOADK: { var v = _consts[c[pc + 2]]; return v.IsInt ? L_ : v.IsFloat ? D_ : V_; }
                case Op.ADD: case Op.SUB: case Op.MUL: case Op.IADD: case Op.FLOORDIV: case Op.MOD:
                    {
                        byte a = KindOf(c[pc + 2], s), b = KindOf(c[pc + 3], s);
                        if (a == L_ && b == L_) return L_;
                        if (Num(a) && Num(b)) return D_;
                        return V_;
                    }
                case Op.TRUEDIV:
                    {
                        byte a = KindOf(c[pc + 2], s), b = KindOf(c[pc + 3], s);
                        return Num(a) && Num(b) ? D_ : V_;
                    }
                case Op.BITAND: case Op.BITOR: case Op.BITXOR:
                    return KindOf(c[pc + 2], s) == L_ && KindOf(c[pc + 3], s) == L_ ? L_ : V_;
                case Op.NEG:
                    {
                        byte a = KindOf(c[pc + 2], s);
                        return Num(a) ? a : V_;
                    }
                case Op.FORITER_RANGE: return L_;
                default: return V_;
            }
        }

        void Write(byte[] s, int r, byte t)
        {
            if (_forced[r] != 0) s[r] = _forced[r];
            else if (_never[r]) s[r] = V_;
            else s[r] = t;
        }

        /// <summary>Abstract interpretation of one instruction.</summary>
        void Step(int pc, byte[] s)
        {
            var c = _code;
            var op = (Op)c[pc];
            switch (op)
            {
                case Op.FORITER_RANGE:
                    Write(s, c[pc + 2], L_);
                    Write(s, c[pc + 1], L_);
                    return;
                case Op.FORPREP_RANGE:
                    Write(s, c[pc + 1], L_); Write(s, c[pc + 1] + 1, L_); Write(s, c[pc + 1] + 2, L_);
                    return;
                case Op.UNPACK:
                    for (int i = 0; i < c[pc + 2]; i++) Write(s, c[pc + 1] + i, V_);
                    return;
                case Op.UNPACKEX:
                    for (int i = 0; i <= c[pc + 2] + c[pc + 3]; i++) Write(s, c[pc + 1] + i, V_);
                    return;
                case Op.FORITER:
                    Write(s, c[pc + 2], V_);
                    return;
            }
            int d = Dest(pc);
            if (d >= 0) Write(s, d, ResultKind(pc, s));
        }

        int Dest(int pc)
        {
            var c = _code;
            switch ((Op)c[pc])
            {
                case Op.MOVE: case Op.LOADK: case Op.LOADGLOBAL: case Op.LOADDEREF: case Op.LOADCELL: case Op.LOADNAME:
                case Op.CHECKTYPE: case Op.MKCELL: case Op.DELLOCAL: case Op.NEG: case Op.POS: case Op.NOT: case Op.INVERT:
                case Op.GETATTR: case Op.GETITEM: case Op.SLICE: case Op.CALL: case Op.CALLKW: case Op.CALLEX: case Op.CALLM: case Op.CALLMKW:
                case Op.BUILDLIST: case Op.BUILDTUPLE: case Op.BUILDSET: case Op.BUILDDICT: case Op.LISTTOTUPLE: case Op.GETITER:
                case Op.MAKEFUNC: case Op.MAKECLASS: case Op.SUPER: case Op.EXCMATCH: case Op.YIELD: case Op.FORMAT: case Op.BUILDSTR:
                case Op.IMPORT: case Op.IMPORTFROM:
                    return c[pc + 1];
                default:
                    return OpInfo.IsBinary((Op)c[pc]) ? c[pc + 1] : -1;
            }
        }

        IEnumerable<int> Successors(int pc)
        {
            var c = _code;
            var op = (Op)c[pc];
            int next = pc + OpInfo.Len[c[pc]];
            switch (op)
            {
                case Op.JMP: yield return c[pc + 1]; yield break;
                case Op.RET: case Op.RETNONE: case Op.RAISE: case Op.RAISEFROM: case Op.RERAISE: yield break;
                case Op.JT: case Op.JF: yield return c[pc + 2]; break;
                case Op.JCMP: case Op.JNCMP: yield return c[pc + 4]; break;
                case Op.FORITER: case Op.FORITER_RANGE: yield return c[pc + 3]; break;
            }
            if (next < c.Length) yield return next;
        }

        static bool EndsBlock(Op op) =>
            op == Op.JMP || op == Op.JT || op == Op.JF || op == Op.JCMP || op == Op.JNCMP || op == Op.FORITER || op == Op.FORITER_RANGE
            || op == Op.RET || op == Op.RETNONE || op == Op.RAISE || op == Op.RAISEFROM || op == Op.RERAISE;

        void Analyze()
        {
            var c = _code;
            for (int pc = 0; pc < c.Length; pc += OpInfo.Len[c[pc]])
            {
                _starts.Add(pc);
                var op = (Op)c[pc];
                if (op == Op.CHKBOUND) _never[c[pc + 1]] = true;
                if (op == Op.FORPREP_RANGE)
                {
                    int iter = pc + OpInfo.Len[c[pc]];
                    if ((Op)c[iter] != Op.FORITER_RANGE || c[iter + 1] != c[pc + 1]) throw new InvalidOperationException("unexpected range loop shape");
                }
                if (EndsBlock(op))
                {
                    int next = pc + OpInfo.Len[c[pc]];
                    if (next < c.Length) _leaders.Add(next);
                    foreach (var t in Successors(pc))
                    {
                        _leaders.Add(t);
                        if (t <= pc) _entries.Add(t);
                    }
                }
            }
            for (int r = 0; r < _n; r++)
            {
                byte t = _fc.RegTypes != null && r < _fc.RegTypes.Length ? _fc.RegTypes[r] : (byte)0;
                if (t != 0 && !_never[r]) _forced[r] = t;
            }

            // forward data-flow to a fixed point over basic blocks
            var entry = new byte[_n];
            for (int r = 0; r < _n; r++)
                entry[r] = _forced[r] != 0 ? _forced[r] : r < _fc.NumParams || _never[r] ? V_ : U_;
            _in[0] = entry;
            var work = new Queue<int>();
            var queued = new HashSet<int>();
            work.Enqueue(0);
            queued.Add(0);
            var leaders = _leaders.ToArray();
            while (work.Count > 0)
            {
                int b = work.Dequeue();
                queued.Remove(b);
                var s = (byte[])_in[b].Clone();
                int pc = b;
                while (true)
                {
                    Step(pc, s);
                    var op = (Op)c[pc];
                    int next = pc + OpInfo.Len[c[pc]];
                    bool end = EndsBlock(op) || next >= c.Length || _leaders.Contains(next);
                    if (!end) { pc = next; continue; }
                    foreach (var t in Successors(pc))
                    {
                        // FORITER exit: the loop target was not written on that edge, but V/L joins conservatively anyway
                        if (Merge(t, s) && queued.Add(t)) work.Enqueue(t);
                    }
                    break;
                }
            }
        }

        bool Merge(int target, byte[] s)
        {
            if (!_in.TryGetValue(target, out var cur))
            {
                _in[target] = (byte[])s.Clone();
                return true;
            }
            bool changed = false;
            for (int r = 0; r < _n; r++)
            {
                byte j = Join(cur[r], s[r]);
                if (j != cur[r]) { cur[r] = j; changed = true; }
            }
            return changed;
        }

        // ================================================================ register access
        Expression V(int r)
        {
            switch (_s[r])
            {
                case L_: return Call(MInt, RL(r));
                case D_: return Call(MFloat, RD(r));
                default: return _rv[r];
            }
        }

        Expression KV(int idx)
        {
            var v = _consts[idx];
            if (v.IsInt) return Call(MInt, Constant(v.N));
            if (v.IsFloat) return Call(MFloat, Constant(v.AsFloat));
            if (v.IsBool) return Call(MBool, Constant(v.AsBool));
            if (v.IsNone) return Field(null, FNone);
            return ArrayIndex(Constant(_consts), Constant(idx));
        }

        Expression RK(int o) => o >= 0 ? V(o) : KV(~o);
        byte Kind(int o) => KindOf(o, _s);
        Expression AsL(int o) => o < 0 ? Constant(_consts[~o].N) : (Expression)RL(o);
        Expression AsD(int o)
        {
            if (o < 0) { var v = _consts[~o]; return Constant(v.IsFloat ? v.AsFloat : v.N); }
            return _s[o] == D_ ? (Expression)RD(o) : Convert(RL(o), typeof(double));
        }

        /// <summary>Writes a boxed value; the register's representation after the write follows <see cref="Write"/>.</summary>
        Expression WriteV(int r, Expression value)
        {
            if (_forced[r] == L_) return Assign(RL(r), Call(J(nameof(JitOps.ToLongTyped)), value));
            if (_forced[r] == D_) return Assign(RD(r), Call(J(nameof(JitOps.ToDoubleTyped)), value));
            return Assign(_rv[r], value);
        }

        Expression WriteL(int r, Expression x)
        {
            if (_forced[r] == D_) return Assign(RD(r), Convert(x, typeof(double)));
            if (_never[r]) return Assign(_rv[r], Call(MInt, x));
            return Assign(RL(r), x);
        }

        Expression WriteD(int r, Expression x)
        {
            if (_forced[r] == L_) return Assign(RL(r), Call(J(nameof(JitOps.ToLongTyped)), Call(MFloat, x)));
            if (_never[r]) return Assign(_rv[r], Call(MFloat, x));
            return Assign(RD(r), x);
        }

        /// <summary>Materializes unboxed registers into their boxed locals where <paramref name="target"/> expects boxed.</summary>
        IEnumerable<Expression> ConvertTo(byte[] target)
        {
            for (int r = 0; r < _n; r++)
            {
                byte cur = _s[r];
                if (cur == V_ || cur == U_) continue;
                if (target != null && target[r] == cur) continue;
                yield return Assign(_rv[r], cur == L_ ? Call(MInt, RL(r)) : Call(MFloat, RD(r)));
            }
        }

        /// <summary>Leaves compiled code: box everything, store pc, return <paramref name="status"/> (1 = suspended, 2 = deopt).</summary>
        Expression Leave(int resumePc, int status)
        {
            var b = new List<Expression>(ConvertTo(null))
            {
                Assign(_pc, Constant(resumePc)),
                Assign(_st, Constant(status)),
                Goto(_spill)
            };
            return Block(b);
        }

        Expression JumpTo(int from, int to)
        {
            var conv = ConvertTo(_in.TryGetValue(to, out var t) ? t : null).ToList();
            Expression jump = Goto(Lbl(to));
            if (to <= from)
                jump = Block(
                    IfThen(LessThanOrEqual(PreDecrementAssign(_budget), Constant(0)),
                        IfThen(Call(J(nameof(JitOps.Safepoint)), _vm, _budget), Leave(to, 1))),
                    Goto(Lbl(to)));
            if (conv.Count == 0) return jump;
            conv.Add(jump);
            return Block(conv);
        }

        Expression Truthy(int r)
        {
            if (_s[r] == L_) return NotEqual(RL(r), Constant(0L));
            if (_s[r] == D_) return NotEqual(RD(r), Constant(0.0));
            return Call(J(nameof(JitOps.Truthy)), _rv[r]);
        }

        static Expression Cmp(Cmp c, Expression a, Expression b)
        {
            switch (c)
            {
                case DynLan2.Cmp.LT: return LessThan(a, b);
                case DynLan2.Cmp.LE: return LessThanOrEqual(a, b);
                case DynLan2.Cmp.GT: return GreaterThan(a, b);
                case DynLan2.Cmp.GE: return GreaterThanOrEqual(a, b);
                case DynLan2.Cmp.EQ: return Equal(a, b);
                default: return NotEqual(a, b);
            }
        }

        Expression Compare(Cmp c, int a, int b)
        {
            byte ka = Kind(a), kb = Kind(b);
            if (ka == L_ && kb == L_) return Cmp(c, AsL(a), AsL(b));
            if (Num(ka) && Num(kb)) return Cmp(c, AsD(a), AsD(b));
            return Call(J(nameof(JitOps.CmpOp)), Constant((int)c), RK(a), RK(b));
        }

        // ================================================================ compile
        public JitFn Compile()
        {
            Analyze();
            foreach (var e in _entries) Lbl(e);
            var body = new List<Expression>();
            var c = _code;
            _s = (byte[])_in[0].Clone();
            bool reachable = true;
            foreach (int pc in _starts)
            {
                if (_leaders.Contains(pc))
                {
                    if (!_in.TryGetValue(pc, out var inState))
                    {
                        reachable = false; // dead code (never reached by data-flow)
                        continue;
                    }
                    if (reachable) body.AddRange(ConvertTo(inState));
                    body.Add(Label(Lbl(pc)));
                    _s = (byte[])inState.Clone();
                    reachable = true;
                }
                else if (!reachable) continue;
                body.Add(Assign(_pc, Constant(pc)));
                body.Add(Instr(pc));
                Step(pc, _s);
                var op = (Op)c[pc];
                if (op == Op.JMP || op == Op.RET || op == Op.RETNONE || op == Op.RAISE || op == Op.RAISEFROM || op == Op.RERAISE)
                    reachable = false;
            }
            if (reachable) body.Add(Return(_ret, Constant(0)));

            // shared exit: store every boxed register into the frame
            body.Add(Label(_spill));
            for (int r = 0; r < _n; r++) body.Add(Assign(ArrayAccess(_arr, Constant(r)), _rv[r]));
            body.Add(Assign(Field(_f, nameof(Frame.Pc)), _pc));
            body.Add(Assign(Field(_f, nameof(Frame.PcExact)), Constant(false)));
            body.Add(Return(_ret, _st));

            // entry dispatch (function start + loop headers for OSR / resume)
            var cases = new List<SwitchCase>();
            foreach (var e in _entries)
            {
                if (!_in.TryGetValue(e, out var st)) continue;
                var pro = new List<Expression>();
                for (int r = 0; r < _n; r++)
                {
                    var src = ArrayIndex(_arr, Constant(r));
                    switch (st[r])
                    {
                        case L_:
                            if (_forced[r] == 0) pro.Add(IfThen(Not(Call(J(nameof(JitOps.IsIntOrUnbound)), src)), Return(_ret, Constant(2))));
                            pro.Add(Assign(RL(r), Call(J(nameof(JitOps.LoadL)), src)));
                            break;
                        case D_:
                            if (_forced[r] == 0) pro.Add(IfThen(Not(Call(J(nameof(JitOps.IsFloatOrUnbound)), src)), Return(_ret, Constant(2))));
                            pro.Add(Assign(RD(r), Call(J(nameof(JitOps.LoadD)), src)));
                            break;
                        case U_:
                            // fresh call: never-assigned registers are unbound (locals start zeroed = unbound)
                            if (e != 0) pro.Add(Assign(_rv[r], src));
                            break;
                        default:
                            pro.Add(Assign(_rv[r], src));
                            break;
                    }
                }
                pro.Add(Assign(_budget, Constant(1)));
                pro.Add(Goto(Lbl(e)));
                cases.Add(SwitchCase(Block(pro), Constant(e)));
            }
            var dispatch = Switch(Field(_f, nameof(Frame.Pc)), Return(_ret, Constant(2)), cases.ToArray());

            var ex = Variable(typeof(Exception), "ex");
            var tryBody = Block(typeof(void), new[] { (Expression)dispatch }.Concat(body));
            var guarded = TryCatch(tryBody,
                Catch(ex, Block(typeof(void),
                    Assign(Field(_f, nameof(Frame.Pc)), _pc),
                    Assign(Field(_f, nameof(Frame.PcExact)), Constant(true)),
                    Rethrow())));

            var all = new List<Expression> { Assign(_arr, Field(_f, nameof(Frame.R))), guarded, Label(_ret, Constant(0)) };
            var vars = new List<ParameterExpression> { _arr, _pc, _st, _tmp, _tl, _tmpArr, _argBase, _argArr, _budget };
            vars.AddRange(_rv);
            vars.AddRange(_rl.Where(x => x != null));
            vars.AddRange(_rd.Where(x => x != null));
            var lambda = Lambda<JitFn>(Block(typeof(int), vars, all), "jit_" + _fc.Name, new[] { _vm, _f });
            return lambda.Compile();
        }

        Expression Fallback(int pc)
        {
            var b = new List<Expression>(ConvertTo(null));
            for (int r = 0; r < _n; r++) b.Add(Assign(ArrayAccess(_arr, Constant(r)), _rv[r]));
            b.Add(Call(_vm, typeof(VM).GetMethod(nameof(VM.ExecSimple), BindingFlags.Instance | BindingFlags.NonPublic), _f, _arr, Constant(_code), Constant(pc)));
            // reload what the instruction may have written (everything boxed per the post-state)
            var after = (byte[])_s.Clone();
            Step(pc, after);
            for (int r = 0; r < _n; r++)
            {
                var src = ArrayIndex(_arr, Constant(r));
                if (after[r] == L_) b.Add(Assign(RL(r), Call(J(nameof(JitOps.LoadL)), src)));
                else if (after[r] == D_) b.Add(Assign(RD(r), Call(J(nameof(JitOps.LoadD)), src)));
                else b.Add(Assign(_rv[r], src));
            }
            return Block(b);
        }

        Expression Instr(int pc)
        {
            var c = _code;
            var op = (Op)c[pc];
            int next = pc + OpInfo.Len[c[pc]];
            switch (op)
            {
                case Op.NOP: return Empty();
                case Op.MOVE:
                    {
                        int d = c[pc + 1], s = c[pc + 2];
                        byte k = Kind(s);
                        if (k == L_) return WriteL(d, RL(s));
                        if (k == D_) return WriteD(d, RD(s));
                        return WriteV(d, _rv[s]);
                    }
                case Op.LOADK:
                    {
                        int d = c[pc + 1];
                        var k = _consts[c[pc + 2]];
                        if (k.IsInt) return WriteL(d, Constant(k.N));
                        if (k.IsFloat) return WriteD(d, Constant(k.AsFloat));
                        return WriteV(d, KV(c[pc + 2]));
                    }
                case Op.LOADGLOBAL: return WriteV(c[pc + 1], Call(J(nameof(JitOps.LoadGlobal)), _f, Constant(c[pc + 2])));
                case Op.STOREGLOBAL: return Call(J(nameof(JitOps.StoreGlobal)), _f, Constant(c[pc + 1]), V(c[pc + 2]));
                case Op.LOADDEREF: return WriteV(c[pc + 1], Call(J(nameof(JitOps.LoadDeref)), _f, Constant(c[pc + 2])));
                case Op.STOREDEREF: return Call(J(nameof(JitOps.StoreDeref)), _f, Constant(c[pc + 1]), V(c[pc + 2]));
                case Op.LOADCELL: return WriteV(c[pc + 1], Call(J(nameof(JitOps.LoadCell)), V(c[pc + 2])));
                case Op.STORECELL: return Call(J(nameof(JitOps.StoreCell)), V(c[pc + 1]), V(c[pc + 2]));
                case Op.MKCELL: return WriteV(c[pc + 1], Call(J(nameof(JitOps.MkCell)), V(c[pc + 1])));
                case Op.CHKBOUND:
                    {
                        int r = c[pc + 1];
                        if (_s[r] != V_) return Empty();
                        return IfThen(Call(J(nameof(JitOps.IsUnbound)), _rv[r]), Call(J(nameof(JitOps.ThrowUnbound)), Constant(_fc.Names[c[pc + 2]])));
                    }
                case Op.CHECKTYPE:
                    {
                        int r = c[pc + 1];
                        if (_forced[r] != 0) return Empty();
                        return WriteV(r, Call(typeof(VM).GetMethod(nameof(VM.CheckType), BindingFlags.Static | BindingFlags.NonPublic), V(r), Constant(c[pc + 2])));
                    }

                case Op.ADD: case Op.SUB: case Op.MUL: case Op.IADD:
                    return Arith(pc, op == Op.IADD ? Op.ADD : op, op == Op.IADD);
                case Op.TRUEDIV:
                    {
                        int d = c[pc + 1], a = c[pc + 2], b = c[pc + 3];
                        byte ka = Kind(a), kb = Kind(b);
                        if (Num(ka) && Num(kb))
                            return WriteD(d, Call(J(nameof(JitOps.DivDD)), AsD(a), AsD(b), Constant(ka == D_ || kb == D_)));
                        return WriteV(d, Call(OpsM(nameof(Ops.TrueDiv), typeof(Value), typeof(Value)), RK(a), RK(b)));
                    }
                case Op.FLOORDIV:
                case Op.MOD:
                    {
                        int d = c[pc + 1], a = c[pc + 2], b = c[pc + 3];
                        byte ka = Kind(a), kb = Kind(b);
                        if (ka == L_ && kb == L_)
                        {
                            if (op == Op.MOD) return WriteL(d, Call(OpsM(nameof(Ops.ModLong), typeof(long), typeof(long)), AsL(a), AsL(b)));
                            Expression guard = AndAlso(Equal(AsL(a), Constant(long.MinValue)), Equal(AsL(b), Constant(-1L)));
                            return Block(
                                IfThen(guard, _forced[d] == L_ ? (Expression)Throw(Constant(new OverflowException())) : Leave(pc, 2)),
                                WriteL(d, Call(OpsM(nameof(Ops.FloorDivLong), typeof(long), typeof(long)), AsL(a), AsL(b))));
                        }
                        if (Num(ka) && Num(kb))
                            return WriteD(d, Call(J(op == Op.MOD ? nameof(JitOps.ModDD) : nameof(JitOps.FloorDivDD)), AsD(a), AsD(b)));
                        return WriteV(d, Call(J(op == Op.MOD ? nameof(JitOps.Mod) : nameof(JitOps.FloorDiv)), RK(a), RK(b)));
                    }
                case Op.BITAND: case Op.BITOR: case Op.BITXOR:
                    {
                        int d = c[pc + 1], a = c[pc + 2], b = c[pc + 3];
                        if (Kind(a) == L_ && Kind(b) == L_)
                            return WriteL(d, op == Op.BITAND ? And(AsL(a), AsL(b)) : op == Op.BITOR ? Or(AsL(a), AsL(b)) : ExclusiveOr(AsL(a), AsL(b)));
                        return WriteV(d, Call(OpsM(nameof(Ops.Binary), typeof(Op), typeof(Value), typeof(Value)), Constant(op), RK(a), RK(b)));
                    }
                case Op.LT: case Op.LE: case Op.GT: case Op.GE: case Op.EQ: case Op.NE:
                    return WriteV(c[pc + 1], Call(MBool, Compare((Cmp)(op - Op.LT), c[pc + 2], c[pc + 3])));
                case Op.POW: case Op.LSHIFT: case Op.RSHIFT: case Op.MATMUL:
                case Op.IN: case Op.NOTIN: case Op.IS: case Op.ISNOT:
                    return WriteV(c[pc + 1], Call(OpsM(nameof(Ops.Binary), typeof(Op), typeof(Value), typeof(Value)), Constant(op), RK(c[pc + 2]), RK(c[pc + 3])));
                case Op.NEG:
                    {
                        int d = c[pc + 1], s = c[pc + 2];
                        byte k = Kind(s);
                        if (k == L_)
                        {
                            if (_forced[d] == L_) return Assign(RL(d), NegateChecked(RL(s)));
                            return Block(IfThen(Equal(RL(s), Constant(long.MinValue)), Leave(pc, 2)), WriteL(d, Negate(RL(s))));
                        }
                        if (k == D_) return WriteD(d, Negate(RD(s)));
                        return WriteV(d, Call(OpsM(nameof(Ops.Neg), typeof(Value)), V(s)));
                    }
                case Op.NOT: return WriteV(c[pc + 1], Call(MBool, Not(Truthy(c[pc + 2]))));
                case Op.POS: return WriteV(c[pc + 1], Call(OpsM(nameof(Ops.Pos), typeof(Value)), V(c[pc + 2])));
                case Op.INVERT: return WriteV(c[pc + 1], Call(OpsM(nameof(Ops.Invert), typeof(Value)), V(c[pc + 2])));

                case Op.JMP: return JumpTo(pc, c[pc + 1]);
                case Op.JT: return IfThen(Truthy(c[pc + 1]), JumpTo(pc, c[pc + 2]));
                case Op.JF: return IfThen(Not(Truthy(c[pc + 1])), JumpTo(pc, c[pc + 2]));
                case Op.JCMP:
                case Op.JNCMP:
                    {
                        var cond = Compare((Cmp)c[pc + 1], c[pc + 2], c[pc + 3]);
                        return IfThen(op == Op.JCMP ? cond : Not(cond), JumpTo(pc, c[pc + 4]));
                    }

                case Op.GETATTR: return WriteV(c[pc + 1], Call(J(nameof(JitOps.GetAttr)), V(c[pc + 2]), Constant(_fc.Names[c[pc + 3]])));
                case Op.SETATTR: return Call(J(nameof(JitOps.SetAttr)), V(c[pc + 1]), Constant(_fc.Names[c[pc + 2]]), V(c[pc + 3]));
                case Op.GETITEM:
                    {
                        int d = c[pc + 1], o = c[pc + 2], k = c[pc + 3];
                        if (_s[k] == L_) return WriteV(d, Call(J(nameof(JitOps.GetItemL)), V(o), RL(k)));
                        return WriteV(d, Call(J(nameof(JitOps.GetItem)), V(o), V(k)));
                    }
                case Op.SETITEM:
                    {
                        int o = c[pc + 1], k = c[pc + 2], v = c[pc + 3];
                        if (_s[k] == L_) return Call(J(nameof(JitOps.SetItemL)), V(o), RL(k), V(v));
                        return Call(J(nameof(JitOps.SetItem)), V(o), V(k), V(v));
                    }
                case Op.LISTAPPEND: return Call(J(nameof(JitOps.ListAppend)), V(c[pc + 1]), V(c[pc + 2]));
                case Op.DICTSET: return Call(J(nameof(JitOps.DictSet)), V(c[pc + 1]), V(c[pc + 2]), V(c[pc + 3]));
                case Op.SETADD: return Call(J(nameof(JitOps.SetAdd)), V(c[pc + 1]), V(c[pc + 2]));
                case Op.BUILDLIST:
                case Op.BUILDTUPLE:
                    {
                        int d = c[pc + 1], b = c[pc + 2], n = c[pc + 3];
                        if (n > 32) return Fallback(pc);
                        var items = NewArrayInit(typeof(Value), Enumerable.Range(b, n).Select(V));
                        return WriteV(d, Call(J(op == Op.BUILDLIST ? nameof(JitOps.BuildList) : nameof(JitOps.BuildTuple)), items));
                    }
                case Op.UNPACK:
                    {
                        int b = c[pc + 1], n = c[pc + 2];
                        var list = new List<Expression> { Assign(_tmpArr, Call(J(nameof(JitOps.Unpack)), V(c[pc + 3]), Constant(n))) };
                        for (int i = 0; i < n; i++) list.Add(WriteV(b + i, ArrayIndex(_tmpArr, Constant(i))));
                        return Block(list);
                    }
                case Op.FORMAT:
                    {
                        int sp = c[pc + 4];
                        return WriteV(c[pc + 1], Call(J(nameof(JitOps.FormatV)), V(c[pc + 2]), Constant(c[pc + 3]), sp >= 0 ? V(sp) : (Expression)Default(typeof(Value))));
                    }
                case Op.BUILDSTR:
                    {
                        int b = c[pc + 2], n = c[pc + 3];
                        return WriteV(c[pc + 1], Call(J(nameof(JitOps.BuildStr)), NewArrayInit(typeof(Value), Enumerable.Range(b, n).Select(V))));
                    }
                case Op.GETITER: return WriteV(c[pc + 1], Call(J(nameof(JitOps.GetIter)), V(c[pc + 2])));
                case Op.FORITER:
                    {
                        int it = c[pc + 1], d = c[pc + 2], exit = c[pc + 3];
                        var after = (byte[])_s.Clone();
                        Step(pc, after);
                        return IfThenElse(Call(J(nameof(JitOps.ForIter)), _vm, V(it), _tmp), WriteV(d, _tmp), JumpTo(pc, exit));
                    }
                case Op.FORPREP_RANGE:
                    {
                        int it = c[pc + 1], fn = c[pc + 2], b = c[pc + 3], n = c[pc + 4];
                        Expression A(int i) => i < n ? V(b + i) : (Expression)Default(typeof(Value));
                        return IfThen(Not(Call(J(nameof(JitOps.RangePrep)), V(fn), A(0), A(1), A(2), Constant(n), RL(it), RL(it + 1), RL(it + 2))),
                            Block(Call(J(nameof(JitOps.NoteDeopt)), Constant(_fc)), Leave(pc, 2)));
                    }
                case Op.FORITER_RANGE:
                    {
                        int it = c[pc + 1], d = c[pc + 2], exit = c[pc + 3];
                        if (_s[it] != L_ || _s[it + 1] != L_ || _s[it + 2] != L_) throw new InvalidOperationException("range counters are not unboxed");
                        var more = Condition(GreaterThan(RL(it + 2), Constant(0L)), LessThan(RL(it), RL(it + 1)), GreaterThan(RL(it), RL(it + 1)));
                        return IfThenElse(more, Block(WriteL(d, RL(it)), AddAssign(RL(it), RL(it + 2))), JumpTo(pc, exit));
                    }

                case Op.CALL:
                case Op.CALLKW:
                    {
                        int d = c[pc + 1], fn = c[pc + 2], b = c[pc + 3], n = c[pc + 4];
                        var kwn = op == Op.CALLKW ? (string[])_consts[c[pc + 5]].O : null;
                        return EmitCall(pc, d, next, Enumerable.Range(b, n).ToArray(),
                            Call(J(nameof(JitOps.Call)), _vm, _f, V(fn), _argBase, Constant(n), Constant(kwn, typeof(string[])), Constant(next), Constant(d)));
                    }
                case Op.CALLM:
                case Op.CALLMKW:
                    {
                        int d = c[pc + 1], b = c[pc + 2], n = c[pc + 4];
                        string name = _fc.Names[c[pc + 3]];
                        var kwn = op == Op.CALLMKW ? (string[])_consts[c[pc + 5]].O : null;
                        return EmitCall(pc, d, next, Enumerable.Range(b, n + 1).ToArray(),
                            Call(J(nameof(JitOps.CallMethod)), _vm, _f, Constant(name), _argBase, Constant(n + 1), Constant(kwn, typeof(string[])), Constant(next), Constant(d)));
                    }
                case Op.CALLEX:
                    {
                        int d = c[pc + 1];
                        int kw = c[pc + 4];
                        var call = Call(J(nameof(JitOps.CallEx)), _vm, _f, V(c[pc + 2]), V(c[pc + 3]), kw >= 0 ? V(kw) : (Expression)Default(typeof(Value)), Constant(next), Constant(d));
                        return CallTail(pc, d, next, call, null);
                    }
                case Op.RET: return Block(Assign(Field(_vm, nameof(VM.Result)), V(c[pc + 1])), Return(_ret, Constant(0)));
                case Op.RETNONE: return Block(Assign(Field(_vm, nameof(VM.Result)), Field(null, FNone)), Return(_ret, Constant(0)));

                default:
                    return Fallback(pc);
            }
        }

        Expression Arith(int pc, Op op, bool iadd)
        {
            var c = _code;
            int d = c[pc + 1], a = c[pc + 2], b = c[pc + 3];
            byte ka = Kind(a), kb = Kind(b);
            if (ka == L_ && kb == L_)
            {
                var x = AsL(a);
                var y = AsL(b);
                if (_forced[d] == L_)
                    return Assign(RL(d), op == Op.ADD ? AddChecked(x, y) : op == Op.SUB ? SubtractChecked(x, y) : MultiplyChecked(x, y));
                Expression overflow;
                Expression compute;
                if (op == Op.MUL)
                {
                    compute = Empty();
                    overflow = Call(J(nameof(JitOps.MulOv)), x, y, _tl);
                }
                else
                {
                    compute = Assign(_tl, op == Op.ADD ? Add(x, y) : Subtract(x, y));
                    overflow = op == Op.ADD
                        ? LessThan(And(ExclusiveOr(x, _tl), ExclusiveOr(y, _tl)), Constant(0L))
                        : LessThan(And(ExclusiveOr(x, y), ExclusiveOr(x, _tl)), Constant(0L));
                }
                return Block(compute, IfThen(overflow, Leave(pc, 2)), WriteL(d, _tl));
            }
            if (Num(ka) && Num(kb))
                return WriteD(d, op == Op.ADD ? Add(AsD(a), AsD(b)) : op == Op.SUB ? Subtract(AsD(a), AsD(b)) : Multiply(AsD(a), AsD(b)));
            string g = iadd ? nameof(JitOps.IAdd) : op == Op.ADD ? nameof(JitOps.Add) : op == Op.SUB ? nameof(JitOps.Sub) : nameof(JitOps.Mul);
            return WriteV(d, Call(J(g), RK(a), RK(b)));
        }

        Expression EmitCall(int pc, int d, int next, int[] argRegs, Expression call)
        {
            var pre = new List<Expression>
            {
                Assign(_argBase, Call(_vm, typeof(VM).GetMethod(nameof(VM.ArgReserve), BindingFlags.Instance | BindingFlags.NonPublic), Constant(argRegs.Length))),
                Assign(_argArr, Field(_vm, nameof(VM.ArgStack))),
            };
            for (int i = 0; i < argRegs.Length; i++)
                pre.Add(Assign(ArrayAccess(_argArr, Add(_argBase, Constant(i))), V(argRegs[i])));
            return CallTail(pc, d, next, call, pre);
        }

        Expression CallTail(int pc, int d, int next, Expression call, List<Expression> pre)
        {
            var b = pre ?? new List<Expression>();
            b.Add(Assign(_st, call));
            // suspended inside the callee: the interpreter will deliver the result into R[d] later
            b.Add(IfThen(Equal(_st, Constant(1)), Leave(next, 1)));
            b.Add(WriteV(d, Field(_vm, nameof(VM.Result))));
            // completed, but a suspension was requested (e.g. checkpoint())
            var after = (byte[])_s.Clone();
            Step(pc, after);
            var save = _s;
            _s = after;
            var leaveAfter = Leave(next, 1);
            _s = save;
            b.Add(IfThen(Equal(_st, Constant(3)), leaveAfter));
            return Block(b);
        }
    }
}
