using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.OnpEngine.Logic
{
    public static class TokenSingleGetter
    {
        /// <summary>
        /// Zwraca kolejny token
        /// </summary>
        public static ExpressionToken GetNextToken(IList<Char> Chars, Int32 StartIndex, ExpressionToken PrevToken)
        {
            // szukanie operatórw porównania
            Char[] foundCompareOperator = StringHelper.StartsWith(Chars, DynLanuageSymbols.CompareOperators, StartIndex, false);
            if (foundCompareOperator != null)
            {
                return new ExpressionToken(foundCompareOperator, TokenType.OPERATOR)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundCompareOperator.Length - 1
                };
            }

            // szukanie nawiasu START
            Char[] foundBracketBegin = StringHelper.StartsWith(Chars, DynLanuageSymbols.BracketBegin, StartIndex, false);
            if (foundBracketBegin != null)
            {
                return new ExpressionToken(foundBracketBegin, TokenType.BRACKET_BEGIN)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundBracketBegin.Length - 1
                };
            }

            // szukanie nawiasu END
            Char[] foundBracketEnd = StringHelper.StartsWith(Chars, DynLanuageSymbols.BracketEnd, StartIndex, false);
            if (foundBracketEnd != null)
            {
                return new ExpressionToken(foundBracketEnd, TokenType.BRACKET_END)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundBracketEnd.Length - 1
                };
            }

            // szukanie indexera START
            Char[] foundIndexerBegin = StringHelper.StartsWith(Chars, DynLanuageSymbols.IndexerBegin, StartIndex, false);
            if (foundIndexerBegin != null)
            {
                return new ExpressionToken(foundIndexerBegin, TokenType.INDEXER_BEGIN)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundIndexerBegin.Length - 1
                };
            }

            // szukanie indexera END
            Char[] foundIndexerEnd = StringHelper.StartsWith(Chars, DynLanuageSymbols.IndexerEnd, StartIndex, false);
            if (foundIndexerEnd != null)
            {
                return new ExpressionToken(foundIndexerEnd, TokenType.INDEXER_END)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundIndexerEnd.Length - 1
                };
            }

            // szukanie operatora równości
            Char[] foundEqualOperator = StringHelper.StartsWith(Chars, DynLanuageSymbols.EqualOperator, StartIndex, false);
            if (foundEqualOperator != null)
            {
                return new ExpressionToken(foundEqualOperator, TokenType.EQUAL_OPERATOR)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundEqualOperator.Length - 1
                };
            }

            // szukanie operatora
            Char[] foundOperator = StringHelper.StartsWith(Chars, DynLanuageSymbols.Operators, StartIndex, false);
            if (foundOperator != null)
            {
                return new ExpressionToken(foundOperator, TokenType.OPERATOR)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundOperator.Length - 1
                };
            }

            // szukanie operatorów logicznych
            Char[] foundLogicalOperator = StringHelper.StartsWith(Chars, DynLanuageSymbols.LogicalOperators, StartIndex, true);
            if (foundLogicalOperator != null)
            {
                Char? prevChar = StartIndex > 0 ? (Char?)Chars[StartIndex - 1] : null;
                Char? nextChar = StartIndex + foundLogicalOperator.Length < Chars.Count ? (Char?)Chars[StartIndex + foundLogicalOperator.Length] : null;

                if (!(prevChar != null && Char.IsLetterOrDigit(prevChar.Value) && Char.IsLetterOrDigit(foundLogicalOperator[0])) &&
                    !(nextChar != null && Char.IsLetterOrDigit(nextChar.Value) && Char.IsLetterOrDigit(foundLogicalOperator[foundLogicalOperator.Length - 1])))
                {
                    return new ExpressionToken(foundLogicalOperator, TokenType.OPERATOR)
                    {
                        StartIndex = StartIndex,
                        EndIndex = StartIndex + foundLogicalOperator.Length - 1
                    };

                }
                else
                {
                    prevChar = prevChar;
                }
            }

            // szukanie białych znaków
            Char[] foundWhitespaces = StringHelper.StartsWith(Chars, DynLanuageSymbols.Whitespaces, StartIndex, false);
            if (foundWhitespaces != null)
            {
                return new ExpressionToken(foundWhitespaces, TokenType.WHITESPACE)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundWhitespaces.Length - 1
                };
            }

            // szukanie przecinka                                    
            Char[] foundSeparator = StringHelper.StartsWith(Chars, DynLanuageSymbols.Separators, StartIndex, false);
            if (foundSeparator != null)
            {
                return new ExpressionToken(foundSeparator, TokenType.SEPARATOR)
                {
                    StartIndex = StartIndex,
                    EndIndex = StartIndex + foundSeparator.Length - 1
                };
            }

            TokenType tokenType = TokenType.VARIABLE;
            Int32 nextIndex = -1;
            List<Char> chars = new List<Char>();

            OnpOnpStringFindResult firstNext = StringHelper.FirstNextIndex(
                Chars, StartIndex,
                new[] { DynLanuageSymbols.IndexerBegin, DynLanuageSymbols.IndexerEnd, DynLanuageSymbols.BracketBegin, DynLanuageSymbols.BracketEnd, DynLanuageSymbols.OperatorsAndPropertyAndBrackets, DynLanuageSymbols.Separators },
                true);

            if (firstNext == null || firstNext.Index < 0)
            {
                for (Int32 j = StartIndex; j < Chars.Count; j++)
                    chars.Add(Chars[j]);
            }
            else
            {
                int len = firstNext.Index;
                for (Int32 j = StartIndex; j < len; j++)
                    chars.Add(Chars[j]);
                nextIndex = firstNext.Index;
            }
            if (nextIndex < 0)
                nextIndex = Chars.Count;

            chars.Trim();

            if (chars.Count != 0)
            {
                if (StringHelper.IsValue(chars))
                    tokenType = TokenType.VALUE;

                //if (tokenType == TokenType.VARIABLE)
                //    for (var i = 0; i < chars.Count; i++)
                //        chars[i] = Char.ToUpper(chars[i]);

                return
                    new ExpressionToken(chars, tokenType)
                    {
                        TokenLength = (nextIndex - StartIndex),
                        StartIndex = StartIndex,
                        EndIndex = StartIndex + chars.Count - 1
                    };
            }

            return null;
        }

        /*
        /// <summary>
        /// Zwraca kolejny token
        /// </summary>
        public static IEnumerable<ExpressionToken> GetNextToken(IList<Char> Chars, Int32 StartIndex, ExpressionToken PrevToken)
        {
            // szukanie operatórw porównania
            Char[] foundCompareOperator = StringHelper.StrEquals(Chars, OnpSymbols.CompareOperators, StartIndex);
            if (foundCompareOperator != null)
            {
                return new ExpressionToken(foundCompareOperator, TokenType.OPERATOR);
                yield break;
            }

            // szukanie nawiasu START
            Char[] foundBracketBegin = StringHelper.StrEquals(Chars, OnpSymbols.BracketBegin, StartIndex);
            if (foundBracketBegin != null)
            {
                return new ExpressionToken(foundBracketBegin, TokenType.BRACKET_BEGIN);
                yield break;
            }

            // szukanie nawiasu END
            Char[] foundBracketEnd = StringHelper.StrEquals(Chars, OnpSymbols.BracketEnd, StartIndex);
            if (foundBracketEnd != null)
            {
                return new ExpressionToken(foundBracketEnd, TokenType.BRACKET_END);
                yield break;
            }

            // szukanie indexera START
            Char[] foundIndexerBegin = StringHelper.StrEquals(Chars, OnpSymbols.IndexerBegin, StartIndex);
            if (foundIndexerBegin != null)
            {
                return new ExpressionToken(foundIndexerBegin, TokenType.INDEXER_BEGIN);
                yield break;
            }

            // szukanie indexera END
            Char[] foundIndexerEnd = StringHelper.StrEquals(Chars, OnpSymbols.IndexerEnd, StartIndex);
            if (foundIndexerEnd != null)
            {
                return new ExpressionToken(foundIndexerEnd, TokenType.INDEXER_END);
                yield break;
            }

            // szukanie operatora równości
            Char[] foundEqualOperator = StringHelper.StrEquals(Chars, OnpSymbols.EqualOperator, StartIndex);
            if (foundEqualOperator != null)
            {
                return new ExpressionToken(foundEqualOperator, TokenType.EQUAL_OPERATOR);
                yield break;
            }

            // szukanie operatora
            Char[] foundOperator = StringHelper.StrEquals(Chars, OnpSymbols.Operators, StartIndex);
            if (foundOperator != null)
            {
                return new ExpressionToken(foundOperator, TokenType.OPERATOR);
                yield break;
            }

            // szukanie operatorów logicznych
            Char[] foundLogicalOperator = StringHelper.StrEquals(Chars, OnpSymbols.LogicalOperators, StartIndex);
            if (foundLogicalOperator != null)
            {
                return new ExpressionToken(foundLogicalOperator, TokenType.OPERATOR);
                yield break;
            }

            // szukanie białych znaków
            Char[] foundWhitespaces = StringHelper.StrEquals(Chars, OnpSymbols.Whitespaces, StartIndex);
            if (foundWhitespaces != null)
            {
                return new ExpressionToken(foundWhitespaces, TokenType.WHITESPACE);
                yield break;
            }

            // szukanie przecinka                                    
            Char[] foundSeparator = StringHelper.StrEquals(Chars, OnpSymbols.Separators, StartIndex);
            if (foundSeparator != null)
            {
                return new ExpressionToken(foundSeparator, TokenType.SEPARATOR);
                yield break;
            }

            TokenType tokenType = TokenType.VARIABLE;
            Int32 nextIndex = -1;
            Int32 countBracketIn = 0;
            Int32 countBracketOut = 0;
            Int32 firstBracketIndex = -1;
            Boolean containsBrackets = false;
            List<Char> chars = new List<Char>();

            OnpOnpStringFindResult firstNext = StringHelper.FirstNextIndex(
                Chars, StartIndex,
                new[] { OnpSymbols.IndexerBegin, OnpSymbols.IndexerEnd, OnpSymbols.BracketBegin, OnpSymbols.BracketEnd, OnpSymbols.OperatorsAndPropertyAndBrackets, OnpSymbols.Separators });

            if (firstNext == null || firstNext.Index < 0)
            {
                for (Int32 j = StartIndex; j < Chars.Count; j++)
                    chars.Add(Chars[j]);
            }
            else
            {
                int len = firstNext.Index; 
                for (Int32 j = StartIndex; j < len; j++)
                    chars.Add(Chars[j]);
                nextIndex = firstNext.Index;
            }
            if (nextIndex < 0)
                nextIndex = Chars.Count;

            chars.Trim();

            if (chars.Count != 0)
            {
                if (StringHelper.IsValue(chars))
                    tokenType = TokenType.VALUE;

                if (tokenType == TokenType.VARIABLE)
                    for (var i = 0; i < chars.Count; i++)
                        chars[i] = Char.ToUpper(chars[i]);

                yield return
                    new ExpressionToken(chars, tokenType)
                    {
                        TokenLength = (nextIndex - StartIndex)
                    };
            }

            yield break;
        }
        */
    }
}