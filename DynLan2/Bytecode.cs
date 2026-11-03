using System;
using System.Collections.Generic;
using System.Linq;

namespace DynLan2
{
    /// <summary>
    /// Register-based instruction set. Each instruction is the opcode followed by a fixed number of int operands.
    /// Operands marked "rk" may be a register (>= 0) or a constant (~constIndex, i.e. negative).
    /// </summary>
    internal enum Op : int
    {
        NOP,
        MOVE,           // d s
        LOADK,          // d k
        LOADGLOBAL,     // d g
        STOREGLOBAL,    // g s
        DELGLOBAL,      // g
        LOADDEREF,      // d i
        STOREDEREF,     // i s
        LOADCELL,       // d r
        STORECELL,      // r s
        MKCELL,         // r
        DELCELL,        // r
        LOADNAME,       // d n
        STORENAME,      // n s
        DELNAME,        // n
        CHKBOUND,       // r n
        DELLOCAL,       // r
        CHECKTYPE,      // r t   (1 int, 2 float)

        ADD, SUB, MUL, TRUEDIV, FLOORDIV, MOD, POW, LSHIFT, RSHIFT, BITAND, BITOR, BITXOR, MATMUL, IADD, // d rk rk
        LT, LE, GT, GE, EQ, NE, IN, NOTIN, IS, ISNOT,                                                   // d rk rk
        NEG, POS, NOT, INVERT,  // d s

        JMP,            // t
        JT,             // c t
        JF,             // c t
        JCMP,           // cmp rk rk t   jump if comparison is true
        JNCMP,          // cmp rk rk t   jump if comparison is false

        GETATTR,        // d o n
        SETATTR,        // o n s
        DELATTR,        // o n
        GETITEM,        // d o k
        SETITEM,        // o k s
        DELITEM,        // o k
        SLICE,          // d lo hi step   (-1 = None)

        CALL,           // d f base argc
        CALLKW,         // d f base argc kwk     (kwk: const index of string[] kw names; last kw values are at the end)
        CALLEX,         // d f args kw           (args: list/tuple reg, kw: dict reg or -1)
        CALLM,          // d base n argc         (R[base] = receiver, args at base+1...)
        CALLMKW,        // d base n argc kwk
        RET,            // s
        RETNONE,

        BUILDLIST,      // d base n
        BUILDTUPLE,     // d base n
        BUILDSET,       // d base n
        BUILDDICT,      // d base npairs
        LISTAPPEND,     // l v
        SETADD,         // s v
        DICTSET,        // d k v
        LISTEXTEND,     // l v
        SETUPDATE,      // s v
        DICTUPDATE,     // d v
        LISTTOTUPLE,    // d s
        UNPACK,         // base n s
        UNPACKEX,       // base before after s

        GETITER,        // d s
        FORITER,        // it d exit
        FORPREP_RANGE,  // it f base argc   (it, it+1, it+2 = cur, stop, step)
        FORITER_RANGE,  // it d exit

        MAKEFUNC,       // d child defBase ndef kwBase nkw
        MAKECLASS,      // d child base nbases
        SUPER,          // d self

        RAISE,          // s
        RAISEFROM,      // s c
        RERAISE,        // s
        EXCMATCH,       // d e t
        YIELD,          // d s

        FORMAT,         // d s conv spec   (spec reg or -1)
        BUILDSTR,       // d base n
        IMPORT,         // d n
        IMPORTFROM,     // d m n
        ASSERTFAIL,     // s (-1 = no message)

        COUNT
    }

    internal enum Cmp : int { LT, LE, GT, GE, EQ, NE }

    internal static class OpInfo
    {
        public static readonly int[] Len = new int[(int)Op.COUNT];

        static OpInfo()
        {
            void S(int n, params Op[] ops) { foreach (var o in ops) Len[(int)o] = n + 1; }
            S(0, Op.NOP, Op.RETNONE);
            S(1, Op.DELGLOBAL, Op.MKCELL, Op.DELCELL, Op.DELNAME, Op.DELLOCAL, Op.JMP, Op.RET, Op.RAISE, Op.RERAISE, Op.ASSERTFAIL);
            S(2, Op.MOVE, Op.LOADK, Op.LOADGLOBAL, Op.STOREGLOBAL, Op.LOADDEREF, Op.STOREDEREF, Op.LOADCELL, Op.STORECELL,
                Op.LOADNAME, Op.STORENAME, Op.CHKBOUND, Op.CHECKTYPE, Op.NEG, Op.POS, Op.NOT, Op.INVERT, Op.JT, Op.JF,
                Op.DELATTR, Op.DELITEM, Op.LISTAPPEND, Op.SETADD, Op.LISTEXTEND, Op.SETUPDATE, Op.DICTUPDATE, Op.LISTTOTUPLE,
                Op.GETITER, Op.SUPER, Op.RAISEFROM, Op.YIELD, Op.IMPORT);
            S(3, Op.ADD, Op.SUB, Op.MUL, Op.TRUEDIV, Op.FLOORDIV, Op.MOD, Op.POW, Op.LSHIFT, Op.RSHIFT, Op.BITAND, Op.BITOR,
                Op.BITXOR, Op.MATMUL, Op.IADD, Op.LT, Op.LE, Op.GT, Op.GE, Op.EQ, Op.NE, Op.IN, Op.NOTIN, Op.IS, Op.ISNOT,
                Op.GETATTR, Op.SETATTR, Op.GETITEM, Op.SETITEM, Op.BUILDLIST, Op.BUILDTUPLE, Op.BUILDSET, Op.BUILDDICT,
                Op.DICTSET, Op.UNPACK, Op.FORITER, Op.FORITER_RANGE, Op.EXCMATCH, Op.BUILDSTR, Op.IMPORTFROM);
            S(4, Op.JCMP, Op.JNCMP, Op.SLICE, Op.CALL, Op.CALLEX, Op.CALLM, Op.UNPACKEX, Op.MAKECLASS, Op.FORMAT, Op.FORPREP_RANGE);
            S(5, Op.CALLKW, Op.CALLMKW);
            S(6, Op.MAKEFUNC);
            for (int i = 0; i < Len.Length; i++)
                if (Len[i] == 0) throw new InvalidOperationException("missing length for " + (Op)i);
        }

        public static bool IsBinary(Op op) => op >= Op.ADD && op <= Op.ISNOT;
    }

    internal sealed class Handler
    {
        public int Start, End, Target, ExcReg;
    }

    internal sealed class ChildRef
    {
        public FunctionCode Code;
        public int[] FreeSources; // >= 0: parent register holding a Cell; < 0: ~index into parent's closure
    }

    internal delegate int JitFn(VM vm, Frame f);

    /// <summary>Compiled (or lazily compilable) unit: module body, function, lambda, class body, generator expression.</summary>
    internal sealed class FunctionCode
    {
        public long Key;
        public string Name;
        public Script Script;
        public Scope Scope;

        // signature
        public int NumPos;
        public int NumKwOnly;
        public bool HasVarArgs, HasKwArgs;
        public string[] ParamNames;
        public int NumParams;
        public int[] PosDefaultsStart; // unused placeholder for symmetry
        public string[] FreeNames;
        public bool IsGenerator, IsClassBody, IsModule;

        // compiled
        public volatile bool Compiled;
        public int[] Code;
        public int[] Lines;
        public Value[] Consts;
        public string[] Names;
        public string[] GlobalNames = Array.Empty<string>();
        public Handler[] Handlers;
        public ChildRef[] Children;
        public int NumRegs;
        public byte[] RegTypes;     // per register: 0 dynamic, 1 int (annotated), 2 float (annotated)
        public bool HasYield;
        public bool HasHandlers;
        public bool HasClassDef;

        // tiering
        public int Hot;
        public volatile JitFn Jit;
        public int JitState;        // 0 none, 1 compiling, 2 ready, 3 failed / disabled
        public int Deopts;

        readonly object _lock = new object();

        public int LineAt(int pc) => Lines == null || Lines.Length == 0 ? 0 : Lines[Math.Max(0, Math.Min(pc, Lines.Length - 1))];

        public void EnsureCompiled()
        {
            if (Compiled) return;
            lock (_lock)
            {
                if (Compiled) return;
                if (Scope.Lazy) ScopeAnalyzer.AnalyzeRoot(Scope, Script.File, Script.Register);
                // scope facts are complete only after analysis of the enclosing unit
                IsGenerator = Scope.IsGenerator;
                FreeNames = Scope.FreeNames.ToArray();
                ParamNames = Scope.ParamNames.ToArray();
                NumParams = ParamNames.Length;
                CodeGen.Compile(this);
                Compiled = true;
            }
        }

        public override string ToString() => Name;
    }
}
