using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Reflection;
using DynLan.Classes;
using DynLan.OnpEngine.Models;
using DynLan.Extenders;
using DynLan.OnpEngine.Logic;
using DynLan.Helpers;
using DynLan.Exceptions;

namespace DynLan.Evaluator
{
    public static class ContextEvaluator
    {
        public static Boolean ExecuteNext(
            DynLanContext DynLanContext)
        {
            try
            {
                Object currentValue = null;
                DynLanState currentState = DynLanContext.
                    CurrentState;

                Boolean? checkResult = CheckIfFinishedOrEmptyLine(
                    DynLanContext,
                    currentState);

                if (checkResult != null)
                    return checkResult.Value;

                Boolean? executeResult = ExecuteCalculations(
                    DynLanContext,
                    currentState,
                    out currentValue);

                if (executeResult != null)
                    return executeResult.Value;

                Boolean gotoResult = GotoNextLine(
                    DynLanContext,
                    currentState,
                    currentValue);

                return gotoResult;
            }
            catch (Exception ex)
            {
                Exception error = (ex is TargetInvocationException ? ex.InnerException : ex);
                DynLanContext.Error = error;

                Boolean result = false;

                DynLanState currentState = DynLanContext.
                    CurrentState;

                Exception outError = PrepareDynLanException(
                    DynLanContext,
                    error);

                // próba obsługi błędu 
                Boolean handled = DynLanContext.RaiseError(
                    currentState,
                    outError);

                if (outError is DynLanAbortException)
                    handled = false;

                if (!handled)
                {
                    result = GotoCatch(
                        DynLanContext,
                        outError);
                }
                else
                {
                    DynLanContext.Error = null;
                    result = true;
                }

                if (DynLanContext.IsFinished && DynLanContext.Error != null)
                    throw outError;

                return result;
            }
        }

        private static Exception PrepareDynLanException(DynLanContext DynLanContext, Exception Error)
        {
            Exception outError = null;
            if (Error != null)
            {
                IDynLanException DynLanException = Error as IDynLanException;
                if (DynLanException != null)
                {
                    //if (String.IsNullOrEmpty(DynLanException.DynLanStacktrace))
                    DynLanException.DynLanStacktrace = GetStacktrace(DynLanContext);

                    outError = Error;
                }
                else if (Error.GetType() == typeof(Exception))
                {
                    outError = new DynLanExecuteException(Error.Message)
                    {
                        DynLanStacktrace = GetStacktrace(DynLanContext)
                    };
                }
                else
                {
                    outError = new DynLanExecuteException(Error.Message, Error)
                    {
                        DynLanStacktrace = GetStacktrace(DynLanContext)
                    };
                }
            }
            return outError;
        }

        private static String GetStacktrace(DynLanContext DynLanContext)
        {
            String DynLanStacktrace = "";
            if (DynLanContext != null && DynLanContext.Stack != null)
            {
                for (Int32 i = DynLanContext.Stack.Count - 1; i >= 0; i--)
                {
                    DynLanState state = DynLanContext.Stack[i];
                    if (state == null)
                        continue;

                    String name = state.Program is DynLanMethod ? ((DynLanMethod)state.Program).Name : "";

                    //if (DynLanStacktrace.Length > 0)
                    //    DynLanStacktrace += Environment.NewLine; // " | ";

                    String code = "";
                    if (state.Program.Lines != null && state.CurrentLineIndex >= 0 && state.CurrentLineIndex < state.Program.Lines.Count)
                        code = (state.Program.Lines[state.CurrentLineIndex].Code ?? "").Trim();

                    DynLanStacktrace +=
                        "at " +
                        (name == "" ? (state.Program.ContextType + "; ") : (name + " {" + state.Program.ContextType + "}; ")) +
                        "index: " + (state.CurrentLineIndex) + "; " +
                        "code: " + code +
                        "\r\n";

                    /*DynLanStacktrace +=
                        "at " +
                        (name == "" ? (state.Program.ContextType + "; ") : (name + " {" + state.Program.ContextType + "}; ")) +
                        "line index: " + (state.CurrentLineIndex + 1) + "; " +
                        "code: {" + state.Program.Lines[state.CurrentLineIndex].Code + "}";*/
                }
            }
            return DynLanStacktrace;
        }

        private static Boolean? CheckIfFinishedOrEmptyLine(
            DynLanContext DynLanContext,
            DynLanState currentState)
        {
            if (currentState == null || DynLanContext.IsFinished)
            {
                DynLanContext.IsFinished = true;
                return true;
            }

            if (DynLanContext.ExceptionToThrow != null)
            {
                Exception ex = DynLanContext.ExceptionToThrow;
                DynLanContext.ExceptionToThrow = null;
                throw ex;
            }

            DynLanCodeLines lines = currentState.GetCurrentLines();
            DynLanCodeLine currentLine = currentState.GetCurrentLine();

            if (currentLine == null)
            {
                return ExitCurrentContext(
                    DynLanContext,
                    null);
            }

            // jeśli linia jest pusta to przechodzimy do nastepnej
            if (currentLine.IsLineEmpty)
            {
                DynLanCodeLine nextLine = DynLanCodeLinesExtender.NextLine(lines, currentLine);
                if (nextLine == null)
                {
                    return ExitCurrentContext(
                        DynLanContext,
                        null);
                }
                else
                {
                    currentState.CurrentLineID = nextLine.ID;
                }
                return true;
            }
            return null;
        }

        private static Boolean? ExecuteCalculations(
            DynLanContext DynLanContext,
            DynLanState currentState,
            out Object Result)
        {
            Result = null;
            DynLanCodeLines lines = currentState.GetCurrentLines();
            DynLanCodeLine currentLine = currentState.GetCurrentLine();

            // wykonanie kalkulacji
            if (currentLine.ContainsAnyExpressions() &&
                currentLine.OperatorType != EOperatorType.ELSE &&
                currentLine.OperatorType != EOperatorType.PASS &&
                currentLine.OperatorType != EOperatorType.CATCH)
            {
                if (DynLanContext.CurrentState.ExpressionContext == null)
                    DynLanContext.CurrentState.ExpressionContext = new ExpressionContext(currentLine.ExpressionGroup);

                if (DynLanContext.CurrentState.ExpressionContext != null &&
                    DynLanContext.CurrentState.ExpressionContext.IsFinished)
                {
                    Result = DynLanContext.CurrentState.ExpressionContext.Result;
                    DynLanContext.CurrentState.ExpressionContext = null;
                }
                else
                {
                    try
                    {
                        Boolean result = ExpressionEvaluator.NextStep(
                            DynLanContext);

                        return result;
                    }
                    catch
                    {
                        throw;
                    }
                }
            }

            return null;
        }

        //////////////////////////////////////////////

        private static Boolean GotoNextLine(
            DynLanContext DynLanContext,
            DynLanState currentState,
            Object currentValue)
        {
            try
            {
                DynLanCodeLines lines = currentState.GetCurrentLines();
                DynLanCodeLine currentLine = currentState.GetCurrentLine();

                // jesli return to konczymy
                if (currentLine.OperatorType == EOperatorType.RETURN)
                {
                    return ExitCurrentContext(
                        DynLanContext,
                        currentValue,
                        null);
                }
                // throw błędu
                else if (currentLine.OperatorType == EOperatorType.THROW)
                {
                    if (currentValue is Exception)
                    {
                        throw (Exception)currentValue;
                    }
                    else
                    {
                        String message = UniConvert.ToString(currentValue ?? "");
                        throw String.IsNullOrEmpty(message) ? new Exception() : new Exception(message);
                    }

                    /*return ExitCurrentContext(
                        DynLanContext,
                        new Exception(message));*/
                }
                // jesli return to konczymy
                else if (currentLine.OperatorType == EOperatorType.BREAK)
                {
                    return ExitCurrentLoop(
                        DynLanContext,
                        currentValue,
                        null);
                }

                if (currentLine.OperatorType == EOperatorType.WHILE ||
                    currentLine.OperatorType == EOperatorType.IF ||
                    currentLine.OperatorType == EOperatorType.ELIF)
                {
                    Boolean conditionResult = BooleanHelper.IfTrue(currentValue);
                    if (conditionResult)
                    {
                        DynLanCodeLine nextLine = DynLanCodeLinesExtender.
                            NextOnSameOrHigher(lines, currentLine);

                        if (nextLine != null)
                        {
                            currentState.CurrentLineID = nextLine.ID;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        DynLanCodeLine nextLine = DynLanCodeLinesExtender.
                            NextOnSameOrLower(lines, currentLine);

                        if (nextLine == null)
                        {
                            return ExitCurrentContext(
                                DynLanContext,
                                null);
                        }
                        else
                        {
                            if (nextLine.Depth < currentLine.Depth)
                            {
                                while (
                                    nextLine != null &
                                    (nextLine.OperatorType == EOperatorType.ELSE ||
                                    nextLine.OperatorType == EOperatorType.ELIF /*||
                                nextLine.OperatorType == EOperatorType.FINALLY*/))
                                {
                                    nextLine = DynLanCodeLinesExtender.ExitParentIf(lines, nextLine);

                                    if (nextLine == null)
                                        break;
                                }

                                if (nextLine == null)
                                {
                                    return ExitCurrentContext(
                                        DynLanContext,
                                        null);
                                }

                                if (nextLine.Depth < currentLine.Depth)
                                {
                                    //DynLanCodeLine prevIf = lines.
                                    //    PrevLineWithLessDepth(currentLine, l => l.OperatorType == EOperatorType.IF || l.OperatorType == EOperatorType.ELIF);

                                    while (true)
                                    {
                                        DynLanCodeLine prevConditionLine = DynLanCodeLinesExtender.PrevLineWithLessDepth(
                                                lines,
                                                currentLine,
                                                l => l.OperatorType == EOperatorType.IF || l.OperatorType == EOperatorType.ELIF || l.OperatorType == EOperatorType.ELSE || l.OperatorType == EOperatorType.WHILE);

                                        if (prevConditionLine != null &&
                                            prevConditionLine.Depth >= nextLine.Depth &&
                                            prevConditionLine.OperatorType == EOperatorType.WHILE)
                                        {
                                            currentState.CurrentLineID = prevConditionLine.ID;
                                            break;
                                        }
                                        else if (prevConditionLine != null)
                                        {
                                            currentLine = prevConditionLine;
                                        }
                                        else
                                        {
                                            currentState.CurrentLineID = nextLine.ID;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    currentState.CurrentLineID = nextLine.ID;
                                }
                            }
                            else
                            {
                                currentState.CurrentLineID = nextLine.ID;
                            }
                        }
                    }
                }
                else if (
                    currentLine.OperatorType == EOperatorType.TRY ||
                    currentLine.OperatorType == EOperatorType.ELSE)
                {
                    DynLanCodeLine nextLine = DynLanCodeLinesExtender.
                        NextOnSameOrHigher(lines, currentLine);

                    if (nextLine != null)
                    {
                        currentState.CurrentLineID = nextLine.ID;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else if (
                    (currentLine.OperatorType == EOperatorType.FINALLY))
                {
                    throw new NotImplementedException("FINALLY");
                }
                else if (
                    (currentLine.OperatorType == EOperatorType.CATCH))
                {
                    if (DynLanContext.Error != null)
                    {
                        DynLanContext.Error = null;
                        DynLanCodeLine nextLine = DynLanCodeLinesExtender.
                            NextOnSameOrHigher(lines, currentLine);

                        if (nextLine != null)
                        {
                            currentState.CurrentLineID = nextLine.ID;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        DynLanCodeLine nextLine = DynLanCodeLinesExtender.NextOnSameOrLower(
                            lines, currentLine);

                        if (nextLine != null)
                        {
                            currentState.CurrentLineID = nextLine.ID;
                        }
                        else
                        {
                            return ExitCurrentContext(
                                DynLanContext,
                                null);
                        }
                    }
                }
                else if (currentLine.OperatorType == EOperatorType.NONE || currentLine.OperatorType == EOperatorType.PASS)
                {
                    DynLanCodeLine nextLine = DynLanCodeLinesExtender.NextLine(lines, currentLine);
                    if (nextLine != null)
                    {
                        while (
                            nextLine != null &
                            (nextLine.OperatorType == EOperatorType.ELSE ||
                            nextLine.OperatorType == EOperatorType.ELIF /*||
                        nextLine.OperatorType == EOperatorType.FINALLY*/))
                        {
                            nextLine = DynLanCodeLinesExtender.ExitParentIf(lines, nextLine);

                            if (nextLine == null)
                            {
                                return ExitCurrentContext(
                                    DynLanContext,
                                    null);
                            }
                        }

                        if (nextLine == null)
                        {
                            return ExitCurrentContext(
                                DynLanContext,
                                null);
                        }

                        if (nextLine.Depth < currentLine.Depth)
                        {
                            //DynLanCodeLine prevIf = lines.
                            //    PrevLineWithLessDepth(currentLine, l => l.OperatorType == EOperatorType.IF || l.OperatorType == EOperatorType.ELIF);

                            while (true)
                            {
                                DynLanCodeLine prevConditionLine = DynLanCodeLinesExtender.
                                    PrevLineWithLessDepth(
                                        lines,
                                        currentLine,
                                        l => l.OperatorType == EOperatorType.IF || l.OperatorType == EOperatorType.ELIF || l.OperatorType == EOperatorType.ELSE || l.OperatorType == EOperatorType.WHILE);

                                if (prevConditionLine != null &&
                                    prevConditionLine.Depth >= nextLine.Depth &&
                                    prevConditionLine.OperatorType == EOperatorType.WHILE)
                                {
                                    currentState.CurrentLineID = prevConditionLine.ID;
                                    break;
                                }
                                else if (prevConditionLine != null)
                                {
                                    currentLine = prevConditionLine;
                                }
                                else
                                {
                                    currentState.CurrentLineID = nextLine.ID;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            currentState.CurrentLineID = nextLine.ID;
                        }
                    }
                    // jeśli ostatnia linia i jesteśmy w while'u
                    else
                    {
                        //DynLanCodeLine prevIf = lines.
                        //    PrevLineWithLessDepth(currentLine, l => l.OperatorType == EOperatorType.IF || l.OperatorType == EOperatorType.ELIF);

                        while (true)
                        {
                            DynLanCodeLine prevConditionLine = DynLanCodeLinesExtender.
                                PrevLineWithLessDepth(
                                    lines,
                                    currentLine,
                                    l => l.OperatorType == EOperatorType.IF || l.OperatorType == EOperatorType.ELIF || l.OperatorType == EOperatorType.ELSE || l.OperatorType == EOperatorType.WHILE);

                            if (prevConditionLine != null &&
                                prevConditionLine.OperatorType == EOperatorType.WHILE)
                            {
                                currentState.CurrentLineID = prevConditionLine.ID;
                                break;
                            }
                            else if (prevConditionLine != null)
                            {
                                currentLine = prevConditionLine;
                            }
                            else
                            {
                                return ExitCurrentContext(
                                    DynLanContext,
                                    null);
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static Boolean GotoCatch(
            DynLanContext DynLanContext,
            Exception exception)
        {
            while (true)
            {
                DynLanState currentState = DynLanContext.
                    CurrentState;

                // reset dla kontekstu obliczeń, ponieważ przechodzimy do catch'a
                currentState.ExpressionContext = null;

                DynLanCodeLines lines = currentState.GetCurrentLines();
                DynLanCodeLine currentLine = currentState.GetCurrentLine();

                // poszukanie poprzedniego catch'a
                DynLanCodeLine prevCatch = DynLanCodeLinesExtender.
                    PrevLineWithLessDepth(lines, currentLine, l => l.OperatorType == EOperatorType.CATCH);

                // poszukanie poprzedniego try'a
                DynLanCodeLine prevTry = DynLanCodeLinesExtender.
                    PrevLineWithLessDepth(lines, currentLine, l => l.OperatorType == EOperatorType.TRY);

                if (exception is DynLanAbortException)
                {
                    ExitCurrentContext(
                        DynLanContext,
                        exception);

                    if (DynLanContext.IsFinished)
                        break;
                }
                else if (prevTry == null)
                {
                    ExitCurrentContext(
                        DynLanContext,
                        exception);

                    if (DynLanContext.IsFinished)
                        break;
                }
                // jeśli znalazł try'a i nie jesteśmy w catch'u
                else if (prevTry.Depth < currentLine.Depth &&
                        (prevCatch == null || lines.IndexOf(prevCatch) < lines.IndexOf(prevTry)))
                {
                    DynLanCodeLine nextCatch = DynLanCodeLinesExtender.NextOnSameOrLower(
                        lines, 
                        prevTry,
                        i => i.OperatorType == EOperatorType.CATCH);

                    if (nextCatch != null)
                    {
                        ExpressionToken variableForException = null;

                        if (nextCatch.ExpressionGroup != null &&
                            nextCatch.ExpressionGroup.MainExpression != null &&
                            nextCatch.ExpressionGroup.MainExpression.Tokens != null &&
                            nextCatch.ExpressionGroup.MainExpression.Tokens.Count > 0)
                        {
#if !NET20
                            variableForException = nextCatch.
                                    ExpressionGroup.MainExpression.Tokens.
                                    FirstOrDefault(i => i.TokenType != TokenType.BRACKET_BEGIN);
#else
                            variableForException = Linq2.FirstOrDefault( nextCatch.
                                    ExpressionGroup.MainExpression.Tokens, 
                                    i => i.TokenType != TokenType.BRACKET_BEGIN);
#endif
                        }


                        currentState.CurrentLineID = nextCatch.ID;

                        if (variableForException != null && !String.IsNullOrEmpty(variableForException.TokenName))
                            currentState.Object[variableForException.TokenName] = exception;

                        break;
                    }
                    else
                    {
                        ExitCurrentContext(
                            DynLanContext,
                            exception);

                        if (DynLanContext.IsFinished)
                            break;
                    }
                }
                else
                {
                    ExitCurrentContext(
                        DynLanContext,
                        exception);

                    if (DynLanContext.IsFinished)
                        break;
                }
            }
            return false;
        }

        //////////////////////////////////////////////

        private static Boolean ExitCurrentContext(
            DynLanContext DynLanContext,
            Object Result,
            Exception ex)
        {
            DynLanState context = DynLanContext.CurrentState;
            context.CurrentLineID = Guid.Empty;

            // jeśli został tylko ostatni główny context
            if (DynLanContext.Stack.Count == 1)
            {
                DynLanContext.Result = Result;
                DynLanContext.IsFinished = true;
            }
            else
            {
                DynLanContext.PopContext(ex);
                if (DynLanContext.CurrentExpressionState != null)
                    DynLanContext.CurrentExpressionState.PushValue(Result);
            }

            return true;
        }

        private static Boolean ExitCurrentLoop(
            DynLanContext DynLanContext,
            Object Result,
            Exception ex)
        {
            DynLanState context = DynLanContext.CurrentState;
            context.CurrentLineID = Guid.Empty;

            // jeśli został tylko ostatni główny context
            if (DynLanContext.Stack.Count == 1)
            {
                DynLanContext.Result = Result;
                DynLanContext.IsFinished = true;
            }
            else
            {
                /*/DynLanContext.PopContext(ex);
                if (DynLanContext.CurrentExpressionState != null)
                    DynLanContext.CurrentExpressionState.PushValue(Result);*/
            }

            return true;
        }

        private static Boolean ExitCurrentContext(
            DynLanContext DynLanContext,
            Exception ex)
        {
            DynLanState state = DynLanContext.CurrentState;
            state.CurrentLineID = Guid.Empty;

            Object result = null;

            if (state != null &&
                state.ContextType == DynLanContextType.CLASS)
            {
                result = state.Object;
            }

            // jeśli został tylko ostatni główny context
            if (DynLanContext.Stack.Count == 1)
            {
                DynLanContext.Result = result;
                DynLanContext.IsFinished = true;
            }
            else
            {
                DynLanContext.PopContext(ex);
                if (DynLanContext.CurrentExpressionState != null)
                    DynLanContext.CurrentExpressionState.PushValue(result);
            }

            return true;
        }

    }

}