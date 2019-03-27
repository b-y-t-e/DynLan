

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.OnpEngine.Symbols
{
    public class DynLanuageSymbols
    {
#if !SPACES_FOR_DEPTH

        public static readonly char[][] DepthBegin = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { "{".ToCharArray() }, i => i.Length));

        public static readonly char[][] DepthEnd = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { "}".ToCharArray() }, i => i.Length));
#else
        
        public static readonly char[][] DepthBegin = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { }, i => i.Length));

        public static readonly char[][] DepthEnd = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { }, i => i.Length));

#endif

        public static readonly char[][] Comment1StartSymbol = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { "#".ToCharArray(), },i => i.Length));

        public static readonly char[][] Comment2StartSymbol = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { "/*".ToCharArray(), },i => i.Length));

        public static readonly char[][] Comment2EndSymbol = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { "*/".ToCharArray(), },i => i.Length));

        public static readonly char[][] EqualOperator = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] { "=".ToCharArray(), },i => i.Length));

        public static readonly char[][] Whitespaces = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            Environment.NewLine.ToCharArray(),
            " ".ToCharArray(),
            ((Char)10).ToString().ToCharArray(),
            ((Char)13).ToString().ToCharArray(), }, i => i.Length));

        public static readonly char[][] NewLineChars = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            Environment.NewLine.ToCharArray(),
            ";".ToCharArray(),
            // ":".ToCharArray(),
            ((Char)10).ToString().ToCharArray(),
            ((Char)13).ToString().ToCharArray(),
        }, i => i.Length));

        public static readonly char[] WhitespaceChars = new char[] {
            ' ',
            ((Char)10),
            ((Char)13) };

        public static readonly char[][] Numbers = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
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
            "9".ToCharArray() }, i => i.Length));

        public static readonly char[][] Separators = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            ",".ToCharArray()}, i => i.Length));

        public static readonly char[][] BracketBegin = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            "(".ToCharArray() }, i => i.Length));

        public static readonly char[][] BracketEnd = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            ")".ToCharArray() }, i => i.Length));

        public static readonly char[][] IndexerBegin = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            "[".ToCharArray() }, i => i.Length));

        public static readonly char[][] IndexerEnd = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            "]".ToCharArray() }, i => i.Length));

        public static readonly char[][] Operators = Linq2.ToArray(Linq2.OrderByDescending(
            new char[][] {
            "*".ToCharArray(),
            "%".ToCharArray(),
            "@".ToCharArray(),
            "-".ToCharArray(),
            ".".ToCharArray(),
            "+".ToCharArray(),
            "/".ToCharArray() }, i => i.Length));

        public static readonly char[][] LogicalOperators =
            Linq2.From(OperatorTypeHelper.op_and).
                Union(OperatorTypeHelper.op_or).
                OrderByDescending(i => i.Length).
                ToArray();

        public static readonly char[][] CompareOperators =
            Linq2.From(new char[][] {
                ">".ToCharArray(),
                ">=".ToCharArray(),
                "<=".ToCharArray(),
                "<".ToCharArray(),
                "!=".ToCharArray(),
                "==".ToCharArray(),
            }).
            OrderByDescending(i => i.Length).
            ToArray();

        public static readonly char[][] OperatorsAndPropertyAndBrackets =
            Linq2.From(EqualOperator).
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