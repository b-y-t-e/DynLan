using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.InteropServices;
using DynLan;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Symbols;
using DynLan.Helpers;
using DynLan.Classes;


namespace DynLan.OnpEngine.Logic
{
    public static class StringHelper
    {

        public static Boolean SequenceEqualInsensitive(
            IList<Char> Items1,
            IList<Char[]> ListOfItems2)
        {
            if (Items1 == null)
                return false;
            foreach (Char[] items2 in ListOfItems2)
                if (SequenceEqualInsensitive(Items1, items2))
                    return true;
            return false;
        }

        public static Boolean SequenceEqualInsensitive(
            this IList<Char> Items1,
            IList<Char> Items2)
        {
            if (Items1 != null && Items2 != null && Items1.Count == Items2.Count)
            {
                Int32 c = Items1.Count;
                for (Int32 i = 0; i < c; i++)
                    if (Char.ToLowerInvariant(Items1[i]) != Char.ToLowerInvariant(Items2[i]))
                        return false;
                return true;
            }
            else if (Items1 == null && Items2 == null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static String FormatDate(DateTime DateTime, String Format)
        {
            String r = Format ?? "";
            r = r.Replace("mm", FormatDatePart(DateTime.Month));
            r = r.Replace("yyyy", DateTime.Year.ToString());
            r = r.Replace("dd", FormatDatePart(DateTime.Day));

            r = r.Replace("hh", FormatDatePart(DateTime.Hour));
            r = r.Replace("mi", FormatDatePart(DateTime.Minute));
            r = r.Replace("ss", FormatDatePart(DateTime.Second));
            r = r.Replace("ms", FormatDatePart(DateTime.Millisecond, 3));
            r = r.Replace("mmm", FormatDatePart(DateTime.Millisecond, 3));
            return r;
        }

        private static String FormatDatePart(Int32 Val, Int32 Count = 2)
        {
            var a = Val.ToString();
            if (Count == 2)
                return a.Length == 2 ? a : ("0" + a);
            while (a.Length < Count)
                a = "0" + a;
            return a;
        }

        public static String RemoveBracketsFromMethodCall(this String Text)
        {
            Text = (Text ?? "").Trim();
            if (Text.StartsWith("(")) Text = Text.Substring(1);
            if (Text.EndsWith(")")) Text = Text.Substring(0, Text.Length - 1);
            return Text;
        }

        public static bool IsWordBreakChar(Char ch)
        {
            if (Char.IsLetterOrDigit(ch))
                return false;
            return true;

            /*1if (Char.IsWhiteSpace(ch))
                return true;

            switch(ch)
            {
                case (':'):
                case (';'):
                case ('{'):
                case ('}'):
                case ('('):
                case (')'):
                case ('-'):
                case ('+'):
                case ('='):
                case ('*'):
                case ('/'):
                case ('&'):
                case ('^'):
                case ('%'):
                case ('$'):
                case (':'):
                    return true;
            }

            return false;*/
        }

        public static ExpressionValue GetValueFromText(IList<Char> chars)
        {
            if (chars == null)
                return null;

            if (StringHelper.IsString(chars, '\''))
            {
                var name = chars.ToString2();
                return new ExpressionValue(
                    name.Substring(1, name.Length - 2).Replace("\\'", "'"));
            }
            else if (StringHelper.IsInteger(chars))
            {
                return new ExpressionValue(
                    UniConvert.ParseUniInt64(chars.Replace2(',', '.').ToString2()));
            }
            else if (StringHelper.IsNumber(chars))
            {
                return new ExpressionValue(
                    UniConvert.ParseUniDecimal(chars.Replace2(',', '.').ToString2()));
            }
            else if (chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_null))
            {
                return new ExpressionValue(null);
            }
            else if (chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_false))
            {
                return new ExpressionValue(false);
            }
            else if (chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_true))
            {
                return new ExpressionValue(true);
            }
            else if (chars.SequenceEqual(OperatorTypeHelper.symbol_new_line))
            {
                return new ExpressionValue(Environment.NewLine);
            }
            else if (chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_undefined))
            {
                return new ExpressionValue(new Undefined());
            }
            return null;
        }

        //////////////////////////////

        public static String ToString(Object Object)
        {
            Type objectType = Object != null ? Object.GetType() : null;
            if (Object == null)
            {
                return "NULL";
            }
            else if (MyTypeHelper.IsDateTime(objectType))
            {
                return "todatetime('" + UniConvert.ToUniString(Object) + "')";
            }
            else if (objectType == typeof(InternalDateTime))
            {
                DateTime? dateTime = objectType == null ?
                    null :
                    (DateTime?)new DateTime(((InternalDateTime)Object).Ticks);

                return "todatetime('" + UniConvert.ToUniString(dateTime) + "')";
            }
            else if (MyTypeHelper.IsNumeric(objectType))
            {
                return UniConvert.ToUniString(Object);
            }
            else if (MyTypeHelper.IsString(objectType))
            {
                return "'" + Object.ToString().Replace("'", "\\'") + "'";
            }
            else
            {
                throw new NotSupportedException("Objects of type " + objectType.Name + " are not supported in expressions");
            }
        }

        public static List<Char> Substring(
            List<Char> Chars,
            Int32 StartIndex,
            Int32? Length = null)
        {
            return Substring((IList<Char>)Chars, StartIndex, Length);
        }

        public static List<Char> Substring(
            this IList<Char> Chars,
            Int32 StartIndex,
            Int32? Length = null)
        {
            Int32 max = Length == null ?
                Chars.Count :
                (StartIndex + Length > Chars.Count ? Chars.Count : StartIndex + Length.Value);

            List<Char> outChars = new List<Char>();
            for (var i = StartIndex; i < max; i++)
                outChars.Add(Chars[i]);
            return outChars;
        }

        public static IEnumerable<Char> Substring2(
            List<Char> Chars,
            Int32 StartIndex,
            Int32? Length = null)
        {
            return Substring2((IList<Char>)Chars, StartIndex, Length);
        }

        public static IEnumerable<Char> Substring2(
            this IList<Char> Chars,
            Int32 StartIndex,
            Int32? Length = null)
        {
            Int32 max = Length == null ?
                Chars.Count :
                (StartIndex + Length > Chars.Count ? Chars.Count : StartIndex + Length.Value);

            List<Char> outChars = new List<Char>();
            for (var i = StartIndex; i < max; i++)
                yield return Chars[i];
        }

        public static IEnumerable<Char> Replace2(
            this IEnumerable<Char> Chars,
            Char From,
            Char? To)
        {
            foreach (Char ch in Chars)
                if (ch == From)
                {
                    if (To != null)
                        yield return To.Value;
                }
                else
                    yield return ch;
        }

        public static String ToString2(
            this IEnumerable<Char> Chars)
        {
            StringBuilder str = new StringBuilder();
            foreach (Char ch in Chars)
                str.Append(ch);
            return str.ToString();
        }

        public static void Trim(
            this IList<Char> Chars,
            Char? trimchar = null)
        {
            for (var i = Chars.Count - 1; i >= 0; i--)
            {
                char ch = Chars[i];
                if (trimchar == null ? Char.IsWhiteSpace(ch) : trimchar == ch)
                    Chars.RemoveAt(i);
                else
                    break;
            }

            while (Chars.Count > 0)
            {
                char ch = Chars[0];
                if (trimchar == null ? Char.IsWhiteSpace(ch) : trimchar == ch)
                    Chars.RemoveAt(0);
                else
                    break;
            }
        }

        public static IList<Char> TrimEnd(
            this IEnumerable<Char> Chars,
            Char? trimchar = null)
        {
            var chars = Chars.ToList();
            for (var i = chars.Count - 1; i >= 0; i--)
            {
                char ch = chars[i];
                if (trimchar == null ? Char.IsWhiteSpace(ch) : trimchar == ch)
                    chars.RemoveAt(i);
                else
                    break;
            }
            return chars;
        }

        public static IList<Char> TrimEnd(
            this IList<Char> Chars,
            Char? trimchar = null)
        {
            for (var i = Chars.Count - 1; i >= 0; i--)
            {
                char ch = Chars[i];
                if (trimchar == null ? Char.IsWhiteSpace(ch) : trimchar == ch)
                    Chars.RemoveAt(i);
                else
                    break;
            }
            return Chars;
        }

        public static IEnumerable<Char> TrimStart(
            this IEnumerable<Char> Chars,
            Char? trimchar = null)
        {
            Boolean areWhiteChars = true;
            foreach (char ch in Chars)
            {
                if (trimchar == null ? Char.IsWhiteSpace(ch) : trimchar == ch)
                {

                }
                else
                {
                    areWhiteChars = false;
                }

                if (!areWhiteChars)
                    yield return ch;
            }
        }

        public static Boolean StartsWith(
            this IEnumerable<Char> Chars,
            Char startChar)
        {
            foreach (char ch in Chars)
            {
                return ch == startChar;
            }
            return false;
        }

        public static Boolean IsValue(
            IList<Char> Chars,
            Char StringChar = '\'')
        {
            return IsNumber(Chars) ||
                IsString(Chars, StringChar) || /*IsDateTime(Chars) ||*/
                IsNull(Chars) ||
                IsNewLine(Chars) ||
                IsUndefined(Chars) ||
                IsFalse(Chars) ||
                IsTrue(Chars);
        }

        public static Boolean IsInteger(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                foreach (var lChar in Chars)
                    if (!Char.IsNumber(lChar) && lChar != '-')
                        return false;
                return true;
            }
        }

        public static Boolean IsNumber(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                foreach (var lChar in Chars)
                    if (!Char.IsNumber(lChar) && lChar != '.' && lChar != ',' && lChar != '-')
                        return false;
                return true;
            }
        }

        public static Boolean IsString(
            IList<Char> Chars,
            Char StringChar = '\'')
        {
            if (Chars.Count < 2)
            {
                return false;
            }
            else
            {
                if (Chars[0] == StringChar && Chars[Chars.Count - 1] == StringChar)
                {
                    for (int i = 1; i < Chars.Count - 1; i++)
                    {
                        Char? lPrevChar = i > 0 ? (Char?)Chars[i - 1] : null;
                        Char lChar = Chars[i];

                        if (lChar == StringChar)
                        {
                            if (lPrevChar != '\\')
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /*public static Boolean IsDateTime(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                if (Chars[0] == '#' && Chars[Chars.Count - 1] == '#')
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        */
        public static Boolean IsUndefined(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                return Chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_undefined);
            }
        }

        public static Boolean IsNull(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                return Chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_null);
            }
        }

        public static Boolean IsTrue(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                return Chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_true);
            }
        }

        public static Boolean IsFalse(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                return Chars.SequenceEqualInsensitive(OperatorTypeHelper.symbol_false);
            }
        }

        public static Boolean IsNewLine(
            IList<Char> Chars)
        {
            if (Chars.Count == 0)
            {
                return false;
            }
            else
            {
                return
                    Chars.Count == OperatorTypeHelper.symbol_new_line.Length &&
                    Chars.SequenceEqual(OperatorTypeHelper.symbol_new_line);
            }
        }

        public static Char[] StartsWith(
            IList<Char> Source,
            IList<Char[]> ItemsToFind,
            Boolean Insensitive,
            Boolean WholeWord = false)
        {
            foreach (Char[] item in ItemsToFind)
                if (StartsWith(Source, item, Insensitive, WholeWord))
                    return item;
            return null;
        }

        public static Char[] StartsWith(
            IList<Char> Source,
            IList<Char[]> ItemsToFind,
            Int32 StartIndex,
            Boolean Insensitive,
            Boolean WholeWord = false)
        {
            foreach (Char[] item in ItemsToFind)
                if (StartsWith(Source, item, StartIndex, Insensitive, WholeWord))
                    return item;
            return null;
        }

        public static Boolean AreEqual(
            IList<Char> Items1,
            IList<Char> Items2,
            Boolean Insensitive)
        {
            if ((Items1 == null && Items2 != null) ||
                (Items1 != null && Items2 == null))
                return false;

            if (Items1 == null && Items2 == null)
                return true;

            if (Items1.Count != Items2.Count)
                return false;

            for (var i = 0; i < Items1.Count; i++)
            {
                if (Insensitive)
                {
                    if (Char.ToLowerInvariant(Items1[i]) != Char.ToLowerInvariant(Items2[i]))
                        return false;
                }
                else
                {
                    if (Items1[i] != Items2[i])
                        return false;
                }
            }


            return true;
        }

        public static Boolean StartsWith(
            IList<Char> Source,
            IList<Char> ItemToFind,
            Boolean Insensitive,
            Boolean WholeWord = false)
        {
            if (Source == null || ItemToFind == null)
                return false;

            if (ItemToFind.Count > Source.Count)
                return false;

            Int32 length = ItemToFind.Count;
            int i = 0;
            for (i = 0; i < length; i++)
                if (Insensitive)
                {
                    if (Char.ToLowerInvariant(Source[i]) != Char.ToLowerInvariant(ItemToFind[i]))
                        return false;
                }
                else
                {
                    if (Source[i] != ItemToFind[i])
                        return false;
                }

            if (WholeWord)
            {
                if (i == Source.Count)
                    return true;

                var ch = Source[i];
                if (!IsWordBreakChar(ch))
                    return false;
            }

            return true;
        }

        public static Boolean StartsWith(
            IList<Char> Source,
            IList<Char> ItemToFind,
            Int32 StartIndex,
            Boolean Insensitive,
            Boolean WholeWord = false)
        {
            if (Source == null || ItemToFind == null)
                return false;

            else if (StartIndex + ItemToFind.Count > Source.Count)
                return false;

            else
            {
                Int32 length = StartIndex + ItemToFind.Count;
                Int32 j = 0;
                Int32 i = 0;
                for (i = StartIndex; i < length; i++)
                {
                    if (Insensitive)
                    {
                        if (Char.ToLowerInvariant(Source[i]) != Char.ToLowerInvariant(ItemToFind[j]))
                            return false;
                    }
                    else
                    {
                        if (Source[i] != ItemToFind[j])
                            return false;
                    }
                    j++;
                }

                if (WholeWord)
                {
                    if (i >= Source.Count)
                        return false;

                    var ch = Source[i];
                    if (!IsWordBreakChar(ch))
                        return false;
                }
                
                return true;
            }
        }

        public static OnpOnpStringFindResult FirstNextIndex(
            IList<Char> Source,
            Int32 StartIndex,
            IList<Char[][]> ItemsToFind,
            Boolean Insensitive,
            Char StringChar = '\'')
        {
            return FirstNextIndex(Source, StartIndex, Source.Count, ItemsToFind, Insensitive, StringChar);
        }

        public static OnpOnpStringFindResult FirstNextIndex(
            IList<Char> Source,
            Int32 StartIndex,
            Int32 EndIndex,
            IList<Char[][]> ItemsToFind,
            Boolean Insensitive,
            Char StringChar = '\'')
        {
            if (ItemsToFind == null || ItemsToFind.Count == 0)
                return null;

            OnpOnpStringFindResult minResult = null;
            foreach (Char[][] items in ItemsToFind)
            {
                if (items.Length == 0)
                    continue;

                OnpOnpStringFindResult findResult = NextIndex(Source, items, StringChar, StartIndex, EndIndex, Insensitive);
                if (findResult != null)
                {
                    if (minResult == null || findResult.Index < minResult.Index)
                        minResult = findResult;
                }
            }

            return minResult;
        }

        private static OnpOnpStringFindResult NextIndex(
            IList<Char> Source,
            Char[][] ItemsToFind,
            Char StringChar,
            Int32 StartIndex,
            Int32 EndIndex,
            Boolean Insensitive)
        {
            Boolean isDigit = true;

            for (int i = StartIndex; i < EndIndex; i++)
            {
                Char cur = Source[i];

                if (!Char.IsDigit(cur) && cur != '.')
                    isDigit = false;

                if (cur == StringChar)
                {
                    int nextIndex = -1;
                    for (int j = i + 1; j < Source.Count; j++)
                    {
                        var lTmpChar = Source[j];
                        if (Source[j] == StringChar &&
                            Source[j - 1] != '\\')
                        {
                            nextIndex = j;
                            break;
                        }
                    }
                    if (nextIndex >= 0)
                    {
                        i = nextIndex;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    char[] foundToken = null;

                    if (i == 0)
                        foundToken = StringHelper.StartsWith(Source, ItemsToFind, Insensitive);
                    else
                        foundToken = StringHelper.StartsWith(Source, ItemsToFind, i, Insensitive);

                    if (foundToken != null)
                    {
                        // jeśli poprzedni znak to cyfra to szukamy dalej
                        if (!isDigit)
                        {
                            Char? prevChar = i > 0 ? (Char?)Source[i - 1] : null;
                            Char? nextChar = i + foundToken.Length < Source.Count ? (Char?)Source[i + foundToken.Length] : null;

                            if (!(prevChar != null && Char.IsLetterOrDigit(prevChar.Value) && Char.IsLetterOrDigit(foundToken[0])) &&
                                !(nextChar != null && Char.IsLetterOrDigit(nextChar.Value) && Char.IsLetterOrDigit(foundToken[foundToken.Length - 1])))
                            {
                                return new OnpOnpStringFindResult() { Index = i, Chars = foundToken };
                            }
                        }
                    }
                }
            }
            return null;
        }
    }

    public static class OnpConverterHelper
    {
        public static DateTime? ToDatetime(Object val)
        {
            if (val != null)
            {
                if (val.GetType() == typeof(TimeSpan) || val.GetType() == typeof(TimeSpan?))
                    return new DateTime().AddSeconds(((TimeSpan)val).TotalSeconds);
                else if (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?))
                    return (DateTime)val;
                else if (val.GetType() == typeof(String))
                    return UniConvert.ToDateTime((String)val);
            }
            return null;
        }

        public static TimeSpan? ToTimeSpan(Object val)
        {
            if (val != null)
            {
                if (val.GetType() == typeof(TimeSpan) || val.GetType() == typeof(TimeSpan?))
                    return (TimeSpan)val;
                else if (val.GetType() == typeof(DateTime) || val.GetType() == typeof(DateTime?))
                    return ((DateTime)val).TimeOfDay;
                else if (val.GetType() == typeof(String))
                    return GetTimeFromtext((String)val);
            }
            return null;
        }

        public static String Join(
            this IEnumerable<Object> Items,
            String Separator = ",")
        {
            StringBuilder str = new StringBuilder();
            if (Items != null)
            {
                foreach (Object item in Items)
                {
                    if (str.Length > 0) str.Append(Separator);
                    str.Append(UniConvert.ToString(item));
                }
            }
            return str.ToString();
        }

        //////////////////////////////////////////////////////////

        private static TimeSpan GetTimeFromtext(String Text)
        {
            Text = (Text ?? "").Trim();

            String numbers = "";
            foreach (var ch in Text)
            {
                if (Char.IsNumber(ch))
                    numbers += ch;
            }

            if (numbers.Length == 1)
                numbers = "0" + numbers;

            if (numbers.Length == 3)
                numbers = "0" + numbers;

            while (numbers.Length < 4)
                numbers += "0";

            Int32 hour = Convert.ToInt32(numbers.Substring(0, 2));
            Int32 minute = Convert.ToInt32(numbers.Substring(2, 2));

            if (minute > 59)
                minute = 59;

            if (hour > 23)
                hour = 23;

            return new TimeSpan(hour, minute, 0);
        }
    }
}