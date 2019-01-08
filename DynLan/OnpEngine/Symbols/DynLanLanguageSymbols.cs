using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace DynLan.OnpEngine.Symbols
{
    public class DynLanuageSymbols
    {
        public static readonly char[][] CommentStartSymbol = new char[][] { 
            "#".ToCharArray(), }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] EqualOperator = new char[][] { 
            "=".ToCharArray(), }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] Whitespaces = new char[][] { 
            Environment.NewLine.ToCharArray(), 
            " ".ToCharArray(),
            ((Char)10).ToString().ToCharArray(),
            ((Char)13).ToString().ToCharArray(), }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] NewLineChars = new char[][] { 
            Environment.NewLine.ToCharArray(), 
            ";".ToCharArray(),
            ((Char)10).ToString().ToCharArray(),
            ((Char)13).ToString().ToCharArray(), }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[] WhitespaceChars = new char[] { 
            ' ',
            ((Char)10),
            ((Char)13) };

        public static readonly char[][] Numbers = new char[][] { 
            ".".ToCharArray(), 
            "0".ToCharArray(), 
            "1".ToCharArray(), 
            "2".ToCharArray(), 
            "3".ToCharArray(), 
            "4".ToCharArray(), 
            "5".ToCharArray(), 
            "6".ToCharArray(), 
            "7".ToCharArray(), 
            "8".ToCharArray(), 
            "9".ToCharArray() }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] Separators = new char[][] { 
            ",".ToCharArray()}.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] BracketBegin = new char[][] { 
            "(".ToCharArray() }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] BracketEnd = new char[][] { 
            ")".ToCharArray() }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] IndexerBegin = new char[][] { 
            "[".ToCharArray() }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] IndexerEnd = new char[][] { 
            "]".ToCharArray() }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] Operators = new char[][] { 
            "*".ToCharArray(), 
            "%".ToCharArray(), 
            "@".ToCharArray(), 
            "-".ToCharArray(), 
            ".".ToCharArray(), 
            "+".ToCharArray(), 
            "/".ToCharArray() }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] LogicalOperators = 
            OperatorTypeHelper.op_and.
            Union(OperatorTypeHelper.op_or).
            OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] CompareOperators = new char[][]{
            ">".ToCharArray(),
            ">=".ToCharArray(),
            "<=".ToCharArray(),
            "<".ToCharArray(),
            "!=".ToCharArray(),
            "==".ToCharArray(),
            }.OrderByDescending(i => i.Length).ToArray();

        public static readonly char[][] OperatorsAndPropertyAndBrackets =
            EqualOperator.
            Union(Operators).
            Union(CompareOperators).
            Union(LogicalOperators).
            OrderByDescending(i => i.Length).ToArray();

        ////////////////////////////

        public static readonly char[] MinusChars =
            new char[] { '-' };

        public static readonly char[] PlusChars =
            new char[] { '+' };
    }
}