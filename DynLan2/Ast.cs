using System.Collections.Generic;

namespace DynLan2
{
    internal abstract class Node { public int Line; }

    // ------------------------------------------------------------------ expressions
    internal abstract class Expr : Node { }

    internal sealed class NameExpr : Expr { public string Id; }
    internal sealed class ConstExpr : Expr { public Value Value; }
    internal sealed class BinExpr : Expr { public string Op; public Expr L, R; }
    internal sealed class UnaryExpr : Expr { public string Op; public Expr X; }
    internal sealed class BoolExpr : Expr { public bool IsAnd; public List<Expr> Values; }
    internal sealed class CompareExpr : Expr { public Expr Left; public List<string> Ops; public List<Expr> Rights; }
    internal sealed class IfExpr : Expr { public Expr Test, Body, Else; }
    internal sealed class LambdaExpr : Expr { public Params Args; public Expr Body; public Scope Scope; }
    internal sealed class CallExpr : Expr { public Expr Func; public List<Expr> Args; public List<Keyword> Keywords; }
    internal sealed class Keyword { public string Name; public Expr Value; } // Name == null => **kwargs
    internal sealed class AttrExpr : Expr { public Expr Obj; public string Name; }
    internal sealed class IndexExpr : Expr { public Expr Obj; public Expr Index; }
    internal sealed class SliceExpr : Expr { public Expr Lo, Hi, Step; }
    internal sealed class ListExpr : Expr { public List<Expr> Items; }
    internal sealed class TupleExpr : Expr { public List<Expr> Items; }
    internal sealed class SetExpr : Expr { public List<Expr> Items; }
    internal sealed class DictExpr : Expr { public List<Expr> Keys; public List<Expr> Values; } // key null => **expr
    internal sealed class StarredExpr : Expr { public Expr Value; }
    internal sealed class YieldExpr : Expr { public Expr Value; public bool From; }
    internal sealed class NamedExpr : Expr { public NameExpr Target; public Expr Value; }
    internal sealed class FStringExpr : Expr { public List<Expr> Parts; } // ConstExpr(str) or FormattedValue
    internal sealed class FormattedValue : Expr { public Expr Value; public char Conv; public Expr Spec; }

    internal sealed class Comprehension { public Expr Target; public Expr Iter; public List<Expr> Ifs = new List<Expr>(); }

    internal sealed class CompExpr : Expr
    {
        public char Kind; // 'l' list, 's' set, 'd' dict, 'g' generator
        public Expr Elt, Value; // Value only for dict
        public List<Comprehension> Gens;
        public Scope Scope;
    }

    // ------------------------------------------------------------------ statements
    internal abstract class Stmt : Node { }

    internal sealed class ExprStmt : Stmt { public Expr X; }
    internal sealed class AssignStmt : Stmt { public List<Expr> Targets; public Expr Value; }
    internal sealed class AugAssignStmt : Stmt { public Expr Target; public string Op; public Expr Value; }
    internal sealed class AnnAssignStmt : Stmt { public Expr Target; public Expr Annotation; public Expr Value; }
    internal sealed class IfStmt : Stmt { public Expr Test; public List<Stmt> Body, Else; }
    internal sealed class WhileStmt : Stmt { public Expr Test; public List<Stmt> Body, Else; }
    internal sealed class ForStmt : Stmt { public Expr Target, Iter; public List<Stmt> Body, Else; }
    internal sealed class BreakStmt : Stmt { }
    internal sealed class ContinueStmt : Stmt { }
    internal sealed class PassStmt : Stmt { }
    internal sealed class ReturnStmt : Stmt { public Expr Value; }
    internal sealed class RaiseStmt : Stmt { public Expr Exc, Cause; }
    internal sealed class GlobalStmt : Stmt { public List<string> Names; }
    internal sealed class NonlocalStmt : Stmt { public List<string> Names; }
    internal sealed class DelStmt : Stmt { public List<Expr> Targets; }
    internal sealed class AssertStmt : Stmt { public Expr Test, Msg; }
    internal sealed class ImportStmt : Stmt { public List<(string Module, string As)> Names; }
    internal sealed class FromImportStmt : Stmt { public string Module; public List<(string Name, string As)> Names; }

    internal sealed class Param { public string Name; public Expr Annotation; public Expr Default; }

    internal sealed class Params
    {
        public List<Param> Positional = new List<Param>();
        public Param VarArg;
        public List<Param> KwOnly = new List<Param>();
        public Param KwArg;
    }

    internal sealed class FuncDef : Stmt
    {
        public string Name;
        public Params Args;
        public List<Stmt> Body;
        public Expr Returns;
        public List<Expr> Decorators = new List<Expr>();
        public Scope Scope;
    }

    internal sealed class ClassDef : Stmt
    {
        public string Name;
        public List<Expr> Bases;
        public List<Keyword> Keywords;
        public List<Stmt> Body;
        public List<Expr> Decorators = new List<Expr>();
        public Scope Scope;
    }

    internal sealed class ExceptHandler : Node { public Expr Type; public string Name; public List<Stmt> Body; }

    internal sealed class TryStmt : Stmt
    {
        public List<Stmt> Body;
        public List<ExceptHandler> Handlers = new List<ExceptHandler>();
        public List<Stmt> Else, Finally;
    }

    internal sealed class WithItem { public Expr Context; public Expr Target; }
    internal sealed class WithStmt : Stmt { public List<WithItem> Items; public List<Stmt> Body; }

    internal sealed class ModuleNode : Node { public List<Stmt> Body; public Scope Scope; }
}
