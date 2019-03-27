using DynLan.OnpEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.OnpEngine.Logic
{
    public class TokenizerQueue
    {
        public void PrepareQueueExpressions(
            Expression onpExpression,
            ParserSettings ParserSettings,
            ExpressionGroup ExpressionGroup)
        {
            if (onpExpression.IsOnpExecution)
            {
                PrepareQueueExpressions(
                    onpExpression.Tokens,
                    ParserSettings,
                    ExpressionGroup);
            }
            else
            {
                CorrectQueueExpression(
                    onpExpression,
                    ParserSettings,
                    ExpressionGroup);
            }
        }

        /// <summary>
        /// Odszukanie ciągów QUEUE w wyrażeniu
        /// </summary>
        public void PrepareQueueExpressions(
            IList<ExpressionToken> Tokens,
            ParserSettings ParserSettings,
            ExpressionGroup ExpressionGroup)
        {
            ExpressionTokens outTokens = new ExpressionTokens();
            ExpressionTokens queueTokens = null;
            Boolean queueStarted = false;
            Int32 startIndex = -1;
            Int32 endIndex = -1;

            for (var i = 0; i < Tokens.Count; i++)
            {
                ExpressionToken token = Tokens[i];

                // rozpoczecie sekwencji queue
                if (token != null &&
                    queueStarted == false &&
                    (OnpOnpTokenHelper.IsPropertyOperatorToken(token) || OnpOnpTokenHelper.IsFunctionOperatorToken(token)))
                {
                    ExpressionTokens sequence = new ExpressionTokens(
                        GetNextTokensOnSameLevel(Tokens, i));

                    ExpressionTokens prevSequence = new ExpressionTokens(
                        GetPrevTokensOnSameLevel(Tokens, i - 1));

                    if (queueTokens == null)
                        queueTokens = new ExpressionTokens();

                    if (prevSequence == null)
                        throw new FormatException();

                    queueTokens.AddRange(prevSequence);
                    queueTokens.AddRange(sequence);

                    queueStarted = true;
                    startIndex = i - prevSequence.Count;

                    i += sequence.Count - 1;
                }
                // zakończenie sekwencji queue
                else if (
                    token != null &&
                    queueStarted == true &&
                    (
                        token.TokenType == TokenType.BRACKET_END ||
                        token.TokenType == TokenType.OPERATOR &&
                        !OnpOnpTokenHelper.IsPropertyOperatorToken(token) &&
                        !OnpOnpTokenHelper.IsFunctionOperatorToken(token)
                    ))
                {
                    endIndex = i - 1;

                    // zastapienie ciagu tokenków zmienną i podpiecie nowego wyrazenia do zmiennej 
                    if (queueTokens != null && queueTokens.Count > 0)
                    {
                        ExpressionToken newToken = CreateQueueExpression(
                            queueTokens,
                            ParserSettings,
                            ExpressionGroup);

                        for (var j = endIndex; j >= startIndex; j--)
                        {
                            Tokens.RemoveAt(j);
                            i--;
                        }
                        Tokens.Insert(startIndex, newToken);
                    }

                    queueTokens = null;
                    endIndex = -1;
                    startIndex = -1;
                    queueStarted = false;
                }
                // kontynuacja sekwencji queue
                else if (queueStarted == true)
                {
                    ExpressionTokens sequence = new ExpressionTokens(
                        GetNextTokensOnSameLevel(Tokens, i));

                    queueTokens.AddRange(
                        sequence);

                    i += sequence.Count - 1;
                }
            }

            // zastapienie ciagu tokenków zmienną i podpiecie nowego wyrazenia do zmiennej 
            if (queueTokens != null && queueTokens.Count > 0)
            {
                endIndex = Tokens.Count - 1;

                ExpressionToken newToken = CreateQueueExpression(
                    queueTokens,
                    ParserSettings,
                    ExpressionGroup);

                for (var j = endIndex; j >= startIndex; j--)
                    Tokens.RemoveAt(j);

                Tokens.Insert(startIndex, newToken);
            }

            queueTokens = null;
            endIndex = -1;
            startIndex = -1;
        }

        /// <summary>
        /// zastapienie ciagu tokenków zmienną i podpiecie nowego wyrazenia do zmiennej 
        /// </summary>
        public ExpressionToken CreateQueueExpression(
            ExpressionTokens queueTokens,
            ParserSettings ParserSettings,
            ExpressionGroup ExpressionGroup)
        {
            if (queueTokens != null && queueTokens.Count > 0)
            {
                Expression newExpression = new Expression();
                newExpression.Tokens = queueTokens;
                newExpression.IsOnpExecution = false;

                ExpressionGroup.Expressions[newExpression.ID] =
                    newExpression;

                CorrectQueueExpression(
                    newExpression,
                    ParserSettings,
                    ExpressionGroup);

                return new ExpressionToken(
                    newExpression.ID.ToCharArray(),
                    TokenType.VARIABLE);
            }
            return null;
        }

        /// <summary>
        /// zastapienie ciagu tokenków zmienną i podpiecie nowego wyrazenia do zmiennej 
        /// </summary>
        public void CorrectQueueExpression(
            Expression Expression,
            ParserSettings ParserSettings,
            ExpressionGroup ExpressionGroup)
        {
            if (Expression == null)
                return;

            if (Expression.Tokens == null || Expression.Tokens.Count <= 0)
                return;

            for (var i = 0; i < Expression.Tokens.Count; i++)
            {
                ExpressionToken token = Expression.Tokens[i];
                ExpressionToken token_next = i + 1 < Expression.Tokens.Count ? Expression.Tokens[i + 1] : null;
                ExpressionToken token_prev = i - 1 >= 0 ? Expression.Tokens[i - 1] : null;

                ExpressionTokens functionCallTokens = new ExpressionTokens(
                    GetNextTokensOnSameLevel(Expression.Tokens, i));

                if (functionCallTokens.Count > 1)
                {
                    // generowanie expressions dla wnętrz funkcji
                    IList<ExpressionTokens> functionParameters = Linq2.ToList(SplitTokensIntoFunctionParameters(functionCallTokens));
                    foreach (ExpressionTokens functionParameter in functionParameters)
                    {
                        // generowanie expression z wyrażenia z parametru
                        if (functionParameter.Count > 1)
                        {
                            Expression functionExpression = new Expression();
                            functionExpression.Tokens = new ExpressionTokens(functionParameter);
                            functionExpression.IsOnpExecution = true;

                            ExpressionGroup.Expressions[functionExpression.ID] =
                                functionExpression;

                            new Tokenizer().PrepareExpressions(
                                functionExpression,
                                ParserSettings,
                                ExpressionGroup);

                            ExpressionToken functionParameterToken = new ExpressionToken(
                                functionExpression.ID.ToCharArray(),
                                TokenType.VARIABLE);

                            Int32 index = Expression.Tokens.
                                RemoveSequence(functionParameter);

                            Expression.Tokens.Insert(
                                index,
                                functionParameterToken);

                            i = index;
                        }
                        // gdy pojedyncze wyrażenie w fukncji
                        else
                        {
                            Int32 index = Expression.Tokens.
                                IndexOfSequence(functionParameter);

                            i = index;
                        }
                    }

                    // dla operatora @ ustalenie liczby parametrów
                    if (token_prev != null &&
                        token_prev.TokenType == TokenType.OPERATOR &&
                        (OnpOnpTokenHelper.IsFunctionOperatorToken(token_prev)))
                    {
                        token_prev.TokenData = new OnpTokenData();
                        token_prev.TokenData.FunctionParametersCount = functionParameters.Count;
                    }
                }
                else
                {
                    // zamiana typu zmiennej na property_name jeśli nie jest to pierwsza zmienna
                    if (i > 0 && (token.TokenType == TokenType.VARIABLE || token.TokenType == TokenType.VALUE))
                    {
                        if (token_next == null || !OnpOnpTokenHelper.IsFunctionOperatorToken(token_next))
                            token.TokenType = TokenType.PROPERTY_NAME;
                    }
                    // usunięcie operatorów typu 'get property' ('.')
                    /*else if (OnpOnpTokenHelper.IsPropertyOperatorToken(token) )
                    {
                        queueTokens.RemoveAt(i);
                        i--;
                    }*/
                }
            }
        }

        ////////////////////////////////////////////////////

        public IEnumerable<ExpressionTokens> SplitTokensIntoFunctionParameters(
            IList<ExpressionToken> Tokens)
        {
            if (Tokens.Count < 2 ||
                Tokens[0].TokenType != TokenType.BRACKET_BEGIN ||
                Tokens[Tokens.Count - 1].TokenType != TokenType.BRACKET_END)
            {
                yield return new ExpressionTokens(Tokens);
                yield break;
            }

            Int32 startIndex = 1;
            Int32 maxIndex = Tokens.Count - 1;
            Int32 bracketCount = 0;
            ExpressionTokens result = new ExpressionTokens();

            for (var i = startIndex; i < maxIndex; i++)
            {
                ExpressionToken token = Tokens[i];

                if (token.TokenType == TokenType.BRACKET_BEGIN)
                    bracketCount++;

                else if (bracketCount > 0 && token.TokenType == TokenType.BRACKET_END)
                    bracketCount--;

                if (bracketCount == 0 &&
                    token.TokenType == TokenType.SEPARATOR)
                {
                    if (result.Count > 0)
                    {
                        yield return result;
                        result = new ExpressionTokens();
                    }
                }
                else
                {
                    result.Add(token);
                }
            }

            if (result.Count > 0)
            {
                yield return result;
            }
        }

        public IEnumerable<ExpressionToken> GetNextTokensOnSameLevel(
            IList<ExpressionToken> Tokens,
            Int32 StartIndex)
        {
            Int32 bracketCount = 0;
            for (var i = StartIndex; i < Tokens.Count; i++)
            {
                ExpressionToken token = Tokens[i];

                yield return token;

                if (token.TokenType == TokenType.BRACKET_BEGIN)
                    bracketCount++;

                else if (bracketCount > 0 && token.TokenType == TokenType.BRACKET_END)
                    bracketCount--;

                if (bracketCount == 0)
                    yield break;
            }
        }

        public IList<ExpressionToken> GetPrevTokensOnSameLevel(
            IList<ExpressionToken> Tokens,
            Int32 StartIndex)
        {
            List<ExpressionToken> tokens = new List<ExpressionToken>();

            if (StartIndex < 0)
                return null;

            Int32 bracketCount = 0;
            for (var i = StartIndex; i >= 0; i--)
            {
                ExpressionToken token = Tokens[i];

                tokens.Insert(0, token);

                if (token.TokenType == TokenType.BRACKET_END)
                    bracketCount++;

                else if (bracketCount > 0 && token.TokenType == TokenType.BRACKET_BEGIN)
                    bracketCount--;

                if (bracketCount == 0)
                    break;
            }

            if (tokens.Count == 0)
                return null;
            return tokens;
        }

    }
}