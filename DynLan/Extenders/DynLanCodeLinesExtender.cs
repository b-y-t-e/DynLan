using DynLan.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.Extenders
{
    public static class DynLanCodeLinesExtender
    {
        public static DynLanCodeLine NextLine(
#if !NET20
            this 
#endif
             DynLanCodeLines Lines,
            DynLanCodeLine StartLine)
        {
            Int32 depth = (StartLine == null ? 0 : StartLine.Depth);
            Int32 index = (StartLine == null ? 0 : Lines.IndexOf(StartLine));

            if (index < 0)
                return null;

            for (var i = index + 1; i < Lines.Count; i++)
            {
                DynLanCodeLine line = Lines[i];
                if (!line.IsLineEmpty)
                    return line;
            }

            return null;
        }

        public static DynLanCodeLine PrevLineWithLessDepth(
#if !NET20
            this 
#endif
             DynLanCodeLines Lines,
            DynLanCodeLine StartLine,
            Func<DynLanCodeLine, Boolean> Predicate)
        {
            Int32 depth = (StartLine == null ? 0 : StartLine.Depth);
            Int32 index = (StartLine == null ? 0 : Lines.IndexOf(StartLine));

            if (index < 0)
                return null;

            for (var i = index - 1; i >= 0; i--)
            {
                DynLanCodeLine line = Lines[i];
                if (!line.IsLineEmpty && line.Depth < depth)
                    if (Predicate == null || Predicate(line))
                        return line;
            }

            return null;
        }

        public static DynLanCodeLine ExitParentIf(
#if !NET20
            this 
#endif
             DynLanCodeLines Lines,
            DynLanCodeLine StartLine)
        {
            Int32 depth = (StartLine == null ? 0 : StartLine.Depth);
            Int32 index = (StartLine == null ? 0 : Lines.IndexOf(StartLine));

            if (index < 0)
                return null;

            DynLanCodeLine line = StartLine;
            while (true)
            {
                line = NextLine(
                    Lines,
                    line);

                if (line == null)
                    break;

                if (line.Depth < StartLine.Depth)
                    return line;

                if (line.Depth == StartLine.Depth &&
                    line.OperatorType != EOperatorType.ELIF &&
                    line.OperatorType != EOperatorType.ELSE)
                {
                    return line;
                }
            }

            return null;
        }
        
        public static DynLanCodeLine NextOnSameOrLower(
#if !NET20
            this 
#endif
            DynLanCodeLines Lines,
            DynLanCodeLine StartLine)
        {
            return NextOnSameOrLower(Lines, StartLine, null);
        }

        public static DynLanCodeLine NextOnSameOrLower(
#if !NET20
            this 
#endif
            DynLanCodeLines Lines,
            DynLanCodeLine StartLine,
            Func<DynLanCodeLine, Boolean> Predicate)
        {
            Int32 depth = (StartLine == null ? 0 : StartLine.Depth);
            Int32 index = (StartLine == null ? 0 : Lines.IndexOf(StartLine));

            if (index < 0)
                return null;

            for (var i = index + 1; i < Lines.Count; i++)
            {
                DynLanCodeLine line = Lines[i];
                if (!line.IsLineEmpty && line.Depth <= depth)
                    if (Predicate == null || Predicate(line))
                        return line;
            }

            return null;
        }

        public static DynLanCodeLine NextOnSameOrHigher(
#if !NET20
            this 
#endif
             DynLanCodeLines Lines,
            DynLanCodeLine StartLine)
        {
            Int32 depth = (StartLine == null ? 0 : StartLine.Depth);
            Int32 index = (StartLine == null ? 0 : Lines.IndexOf(StartLine));

            if (index < 0)
                return null;

            for (var i = index + 1; i < Lines.Count; i++)
            {
                DynLanCodeLine line = Lines[i];
                if (!line.IsLineEmpty)
                    if (line.Depth > depth)
                        return line;
                    else if (line.Depth == depth)
                        return line;
            }

            return null;
        }
    }
}
