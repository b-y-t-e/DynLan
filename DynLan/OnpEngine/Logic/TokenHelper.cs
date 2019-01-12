using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.OnpEngine.Logic
{
    public static class OnpOnpTokenHelper
    {
        public static bool IsPropertyOperatorToken(ExpressionToken Token)
        {
            return Token != null &&
                   Token.TokenType == TokenType.OPERATOR &&
                   Token.TokenChars.Count == OperatorTypeHelper.op_property.Length &&
                   StringHelper.StartsWith(Token.TokenChars, OperatorTypeHelper.op_property, false);
        }

        public static bool IsFunctionOperatorToken(ExpressionToken Token)
        {
            return Token != null &&
                   Token.TokenType == TokenType.OPERATOR &&
                   Token.TokenChars.Count == OperatorTypeHelper.op_methodcall.Length &&
                   StringHelper.StartsWith(Token.TokenChars, OperatorTypeHelper.op_methodcall, false);
        }

        //////////////////////////////////////////////////////////////////////////////////////////

        public static Int32 GetPriority(ExpressionToken ExpressionToken)
        {
            return GetPriority(ExpressionToken.TokenType, ExpressionToken.TokenChars);
        }

        public static Int32 GetPriority(TokenType TokenType, IList<Char> TokenChars)
        {
            if (TokenType == TokenType.OPERATOR ||
                TokenType == TokenType.BRACKET_BEGIN ||
                TokenType == TokenType.BRACKET_END)
            {
                if (StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_bracket_begin, false))
                {
                    return -10;
                }
                else if (StringHelper.SequenceEqualInsensitive(TokenChars, OperatorTypeHelper.op_and) ||
                       StringHelper.SequenceEqualInsensitive(TokenChars, OperatorTypeHelper.op_or))
                {
                    return -2;
                }
                else if (StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_greater, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_smaller, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_greater_equal, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_smaller_equal, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_equal, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_not_equal, false))
                {
                    return -1;
                }
                else if (
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_plus, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_minus, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_bracket_end, false))
                {
                    return 1;
                }
                else if (
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_multiply, false) ||
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_divide, false))
                {
                    return 2;
                }
                else if (
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_property, false))
                {
                    return 100;
                }
                else if (
                    StringHelper.StartsWith(TokenChars, OperatorTypeHelper.op_methodcall, false  ))
                {
                    return 200;
                }
                /*if (StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_and) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_or))
                {
                    return -2;
                }
                else if (StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_greater) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_smaller) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_greater_equal) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_smaller_equal) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_equal) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_not_equal))
                {
                    return -1;
                }
                else if (StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_bracket_begin))
                {
                    return 0;
                }
                else if (
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_plus) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_minus) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_bracket_end))
                {
                    return 1;
                }
                else if (
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_multiply) ||
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_divide))
                {
                    return 2;
                }
                else if (
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_property))
                {
                    return 100;
                }
                else if (
                    StringHelper.StrEquals(TokenChars, OperatorTypeHelper.op_methodcall))
                {
                    return 200;
                }*/
            }
            return Int32.MaxValue;
        }
    }
}