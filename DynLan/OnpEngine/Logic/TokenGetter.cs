using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan.Exceptions;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Symbols;
using DynLan.OnpEngine.InternalExtenders;

namespace DynLan.OnpEngine.Logic
{
    public static class TokenGetter
    {
        public static ExpressionTokens GetOptimizedTokens(
            IList<Char> expressionChars,
            ParserSettings ParserSettings)
        {
            if (ParserSettings == null)
                ParserSettings = new ParserSettings();

            ExpressionTokens tokens = new ExpressionTokens(
                TokenGetter.GetTokens(expressionChars));

            ConvertIndexersToFunctions(tokens);
            AddFunctionCallOperator(tokens);
            ConvertMinusOperatorToMinusOne(tokens);
            // RemoveOperatorsOnEnd(tokens);

            if (ParserSettings.Optimize)
                OptimizeTokens(tokens);

            if (ParserSettings.Validate)
                ValidateTokens(tokens);

            //CorrectFunctionCallTokenInfo(tokens);

            return tokens;
        }

        public static IEnumerable<String> GetStringTokens(
            String Expression)
        {
            foreach (ExpressionToken token in GetTokens(Expression))
                yield return token.TokenName;
        }

        //////////////////////////////////////////////////////////////////

        public static IEnumerable<ExpressionToken> GetTokens(
            String Expression)
        {
            return GetTokens(
                Expression.ToCharArray());
        }

        private static IEnumerable<ExpressionToken> GetTokens(
            IList<Char> ExpressionChars)
        {
            ExpressionToken prevToken = null;
            for (var i = 0; i < ExpressionChars.Count; i++)
            {
                ExpressionToken token = TokenSingleGetter.GetNextToken(ExpressionChars, i, prevToken);

                if (token == null)
                    continue;

                if (token.TokenType != TokenType.WHITESPACE)
                    yield return token;

                Int32 finalTokenLenght = token.GetFinalTokenLength();
                if (finalTokenLenght > 0)
                {
                    i += finalTokenLenght;
                    i -= 1;
                }

                prevToken = token;
            }
        }

        //////////////////////////////////////////////////////////////////

        private static void ConvertMinusOperatorToMinusOne(ExpressionTokens Tokens)
        {
            for (var i = 0; i < Tokens.Count; i++)
            {
                ExpressionToken token = Tokens[i];
                ExpressionToken nextToken = null;
                ExpressionToken prevToken = null;

                nextToken = (i + 1 < Tokens.Count ? Tokens[i + 1] : null);
                prevToken = (i - 1 >= 0 ? Tokens[i - 1] : null);

                // zamiana minusa na -1
                if (token.TokenType == TokenType.OPERATOR)
                {
                    if (StringHelper.StartsWith(token.TokenChars, OperatorTypeHelper.op_minus, false))
                    {
                        //if (!(nextToken.TokenType == TokenType.VALUE && StringHelper.IsDateTime(nextToken.TokenChars)))
                        {
                            Tokens.RemoveAt(i);
                            Int32 index = i;

                            if (prevToken != null && (
                                /*prevToken.TokenType == TokenType.FUNCTION_CALL ||*/
                                prevToken.TokenType == TokenType.BRACKET_END ||
                                prevToken.TokenType == TokenType.VALUE ||
                                prevToken.TokenType == TokenType.PROPERTY_NAME ||
                                prevToken.TokenType == TokenType.VARIABLE))
                            {
                                ExpressionToken operator1 = new ExpressionToken(OperatorTypeHelper.op_plus, TokenType.OPERATOR);
                                Tokens.Insert(index++, operator1);
                            }

                            ExpressionToken value1 = new ExpressionToken(OperatorTypeHelper.number_minus_one, TokenType.VALUE);
                            Tokens.Insert(index++, value1);

                            if (nextToken != null)
                            {
                                ExpressionToken operator2 = new ExpressionToken(OperatorTypeHelper.op_multiply, TokenType.OPERATOR);
                                Tokens.Insert(index++, operator2);
                            }
                        }
                    }
                }
            }
        }

        private static void ConvertIndexersToFunctions(ExpressionTokens Tokens)
        {
            for (var i = 0; i < Tokens.Count; i++)
            {
                ExpressionToken token = Tokens[i];

                if (token.TokenType == TokenType.INDEXER_BEGIN)
                {
                    token.Set(".", false);
                    token.TokenType = TokenType.OPERATOR;

                    Tokens.Insert(
                        i + 1,
                        new ExpressionToken(ExtenderCollectionGetter.NameChars, TokenType.VARIABLE));

                    Tokens.Insert(
                        i + 2,
                        new ExpressionToken("@", TokenType.OPERATOR));

                    Tokens.Insert(
                        i + 3,
                        new ExpressionToken("(", TokenType.BRACKET_BEGIN));
                }
                else if (token.TokenType == TokenType.INDEXER_END)
                {
                    token.Set(")", false);
                    token.TokenType = TokenType.BRACKET_END;
                }
            }
        }

        private static void AddFunctionCallOperator(ExpressionTokens Tokens)
        {
            for (var i = 0; i < Tokens.Count; i++)
            {
                ExpressionToken token = Tokens[i];
                ExpressionToken nextToken = null;
                ExpressionToken prevToken = null;

                nextToken = (i + 1 < Tokens.Count ? Tokens[i + 1] : null);
                prevToken = (i - 1 >= 0 ? Tokens[i - 1] : null);

                // zamiana minusa na -1
                if (token.TokenType == TokenType.BRACKET_BEGIN &&
                    prevToken != null &&
                    prevToken.TokenType != TokenType.SEPARATOR &&
                    prevToken.TokenType != TokenType.OPERATOR &&
                    prevToken.TokenType != TokenType.EQUAL_OPERATOR &&
                    prevToken.TokenType != TokenType.BRACKET_BEGIN)
                {
                    ExpressionToken value1 = new ExpressionToken(OperatorTypeHelper.op_methodcall, TokenType.OPERATOR);
                    Tokens.Insert(i, value1);
                }
            }
        }

        /*private static void CheckOperatorsOnEnd(
            ExpressionTokens tokens)
        {
            // usunięcie operatorów na końcu
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                ExpressionToken currentToken = tokens[i];
                if (currentToken.TokenType == TokenType.OPERATOR)
                {
                    tokens.RemoveAt(i);
                }
                else
                {
                    break;
                }
            }
        }*/

        private static void RemoveOperatorsOnEnd(
            ExpressionTokens tokens)
        {
            // usunięcie operatorów na końcu
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                ExpressionToken currentToken = tokens[i];
                if (currentToken.TokenType == TokenType.OPERATOR)
                {
                    tokens.RemoveAt(i);
                }
                else
                {
                    break;
                }
            }
        }

        private static void OptimizeTokens(
            ExpressionTokens tokens)
        {
            // usuwanie pustych nawiasów
            /*for (int i = 0; i < tokens.Count - 1; i++)
            {
                ExpressionToken currentToken = tokens[i];
                ExpressionToken nextToken = tokens[i + 1];

                // usuwanie pustych nawiasów
                if (currentToken.TokenType == TokenType.BRACKET_BEGIN &&
                    nextToken.TokenType == TokenType.BRACKET_END)
                {
                    tokens.Insert(i + 1, new ExpressionToken(OperatorTypeHelper.number_zero, TokenType.VALUE));
                    i--;
                }
            }*/

            // zamiana dwóch minusów na plus
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                ExpressionToken currentToken = tokens[i];
                ExpressionToken nextToken = tokens[i + 1];

                Boolean isMinus =
                    currentToken.TokenType == TokenType.OPERATOR &&
                    StringHelper.StartsWith(currentToken.TokenChars, DynLanuageSymbols.MinusChars, false);

                Boolean isNextMinus =
                    nextToken.TokenType == TokenType.OPERATOR &&
                    StringHelper.StartsWith(nextToken.TokenChars, DynLanuageSymbols.MinusChars, false);

                // zamiana dwóch minusów na plus
                if (isMinus && isNextMinus)
                {
                    currentToken.Set(DynLanuageSymbols.PlusChars);
                    tokens.RemoveAt(i + 1);
                    i--;
                }
            }

            // zamiana dwóch plusów na jeden plus
            /*for (int i = 0; i < tokens.Count - 1; i++)
            {
                ExpressionToken currentToken = tokens[i];
                ExpressionToken nextToken = tokens[i + 1];

                Boolean isPlus =
                    currentToken.TokenType == TokenType.OPERATOR &&
                    StringHelper.StartsWith(currentToken.TokenChars, DynLanuageSymbols.PlusChars, false);

                Boolean isNextPlus =
                    nextToken.TokenType == TokenType.OPERATOR &&
                    StringHelper.StartsWith(nextToken.TokenChars, DynLanuageSymbols.PlusChars, false);

                // zamiana dwóch plusów na jeden plus
                if (isPlus && isNextPlus)
                {
                    tokens.RemoveAt(i + 1);
                    i--;
                }
            }*/

        }

        private static void ValidateTokens(
            ExpressionTokens tokens)
        {
            // usunięcie operatorów na końcu
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                ExpressionToken currentToken = tokens[i];
                if (currentToken.TokenType == TokenType.OPERATOR)
                {
                    // tokens.RemoveAt(i);
                    throw new DynLanIncorrectExpressionFormatException("Invalid operator " + currentToken.TokenName + " at the end of expression!");
                }
                else
                {
                    break;
                }
            }

            Int32 equalOperators = 0;
            Int32 beginBrackets = 0;
            Int32 endBrackets = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                ExpressionToken prevToken = i > 0 ? tokens[i - 1] : null;
                ExpressionToken currentToken = tokens[i];
                ExpressionToken nextToken = i < tokens.Count - 1 ? tokens[i + 1] : null;

                Boolean isStringOrNumber =
                    /* currentToken.TokenType == TokenType.FUNCTION_CALL ||*/
                    currentToken.TokenType == TokenType.VARIABLE ||
                    currentToken.TokenType == TokenType.VALUE ||
                    currentToken.TokenType == TokenType.PROPERTY_NAME;

                Boolean isOperator =
                    currentToken.TokenType == TokenType.OPERATOR;

                // przecinki moga wystepowac tylko w nawiasach i przy wolaniu funkcji
                if (currentToken.TokenType == TokenType.SEPARATOR)
                {
                    Boolean functionCallFound = false;
                    Int32 innerBrackets = 0;
                    for (var j = i - 1; j >= 0; j--)
                    {
                        ExpressionToken token = tokens[j];
                        if (token.TokenType == TokenType.BRACKET_END)
                        {
                            innerBrackets++;
                        }
                        else if (token.TokenType == TokenType.BRACKET_BEGIN)
                        {
                            innerBrackets--;
                        }
                        else if (token.TokenType == TokenType.OPERATOR &&
                            OnpOnpTokenHelper.IsFunctionOperatorToken(token))
                        {
                            if (innerBrackets == -1)
                            {
                                functionCallFound = true;
                                break;
                            }
                        }
                    }

                    if (!functionCallFound)
                    {
                        throw new DynLanIncorrectExpressionFormatException("Invalid function call");
                    }
                }

                if (currentToken.TokenType == TokenType.EQUAL_OPERATOR)
                {
                    equalOperators++;
                }

                if (nextToken != null)
                {
                    if (isOperator &&
                        (nextToken.TokenType == TokenType.OPERATOR ||
                        nextToken.TokenType == TokenType.BRACKET_END))
                    {
                        throw new DynLanIncorrectExpressionFormatException("Incorrect symbol '" + nextToken.TokenName + "' after operator '" + currentToken.TokenName + "'");
                    }
                    else if (currentToken.TokenType == TokenType.BRACKET_BEGIN &&
                        !OnpOnpTokenHelper.IsFunctionOperatorToken(prevToken) &&
                        (nextToken.TokenType == TokenType.OPERATOR ||
                        nextToken.TokenType == TokenType.BRACKET_END ||
                        nextToken.TokenType == TokenType.EQUAL_OPERATOR))
                    {
                        throw new DynLanIncorrectExpressionFormatException("Incorrect symbol '" + nextToken.TokenName + "' after operator '('");
                    }
                    else if (
                        currentToken.TokenType == TokenType.BRACKET_END &&
                        nextToken.TokenType != TokenType.OPERATOR &&
                        nextToken.TokenType != TokenType.EQUAL_OPERATOR &&
                        nextToken.TokenType != TokenType.BRACKET_END &&
                        nextToken.TokenType != TokenType.SEPARATOR)
                    {
                        throw new DynLanIncorrectExpressionFormatException("Incorrect symbol '" + nextToken.TokenName + "' after operator ')'");
                    }
                }

                if (currentToken.TokenType == TokenType.BRACKET_BEGIN)
                {
                    beginBrackets++;
                }
                else if (currentToken.TokenType == TokenType.BRACKET_END)
                {
                    endBrackets++;
                }
            }

            if (beginBrackets != endBrackets)
            {
                throw new DynLanIncorrectExpressionFormatException("Number of opening brackets is not equal with number of ending brackets");
            }
        }
    }
}