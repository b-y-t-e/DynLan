using DynLan.OnpEngine.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace DynLan.OnpEngine.Symbols
{
    public enum OperatorType
    {
        PLUS,
        SUBTRACT,
        MULTIPLY,
        PROPERTY,
        //METHODCALL,
        DIVIDE,

        GREATER,
        SMALLER,
        GREATER_OR_EQUAL,
        SMALLER_OR_EQUAL,
        EQUAL,
        NOT_EQUAL,

        AND,
        OR,

        INVALID
    }

    public static class OperatorTypeHelper
    {
        public static readonly char[] op_bracket_begin = new[] { '(' };

        public static readonly char[] op_bracket_end = new[] { ')' };

        ////////////////////////////////////////////////////

        public static readonly char[] op_plus = new[] { '+' };

        public static readonly char[] op_multiply = new[] { '*' };

        public static readonly char[] op_methodcall = new[] { '@' };

        public static readonly char[] op_divide = new[] { '/' };

        public static readonly char[] op_minus = new[] { '-' };

        public static readonly char[] op_property = new[] { '.' };

        public static readonly char[] op_greater_equal = new[] { '>', '=' };

        public static readonly char[] op_greater = new[] { '>' };

        public static readonly char[] op_smaller_equal = new[] { '<', '=' };

        public static readonly char[] op_smaller = new[] { '<' };

        public static readonly char[] op_equal = new[] { '=', '=' };

        public static readonly char[] op_not_equal = new[] { '!', '=' };

        ////////////////////////////////////////////////////

        public static readonly char[][] op_and = new[] { "&&".ToCharArray(), "and".ToCharArray() };

        public static readonly char[][] op_or = new[] { "||".ToCharArray(), "or".ToCharArray() };

        ////////////////////////////////////////////////////

        public static readonly char[] number_zero = new[] { '0' };

        public static readonly char[] number_one = new[] { '1' };

        public static readonly char[] number_minus_one = new[] { '-', '1' };

        public static readonly char[] symbol_true = "true".ToCharArray();

        public static readonly char[] symbol_false = "false".ToCharArray();

        public static readonly char[] symbol_null = "null".ToCharArray();

        public static readonly char[] symbol_undefined = "undefined".ToCharArray();

        public static readonly char[] symbol_new_line = new[] { 'n', 'l' };

        ////////////////////////////////////////////////////

        public static OperatorType GetOperationType(IList<Char> Token)
        {
            if (StringHelper.SequenceEqual(Token, op_plus))
                return OperatorType.PLUS;
            else if (StringHelper.SequenceEqual(Token, op_multiply))
                return OperatorType.MULTIPLY;
            else if (StringHelper.SequenceEqual(Token, op_property))
                return OperatorType.PROPERTY;
            else if (StringHelper.SequenceEqual(Token, op_divide))
                return OperatorType.DIVIDE;
            else if (StringHelper.SequenceEqual(Token, op_minus))
                return OperatorType.SUBTRACT;
            else if (StringHelper.SequenceEqual(Token, op_greater))
                return OperatorType.GREATER;
            else if (StringHelper.SequenceEqual(Token, op_greater_equal))
                return OperatorType.GREATER_OR_EQUAL;
            else if (StringHelper.SequenceEqual(Token, op_smaller_equal))
                return OperatorType.SMALLER_OR_EQUAL;
            else if (StringHelper.SequenceEqual(Token, op_smaller))
                return OperatorType.SMALLER;
            else if (StringHelper.SequenceEqual(Token, op_equal))
                return OperatorType.EQUAL;
            else if (StringHelper.SequenceEqual(Token, op_not_equal))
                return OperatorType.NOT_EQUAL;
            
            foreach (char[] seq in op_and)
                if (StringHelper.SequenceEqualInsensitive(Token, seq))
                    return OperatorType.AND;

            foreach (char[] seq in op_or)
                if (StringHelper.SequenceEqualInsensitive(Token, seq))
                    return OperatorType.OR;

            return OperatorType.INVALID;
        }
    }
}