

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using DynLan.Classes;
using DynLan.OnpEngine.Logic;
using DynLan.OnpEngine.Symbols;
using DynLan.OnpEngine.Models;
using DynLan.Helpers;
using DynLan.Exceptions;

namespace DynLan
{
    public class Compiler
    {
        private static readonly char[] str_method = "def".ToCharArray();

        private static readonly char[] str_class = "class".ToCharArray();

        private static readonly char[] str_if = "if".ToCharArray();

        private static readonly char[] str_while = "while".ToCharArray();

        private static readonly char[] str_else = "else".ToCharArray();

        private static readonly char[] str_elif = "elif".ToCharArray();

        private static readonly char[] str_for = "for".ToCharArray();

        private static readonly char[] str_return = "return".ToCharArray();

        private static readonly char[] str_try = "try".ToCharArray();

        private static readonly char[] str_catch = "catch".ToCharArray();

        private static readonly char[] str_finally = "finally".ToCharArray();

        private static readonly char[] str_throw = "throw".ToCharArray();

        private static readonly char[] str_pass = "pass".ToCharArray();

        private static readonly char[] str_break = "break".ToCharArray();

        private static readonly char[] str_quotation = "'".ToCharArray();

        ////////////////////////////////////////////

        public Compiler()
        {

        }

        ////////////////////////////////////////////

        public DynLanProgram Compile(String Code)
        {
            List<Char> chars = new List<Char>(Code.ToCharArray());
            //chars.AddRange(Environment.NewLine.ToCharArray());
            chars.AddRange(";pass".ToCharArray());
            return Compile(chars);
        }

        ////////////////////////////////////////////

        private DynLanProgram Compile(IList<Char> Code)
        {
            CodeLines lines = GetLines(Code);

            DynLanProgram mainProgram = new DynLanProgram();
            List<DynLanProgram> methodStack = new List<DynLanProgram>();
            methodStack.Push(mainProgram);

            Int32 lineNr = 0;
            Int32 depth = 0;

            //foreach (CodeLine line in lines)
            for (var i = 0; i < lines.Count; i++)
            {
                CodeLine line = lines[i];
                CodeLine nextLine = i < lines.Count - 1 ? lines[i + 1] : null;

                lineNr++;
                try
                {
                    Int32 currentDepth = -1;

#if !SPACES_FOR_DEPTH
                    if (StringHelper.SequenceEqualInsensitive(line, DynLanuageSymbols.DepthBegin))
                    {
                        depth++;
                        continue;
                    }

                    else if (StringHelper.SequenceEqualInsensitive(line, DynLanuageSymbols.DepthEnd))
                    {
                        depth--;
                        continue;
                    }

                    currentDepth = depth;
#endif

                    DynLanMethod method = null;
                    method = GetMethodDefinition(line, currentDepth);

#if SPACES_FOR_DEPTH
                    if (method != null)
                        currentDepth = method.Depth;
#else
                    if (method != null)
                    {
                        if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                        {
                            throw new Exception("Method body should begin with bracket { !");
                        }
                        else
                        {
                            depth++; currentDepth++; i++;
                        }
                    }
#endif
                    DynLanClass classDefinition = null;
                    if (method == null)
                    {
                        classDefinition = GetClassDefinition(line, currentDepth);

#if SPACES_FOR_DEPTH
                        if (classDefinition != null)
                            currentDepth = classDefinition.Depth;
#else
                        if (classDefinition != null)
                        {
                            if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                            {
                                throw new Exception("Class body should begin with bracket { !");
                            }
                            else
                            {
                                depth++; currentDepth++; i++;
                            }
                        }
#endif
                    }

                    DynLanCodeLine codeLine = null;
                    if (method == null && classDefinition == null)
                    {
                        codeLine = SetCodeLine(null, line, nextLine, ref currentDepth, ref i);
                        depth = currentDepth;
#if SPACES_FOR_DEPTH
                        if (codeLine != null)
                            currentDepth = codeLine.Depth;
#endif
                    }

                    DynLanProgram currentMethod = methodStack.Peek();
                    if (codeLine == null || !codeLine.IsLineEmpty)
                    {
                        while (currentDepth < currentMethod.Depth ||
                            (currentDepth == currentMethod.Depth && classDefinition != null && classDefinition != currentMethod) ||
                            (currentDepth == currentMethod.Depth && method != null && method != currentMethod))
                        {
                            methodStack.Pop();
                            currentMethod = methodStack.Peek();
                        }
                    }

                    if (method != null)
                    {
                        currentMethod.Methods.Remove_by_Name(method.Name);
                        currentMethod.Methods.Add(method);
                        methodStack.Push(method);
                        continue;
                    }

                    if (classDefinition != null)
                    {
                        currentMethod.Classes.Remove_by_Name(classDefinition.Name);
                        currentMethod.Classes.Add(classDefinition);
                        methodStack.Push(classDefinition);
                        continue;
                    }

                    if (codeLine != null)
                    {
                        if (codeLine.IsLineEmpty == false)
                            currentMethod.Lines.Add(codeLine);
                    }
                }
                catch (Exception ex)
                {
                    throw new DynLanCompileException(
                        "Line index: " + line.LineIndex + " (" + new string(line.ToArray()) + ")" + "; " + ex.Message,
                        ex);
                }
            }

            if (depth != 0)
            {
                throw new DynLanCompileException(
                    "Incorect number of brackets. " + (depth > 0 ? ("Missing } x " + depth) : ("Too many } x " + Math.Abs(depth))));
            }

            return mainProgram;
        }

        public DynLanMethod GetMethodDefinition(CodeLine line, Int32 Depth)
        {
            CodeLine trimmedLine = new CodeLine(line.TrimStart());

            // DEF: wykrycie definicji metody
            if (//trimmedLine.Count > str_method.Length &&
                StringHelper.StartsWith(trimmedLine, str_method, true, true)/* &&
                Char.IsWhiteSpace(trimmedLine[str_method.Length])*/)
            {
                DynLanMethod method = new DynLanMethod();

#if !SPACES_FOR_DEPTH
                method.Depth = Depth + 1; // zwiekszamy poziom metody
#else
                method.Depth = GetDepth(line) + 1; // zwiekszamy poziom metody
#endif

                string codeLine = trimmedLine.ToString().Substring(str_method.Length + 1);
                if (codeLine.Trim().Length == 0)                
                    throw new Exception("DEF method name cannot be empty !");                

                IList<OnpMethodPart> methodParameters = MethodParser.
                    ExtractNames(codeLine, true);

                foreach (OnpMethodPart methodParameter in methodParameters)
                {
                    if (methodParameter.Part == EOnpMethodPart.METHOD_NAME)
                    {
#if CASE_INSENSITIVE
                        method.Name = methodParameter.Code.ToUpper();
#else
                        method.Name = methodParameter.Code;
#endif
                    }
                    else if (methodParameter.Part == EOnpMethodPart.PARAMETER)
                    {
                        method.Parameters.Add(methodParameter.Code);
                    }
                }

                return method;
            }
            return null;
        }

        public DynLanClass GetClassDefinition(CodeLine line, Int32 Depth)
        {
            CodeLine trimmedLine = new CodeLine(line.TrimStart());

            // DEF: wykrycie definicji klasy
            if (//trimmedLine.Count > str_class.Length &&
                StringHelper.StartsWith(trimmedLine, str_class, true, true) /*&&
                Char.IsWhiteSpace(trimmedLine[str_class.Length])*/)
            {
                DynLanClass classDefinition = new DynLanClass();

#if !SPACES_FOR_DEPTH
                classDefinition.Depth = Depth + 1; // zwiekszamy poziom klasy
#else
                classDefinition.Depth = GetDepth(line) + 1; // zwiekszamy poziom klasy
#endif

                string codeLine = trimmedLine.ToString().Substring(str_class.Length + 1);
                if (codeLine.Trim().Length == 0)
                    throw new Exception("CLASS name cannot be empty !");

                IList<OnpMethodPart> methodParameters = MethodParser.
                    ExtractNames(codeLine, true);

                foreach (OnpMethodPart methodParameter in methodParameters)
                {
                    if (methodParameter.Part == EOnpMethodPart.METHOD_NAME)
                    {
#if CASE_INSENSITIVE
                        classDefinition.Name = methodParameter.Code.ToUpper();
#else
                        classDefinition.Name = methodParameter.Code;
#endif
                    }
                    else if (methodParameter.Part == EOnpMethodPart.PARAMETER)
                    {
                        classDefinition.Parameters.Add(methodParameter.Code);
                    }
                }

                return classDefinition;
            }
            return null;
        }

        /*public DynLanCodeLine GetCodeLine(Int32 Depth, CodeLine line, CodeLine nextLine)
        {
            return SetCodeLine(null, Depth, line, nextLine);
        }*/

        /*public DynLanCodeLine SetCodeLine(DynLanCodeLine compiledLine, Int32 Depth, String line)
        {
            return SetCodeLine(compiledLine, Depth, new CodeLine(line));
        }*/

        public DynLanCodeLine SetCodeLine(DynLanCodeLine compiledLine, CodeLine line, CodeLine nextLine, ref Int32 Depth, ref int CharIndex)
        {
            Int32 orgDepth = Depth;

            IList<Char> lineTrimmed = line.
                TrimEnd().
                TrimStart().
                ToArray();

            IList<Char> lineBody = line;
            EOperatorType operatorType = EOperatorType.NONE;

            // IF: wykrycie definicji IF'a
            if (//lineTrimmed.Count > str_if.Length &&
                   StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_if, true, true)/* &&
                   Char.IsWhiteSpace(lineTrimmed[str_if.Length])*/ )
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.IF;

                lineBody = lineTrimmed.
                    Substring2(str_if.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#else
                if (lineBody.Count == 0)
                {
                    throw new Exception("IF body cannot be empty !");
                }
                if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                {
                    throw new Exception("IF body should begin with bracket { !");
                }
                else
                {
                    Depth++; CharIndex++;
                }
#endif
            }
            // WHILE: wykrycie definicji WHILE'a
            else if (//lineTrimmed.Count > str_while.Length &&
                   StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_while, true, true) /*&&
                   Char.IsWhiteSpace(lineTrimmed[str_while.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.WHILE;

                lineBody = lineTrimmed.
                    Substring2(str_while.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#else
                if (lineBody.Count == 0)
                {
                    throw new Exception("WHILE body cannot be empty !");
                }
                if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                {
                    throw new Exception("WHILE body should begin with bracket { !");
                }
                else
                {
                    Depth++; CharIndex++;
                }
#endif
            }
            // ELSE: wykrycie definicji ELSE'a
            else if (// lineTrimmed.Count >= str_else.Length &&
                   StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_else, true, true))
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.ELSE;

                lineBody = lineTrimmed.
                    Substring2(str_else.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#else
                if (lineBody.Count > 0)
                {
                    throw new Exception("ELSE body must be empty !");
                }
                if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                {
                    throw new Exception("ELSE body should begin with bracket { !");
                }
                else
                {
                    Depth++; CharIndex++;
                }
#endif
            }
            // ELIF: wykrycie definicji ELIF'a
            else if (// lineTrimmed.Count > str_elif.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_elif, true, true) /*&&
                       Char.IsWhiteSpace(lineTrimmed[str_elif.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.ELIF;

                lineBody = lineTrimmed.
                    Substring2(str_elif.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#else
                if (lineBody.Count == 0)
                {
                    throw new Exception("ELIF body cannot be empty !");
                }
                if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                {
                    throw new Exception("ELIF body should begin with bracket { !");
                }
                else
                {
                    Depth++; CharIndex++;
                }
#endif
            }
            // RETURN: wykrycie definicji RETURN'a
            else if (// lineTrimmed.Count > str_return.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_return, true, true) /*&&
                       Char.IsWhiteSpace(lineTrimmed[str_return.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.RETURN;

                lineBody = lineTrimmed.
                    Substring2(str_return.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#endif
            }
            // RETURN: wykrycie definicji RETURN'a
            /*else if (// lineTrimmed.Count >= str_return.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_return, true, true))
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.RETURN;

                lineBody = lineTrimmed.
                    Substring2(str_return.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#endif
            }*/
            // RETURN: wykrycie definicji PASS'a
            else if (//(lineTrimmed.Count == str_pass.Length || (lineTrimmed.Count > str_pass.Length && Char.IsWhiteSpace(lineTrimmed[str_pass.Length])))
                StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_pass, true, true))
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.PASS;

                lineBody = lineTrimmed.
                    Substring2(str_pass.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#endif
            }
            // TRY: wykrycie definicji TRY'a
            else if (// lineTrimmed.Count > str_try.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_try, true, true) /*&&
                       Char.IsWhiteSpace(lineTrimmed[str_try.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.TRY;

                lineBody = lineTrimmed.
                    Substring2(str_try.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#else
                if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                {
                    throw new Exception("TRY body should begin with bracket { !");
                }
                else
                {
                    Depth++; CharIndex++;
                }
#endif
            }
            // CATCH: wykrycie definicji CATCH'a
            else if (// lineTrimmed.Count > str_catch.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_catch, true, true) /*&&
                       Char.IsWhiteSpace(lineTrimmed[str_catch.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.CATCH;

                lineBody = lineTrimmed.
                    Substring2(str_catch.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#else
                if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                {
                    throw new Exception("CATCH body should begin with bracket { !");
                }
                else
                {
                    Depth++; CharIndex++;
                }
#endif
            }
            // FINALLY: wykrycie definicji FINALLY'a
            else if (// lineTrimmed.Count > str_finally.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_finally, true, true) /*&&
                       Char.IsWhiteSpace(lineTrimmed[str_finally.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.FINALLY;

                lineBody = lineTrimmed.
                    Substring2(str_finally.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#else
                if (!StringHelper.SequenceEqualInsensitive(nextLine, DynLanuageSymbols.DepthBegin))
                {
                    throw new Exception("FINALLY body should begin with bracket { !");
                }
                else
                {
                    Depth++; CharIndex++;
                }
#endif
            }
            // THROW: wykrycie definicji THROW'a
            else if (lineTrimmed.Count > str_throw.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_throw, true, true) /*&&
                       Char.IsWhiteSpace(lineTrimmed[str_THROW.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.THROW;

                lineBody = lineTrimmed.
                    Substring2(str_throw.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#endif
            }
            // BREAK: wykrycie definicji BREAK'a
            else if (// lineTrimmed.Count > str_break.Length &&
                       StringHelper.StartsWith(lineTrimmed.TrimStart().ToArray(), str_break, true, true) /*&&
                       Char.IsWhiteSpace(lineTrimmed[str_break.Length])*/)
            {
                Int32 depth = GetDepth(line);
                operatorType = EOperatorType.BREAK;

                lineBody = lineTrimmed.
                    Substring2(str_break.Length).
                    TrimStart().
                    ToList();

#if SPACES_FOR_DEPTH
                for (int i = 0; i < depth; i++)
                    lineBody.Insert(0, ' ');
                // lineBody.TrimEnd(':');
#endif
            }

            ExpressionGroup expressionGroup = TokenizerInvariant.I.
                Compile(lineBody);

            if (compiledLine == null)
                compiledLine = new DynLanCodeLine();

            compiledLine.Code = line.ToString2();
            compiledLine.ExpressionGroup = expressionGroup;
            compiledLine.OperatorType = operatorType;
            compiledLine.IsLineEmpty = lineTrimmed.Count == 0;

#if !SPACES_FOR_DEPTH
            compiledLine.Depth = orgDepth;
#else
            compiledLine.Depth += GetDepth(lineBody);
#endif

            return compiledLine;
        }

        ////////////////////////////////////////////

        private CodeLines GetLines(IList<Char> Chars)
        {
            CodeLines outChars = new CodeLines();

            CodeLine currentLine = new CodeLine();
            outChars.Add(currentLine);
            Boolean wasCommentStart = false;

            for (Int32 i = 0; i <= Chars.Count; i++)
            {
                {
                    OnpOnpStringFindResult firstNext = StringHelper.FirstNextIndex(
                        Chars, i,
                        new[] { DynLanuageSymbols.NewLineChars, DynLanuageSymbols.DepthBegin, DynLanuageSymbols.DepthEnd },
                        false);

                    // gdy nie znalazł znaku nowej linii
                    if (firstNext == null || firstNext.Index < 0)
                    {
                        OnpOnpStringFindResult commentStartNext = StringHelper.FirstNextIndex(
                            Chars, i,
                            new[] { DynLanuageSymbols.Comment1StartSymbol },
                            false);

                        if (commentStartNext == null || commentStartNext.Index < 0)
                        {
                            for (var j = i; j < Chars.Count; j++)
                            {
                                var ch = Chars[j];
#if !SPACES_FOR_DEPTH
                                if (currentLine.Count > 0 || !Char.IsWhiteSpace(ch))
#endif
                                    currentLine.Add(ch);
                            }
                        }
                        else
                        {
                            for (var j = i; j < commentStartNext.Index; j++)
                            {
                                var ch = Chars[j];
#if !SPACES_FOR_DEPTH
                                if (currentLine.Count > 0 || !Char.IsWhiteSpace(ch))
#endif
                                    currentLine.Add(ch);
                            }
                            wasCommentStart = true;
                        }
                        break;

                    }
                    // gdy znalazł znak nowej linii
                    else
                    {
                        if (DynLanuageSymbols.NewLineChars.Contains(firstNext.Chars) ||
                            DynLanuageSymbols.DepthBegin.Contains(firstNext.Chars) ||
                            DynLanuageSymbols.DepthEnd.Contains(firstNext.Chars))
                        {
                            OnpOnpStringFindResult commentStartNext = StringHelper.FirstNextIndex(
                                Chars, i, firstNext.Index,
                                new[] { DynLanuageSymbols.Comment1StartSymbol },
                                false);

                            if (commentStartNext == null || commentStartNext.Index < 0)
                            {
                                for (var j = i; j < firstNext.Index; j++)
                                {
                                    var ch = Chars[j];
#if !SPACES_FOR_DEPTH
                                    if (currentLine.Count > 0 || !Char.IsWhiteSpace(ch))
#endif
                                        currentLine.Add(ch);
                                }
                            }
                            else
                            {
                                for (var j = i; j < commentStartNext.Index; j++)
                                {
                                    var ch = Chars[j];
#if !SPACES_FOR_DEPTH
                                    if (currentLine.Count > 0 || !Char.IsWhiteSpace(ch))
#endif
                                        currentLine.Add(ch);
                                }
                                wasCommentStart = true;
                            }

                            if (DynLanuageSymbols.DepthBegin.Contains(firstNext.Chars) ||
                                DynLanuageSymbols.DepthEnd.Contains(firstNext.Chars))
                            {
                                if (currentLine.Count > 0)
                                {
                                    currentLine = new CodeLine();
                                    outChars.Add(currentLine);
                                }
                                currentLine.AddRange(firstNext.Chars);
                            }

                            i = firstNext.Index + firstNext.Chars.Length - 1;

                            currentLine = new CodeLine();
                            outChars.Add(currentLine);
                            wasCommentStart = false;
                        }
                    }
                }
            }

            return outChars;
        }

        private Int32 GetDepth(IList<Char> line)
        {
#if !SPACES_FOR_DEPTH
            return -1;
#endif
            int Depth = 0;
            for (var i = 0; i < line.Count; i++)
                if (Char.IsWhiteSpace(line[i]))
                {
                    Depth++;
                }
                else
                {
                    break;
                }
            return Depth;
        }
    }

    public static class DynLanConverter
    {
        public static String ConvertToString(Object Obj, Boolean WithQuotes = true)
        {
            if (Obj == null)
            {
                return From_Simple("null");
            }
            else
            {
                Type t = Obj.GetType();
                if (t.IsEnum)
                {
                    return From_Enum(Obj);
                }
                else if (t == typeof(TimeSpan) || t == typeof(TimeSpan?))
                {
                    TimeSpan time = (TimeSpan)Obj;
                    var str = String.Format(
                        "'{0}:{1}:{2}.{3}'",
                        time.Days,
                        FormatDatePart(time.Minutes),
                        FormatDatePart(time.Seconds),
                        FormatMilisecondDatePart(time.Milliseconds));
                    return str;
                }
                else if (t == typeof(DateTime))
                {
                    return From_DateTime((DateTime)Obj, WithQuotes);
                }
                else if (t == typeof(DateTime?))
                {
                    return From_DateTime((DateTime?)Obj, WithQuotes);
                }
                else if (t == typeof(Boolean?) ||
                    t == typeof(Boolean))
                {
                    return From_Boolean(Obj);
                }
                else if (t == typeof(Decimal) ||
                    t == typeof(Single) ||
                    t == typeof(Double) ||
                    t == typeof(Decimal?) ||
                    t == typeof(Single?) ||
                    t == typeof(Double?))
                {
                    return From_Numeric(Obj);
                }
                else if (t == typeof(Int32) ||
                    t == typeof(Int32?) ||
                    t == typeof(Int64) ||
                    t == typeof(Int64?) ||
                    t == typeof(Int16) ||
                    t == typeof(Int16?))
                {
                    return From_Int(Obj);
                }
                else if (t == typeof(Byte[]))
                {
                    return From_ByteArray((Byte[])Obj, WithQuotes);
                }
                else
                {
                    return From_String(Obj.ToString(), WithQuotes);
                }
                throw new NotSupportedException();
            }
        }

        public static String Format(String Format, params Object[] Params)
        {
            if (Params != null && Params.Length > 0)
            {
                Object[] lNewParams = new Object[Params.Length];
                Int32 lIndex = -1;
                foreach (var lParam in Params)
                    lNewParams[++lIndex] = ConvertToString(lParam, true);
                return String.Format(Format, lNewParams);
            }
            else
            {
                return Format;
            }
        }

        private static String From_DateTime(DateTime? Datetime, Boolean WithQuotes)
        {
            if (Datetime.HasValue)
            {
                From_DateTime(Datetime.Value, WithQuotes);
            }
            return null;
        }

        private static String FormatDatePart(Int32 Value)
        {
            StringBuilder lStr = new StringBuilder();
            lStr.Append(Value);
            if (lStr.Length == 1) lStr.Insert(0, "0");
            return lStr.ToString();
        }

        private static String FormatMilisecondDatePart(Int32 Value)
        {
            StringBuilder lStr = new StringBuilder();
            lStr.Append(Value);
            if (lStr.Length == 1) lStr.Insert(0, "00");
            else if (lStr.Length == 2) lStr.Insert(0, "0");
            return lStr.ToString();
        }

        private static String From_DateTime(DateTime Datetime, Boolean WithQuotes)
        {
            String year = Datetime.Year.ToString();
            while (year.Length < 4)
                year = "0" + year;

            if (WithQuotes)
            {
                return String.Format(
                    "'{0}-{1}-{2} {3}:{4}:{5}.{6}'",
                    year,
                    FormatDatePart(Datetime.Month),
                    FormatDatePart(Datetime.Day),
                    FormatDatePart(Datetime.Hour),
                    FormatDatePart(Datetime.Minute),
                    FormatDatePart(Datetime.Second),
                    FormatMilisecondDatePart(Datetime.Millisecond));
            }
            else
            {
                return String.Format(
                    "{0}-{1}-{2} {3}:{4}:{5}.{6}",
                    year,
                    FormatDatePart(Datetime.Month),
                    FormatDatePart(Datetime.Day),
                    FormatDatePart(Datetime.Hour),
                    FormatDatePart(Datetime.Minute),
                    FormatDatePart(Datetime.Second),
                    FormatMilisecondDatePart(Datetime.Millisecond));
            }
        }

        private static String From_Enum(Object EnumValue)
        {
            return System.Convert.ToInt32(EnumValue).ToString();  // true.Equals(BooleanValue) ? "1" : "0";
        }

        private static String From_Boolean(Object BooleanValue)
        {
            return true.Equals(BooleanValue) ? "true" : "false";
        }

        private static String From_Numeric(Object NumericValue)
        {
            return UniConvert.ToString(NumericValue).Replace(",", ".");
        }

        private static String From_Int(Object NumericValue)
        {
            return UniConvert.ToString(NumericValue);
        }

        private static String From_String(String String, Boolean WithQuotes)
        {
            if (WithQuotes)
            {
                return String.Format(
                    "'{0}'",
                    String.Replace("'", "\\'"));
            }
            else
            {
                return String.Replace("'", "''");
            }
        }

        private static String From_Simple(Object Object)
        {
            return Object.ToString();
        }

        private static String From_ByteArray(Byte[] Array, Boolean WithQuotes)
        {
            if (WithQuotes)
            {
                return String.Format(
                    "'{0}'",
                    BitConverter.ToString(Array));
            }
            else
            {
                return BitConverter.ToString(Array);
            }
        }
    }
}
