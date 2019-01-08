using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DynLan.Exceptions;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Symbols;
using DynLan.OnpEngine.InternalExtenders;
using DynLan.OnpEngine.InternalMethods;


namespace DynLan.OnpEngine.Logic
{
    public static class TokenizerInvariant
    {
        private static Object lck = new Object();

        private static Tokenizer i;

        public static Tokenizer I
        {
            get { if (i == null) lock (lck) if (i == null) i = new Tokenizer(); return i; }
        }
    }

    public class Tokenizer
    {
        public ExpressionGroup Compile(
            String Expression,
            ParserSettings ParserSettings = null)
        {
            if (ParserSettings == null)
                ParserSettings = new ParserSettings();

            return Compile(
                Expression.ToCharArray(),
                ParserSettings);
        }

        public ExpressionGroup Compile(
            IList<Char> expressionChars,
            ParserSettings ParserSettings = null)
        {
            if (expressionChars != null)
            {
                if (ParserSettings == null)
                    ParserSettings = new ParserSettings();

                ExpressionGroup expressionGroup = new ExpressionGroup();

                PrepareMainExpression(
                    IdGenerator.Generate(),
                    expressionChars,
                    ParserSettings,
                    expressionGroup);

                return expressionGroup;
            }
            return null;
        }

        ////////////////////////////

        /// <summary>
        /// Zwraca przygotowane wyrażenie
        /// </summary>
        public void PrepareMainExpression(
           String ExpressionId,
           IList<Char> ExpressionChars,
           ParserSettings ParserSettings,
           ExpressionGroup ExpressionGroup)
        {
            try
            {
                Expression onpExpression = new Expression(ExpressionId);
                onpExpression.Tokens = TokenGetter.GetOptimizedTokens(ExpressionChars, ParserSettings);
                ExpressionGroup.Expressions[ExpressionId] = onpExpression;

                // zamiania znaków równości na wołanie funkcji
                List<ExpressionTokens> resultTokens = new List<ExpressionTokens>();
                while (true)
                {
                    ExpressionTokens setTokens = TakeSetTokens(
                        onpExpression.Tokens);

                    if (setTokens == null)
                        break;

                    resultTokens.Insert(0, setTokens);
                }

                Expression currentExpression = onpExpression;
                foreach (ExpressionTokens resultToken in resultTokens)
                {
                    Expression setExpression = new Expression();
                    setExpression.IsOnpExecution = false;
                    setExpression.Tokens = resultToken;

                    ExpressionGroup.Expressions[setExpression.ID] = setExpression;

                    PrepareExpressions(
                        setExpression,
                        ParserSettings,
                        ExpressionGroup);

                    CorrectSetExpression(
                        setExpression,
                        currentExpression.ID,
                        ParserSettings,
                        ExpressionGroup);

                    currentExpression = setExpression;
                }

                ExpressionGroup.MainExpression = currentExpression;

                PrepareExpressions(
                    onpExpression,
                    ParserSettings,
                    ExpressionGroup);
            }
            catch (Exception ex)
            {
                throw new DynLanCompileException(
                    "Error in expression: '" + new string(ExpressionChars.ToArray(), 0, ExpressionChars.Count).Replace("'", "\\'") + "'; " + ex.Message,
                    ex);
            }
        }

        /// <summary>
        /// Zwraca przygotowane wyrażenie
        /// </summary>
        public void PrepareExpressions(
            Expression onpExpression,
            ParserSettings ParserSettings,
            ExpressionGroup ExpressionGroup)
        {
            new TokenizerQueue().PrepareQueueExpressions(
                onpExpression,
                ParserSettings,
                ExpressionGroup);

            //onpExpression.VariableNames = GetVariablesNames(
            //    onpExpression, ExpressionGroup);

            if (onpExpression.IsOnpExecution)
            {
                onpExpression.OnpTokens = TransformToOnp(
                    onpExpression.Tokens,
                    ParserSettings);
            }
        }

        ////////////////////////////

        private void CorrectSetExpression(
            Expression SetExpression,
            String ValueExpressionID,
            ParserSettings ParserSettings,
            ExpressionGroup ExpressionGroup)
        {
            if (SetExpression.Tokens.Count == 0)
                return;

            // jeśli tylko jeden symbol (podstawienie do zmiennej)
            else if (SetExpression.Tokens.Count == 1)
            {
                ExpressionToken firstToken = SetExpression.Tokens[0];

                if (firstToken.TokenType != TokenType.VARIABLE)
                    throw new DynLanIncorrectExpressionFormatException("Incorrect setting value to type " + firstToken.TokenType + " (" + firstToken.TokenName + ")");

                if (firstToken.TokenName.Length == 0 || Char.IsDigit(firstToken.TokenName[0]))
                    throw new DynLanIncorrectExpressionFormatException("Incorrect name of token '" + firstToken.TokenName + "'");

                SetExpression.Tokens.Clear();
                SetExpression.Tokens.AddRange(
                    new[] {
                        new ExpressionToken(MethodSetValue.Name/*.ToUpper()*/, TokenType.VARIABLE),
                        new ExpressionToken(OperatorTypeHelper.op_methodcall, TokenType.OPERATOR),
                        new ExpressionToken('(', TokenType.BRACKET_BEGIN),
                        new ExpressionToken("'" + firstToken.TokenName + "'", TokenType.VALUE),
                        new ExpressionToken(',', TokenType.SEPARATOR),
                        new ExpressionToken(ValueExpressionID, TokenType.VARIABLE),
                        new ExpressionToken(')', TokenType.BRACKET_END)
                    });
            }
            // jeśli przypisanie wyniku do elementu listy lub dictionary
            else
            {
                Int32 lastIndex = SetExpression.Tokens.Count - 1;

                ExpressionTokens sequence2 = new ExpressionTokens(
                    new TokenizerQueue().
                        GetPrevTokensOnSameLevel(
                            SetExpression.Tokens,
                            lastIndex));

                ExpressionTokens sequence1 = new ExpressionTokens(
                    new TokenizerQueue().
                        GetPrevTokensOnSameLevel(
                            SetExpression.Tokens,
                            lastIndex - sequence2.Count));

                ExpressionTokens sequence0 = new ExpressionTokens(
                    new TokenizerQueue().
                        GetPrevTokensOnSameLevel(
                            SetExpression.Tokens,
                            lastIndex - sequence2.Count - sequence1.Count));

                // a.get@(b).c = d   ========>   a.get@(b).set2@('c',d)
                if (sequence2.Count == 1 &&
                    sequence1.Count == 1 &&
                    OnpOnpTokenHelper.IsPropertyOperatorToken(sequence1[0]))
                {
                    ExpressionToken propertyOperatorToken = sequence1[0];
                    ExpressionToken propertyNameToken = sequence2[0];

                    SetExpression.Tokens.Remove(propertyNameToken);
                    SetExpression.Tokens.AddRange(
                        new[] {
                            new ExpressionToken(ExtenderSetValue.Name/*.ToUpper()*/.ToCharArray(), TokenType.VARIABLE),
                            new ExpressionToken(OperatorTypeHelper.op_methodcall, TokenType.OPERATOR),
                            new ExpressionToken('(', TokenType.BRACKET_BEGIN),
                            new ExpressionToken("'" + propertyNameToken.TokenName + "'", TokenType.VALUE),
                            new ExpressionToken(',', TokenType.SEPARATOR),
                            new ExpressionToken(ValueExpressionID, TokenType.VARIABLE),
                            new ExpressionToken(')', TokenType.BRACKET_END)
                        });
                }
                // a.get@(b).get@(d) = e   ========>   a.get@(b).set2@(d,e) 
                else if (
                    sequence2.Count > 1 &&
                    sequence1.Count == 1 &&
                    OnpOnpTokenHelper.IsFunctionOperatorToken(sequence1[0]) &&
                    sequence0.Count == 1 &&
                    (sequence0[0].TokenType == TokenType.PROPERTY_NAME || sequence0[0].TokenType == TokenType.VARIABLE))
                {
                    ExpressionToken functionNameToken = sequence0[0];
                    ExpressionToken functionOperatorToken = sequence1[0];
                    ExpressionTokens functionCallToken = sequence2;

                    functionNameToken.Set(
                        ExtenderCollectionSetter.NameChars, false);

                    Int32 bracketBeginIndex = SetExpression.
                        Tokens.
                        IndexOf(functionCallToken[0]);

                    SetExpression.Tokens.Insert(
                        bracketBeginIndex + 1,
                        new ExpressionToken(ValueExpressionID, TokenType.VARIABLE));

                    SetExpression.Tokens.Insert(
                        bracketBeginIndex + 2,
                        new ExpressionToken(',', TokenType.SEPARATOR));
                }
                else
                {
                    throw new DynLanIncorrectExpressionFormatException();
                }
            }
        }

        ////////////////////////////

        /// <summary>
        /// Ustala kolejnośc tokenów zgodną z ONP
        /// </summary>
        private ExpressionTokens TransformToOnp(
            IList<ExpressionToken> Tokens,
            ParserSettings ParserSettings)
        {
            ExpressionTokens onpTokens = new ExpressionTokens();

            // defaul settings
            if (ParserSettings == null)
                ParserSettings = new ParserSettings();

            // przygotowanie stosu
            Stack<ExpressionToken> _tokenStack = new Stack<ExpressionToken>();

            // ONP
            for (int i = 0; i < Tokens.Count; i++)
            {
                ExpressionToken token = Tokens[i];

                if (token.TokenType == TokenType.VALUE ||
                    token.TokenType == TokenType.PROPERTY_NAME ||
                    token.TokenType == TokenType.VARIABLE ||
                    /*token.TokenType == TokenType.FUNCTION_CALL ||*/
                    token.TokenType == TokenType.WHITESPACE)
                {
                    onpTokens.Add(token);
                }
                else if (token.TokenType == TokenType.BRACKET_BEGIN)
                {
                    _tokenStack.Push(token);
                }
                else if (token.TokenType == TokenType.OPERATOR)
                {
                    while (_tokenStack.Count > 0)
                    {
                        var lV = _tokenStack.Peek();
                        if (lV.Priority >= token.Priority)
                        {
                            _tokenStack.Pop();
                            onpTokens.Add(lV);
                        }
                        else
                        {
                            break;
                        }
                    }
                    _tokenStack.Push(token);
                }
                else if (token.TokenType == TokenType.BRACKET_END)
                {
                    while (_tokenStack.Count > 0)
                    {
                        var lV = _tokenStack.Peek();
                        if (lV.TokenType == TokenType.BRACKET_BEGIN)
                        {
                            _tokenStack.Pop();
                            break;
                        }
                        else
                        {
                            _tokenStack.Pop();
                            onpTokens.Add(lV);
                        }
                    }
                }
            }

            while (_tokenStack.Count > 0)
            {
                onpTokens.Add(_tokenStack.Pop());
            }

            return onpTokens;
        }

        private IList<String> GetVariablesNames(Expression Expression, ExpressionGroup ExpressionGroup)
        {
            return Expression.
                Tokens.
                Where(t => t.TokenType == TokenType.VARIABLE && !ExpressionGroup.Expressions.ContainsKey(t.TokenName)).
                Select(t => t.TokenName).
                ToArray();
        }

        private ExpressionTokens TakeSetTokens(IList<ExpressionToken> Tokens, Boolean RemoveTakenTokens = true)
        {
            ExpressionTokens result = null;
            if (Tokens.Count >= 2)
            {
                if (Tokens.Any(t => t.TokenType == TokenType.EQUAL_OPERATOR))
                {
                    for (var i = 0; i < Tokens.Count; i++)
                    {
                        ExpressionToken token = Tokens[i];

                        if (token.TokenType == TokenType.EQUAL_OPERATOR)
                        {
                            if (RemoveTakenTokens)
                            {
                                Tokens.RemoveAt(i);
                            }
                            break;
                        }

                        if (result == null)
                            result = new ExpressionTokens();
                        result.Add(token.Clone());

                        if (RemoveTakenTokens)
                        {
                            Tokens.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
            return result;
        }

        private ExpressionTokens GetSetTokens(IList<ExpressionToken> Tokens)
        {
            ExpressionTokens result = null;
            if (Tokens.Count >= 2)
            {
                if (Tokens.Any(t => t.TokenType == TokenType.EQUAL_OPERATOR))
                {
                    for (var i = 0; i < Tokens.Count; i++)
                    {
                        ExpressionToken token = Tokens[i];

                        if (token.TokenType == TokenType.EQUAL_OPERATOR)
                            break;

                        if (result == null)
                            result = new ExpressionTokens();
                        result.Add(token);
                    }
                }
            }
            return result;
        }
    }
}