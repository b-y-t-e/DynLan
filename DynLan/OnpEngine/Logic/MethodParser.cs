using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace DynLan.OnpEngine.Logic
{
    public class OnpMethodPart
    {
        public EOnpMethodPart Part;

        public String Code;
    }

    public enum EOnpMethodPart
    {
        METHOD_NAME,
        BRACKET,
        PARAMETER,
        COMA
    }

    public static class MethodParser
    {
        public static IList<OnpMethodPart> ExtractNames(String Code)
        {
            return ExtractNames(Code, false);
        }

        public static IList<OnpMethodPart> ExtractNames(String Code, Boolean AllObjects)
        {
            List<OnpMethodPart> parts = new List<OnpMethodPart>();

            Code = Code.Trim();
            Int32 indexOfBracketStart = Code.IndexOf("(");
            Int32 indexOfBracketEnd = Code.LastIndexOf(")");

            Int32 indexOfMethodInvoke = indexOfBracketStart >= 0 ?
                Code.Substring(0, indexOfBracketStart).LastIndexOf(".") :
                Code.IndexOf(".");

            // variable invoke
            if (indexOfBracketStart < 0 && indexOfBracketEnd < 0)
            {
                String propertyPath = Code;

                parts.Add(new OnpMethodPart()
                {
                    Part = propertyPath == "," ? EOnpMethodPart.COMA : EOnpMethodPart.PARAMETER,
                    Code = propertyPath
                });
            }
            // method for object invoke
            else if (indexOfMethodInvoke >= 0 && indexOfBracketStart >= 0 && indexOfBracketEnd >= 0)
            {
                String propertyPath = Code.Substring(
                    0,
                    indexOfMethodInvoke).Trim();

                parts.Add(new OnpMethodPart()
                {
                    Part = propertyPath == "," ? EOnpMethodPart.COMA : EOnpMethodPart.PARAMETER,
                    Code = propertyPath
                });

                String methodName = Code.Substring(
                    indexOfMethodInvoke + 1,
                    indexOfBracketStart - indexOfMethodInvoke - 1).Trim();

                IList<String> parameters = Linq2.ToArray(SplitMethodParameters(
                    Code.Substring(indexOfBracketStart + 1, Code.Length - indexOfBracketStart - 2), AllObjects));

                if (AllObjects)
                {
                    parts.Add(new OnpMethodPart()
                    {
                        Part = EOnpMethodPart.METHOD_NAME,
                        Code = methodName
                    });
                    parts.Add(new OnpMethodPart()
                    {
                        Part = EOnpMethodPart.BRACKET,
                        Code = "("
                    });
                }

                foreach (String parameter in parameters)
                    parts.Add(new OnpMethodPart()
                    {
                        Part = parameter == "," ? EOnpMethodPart.COMA : EOnpMethodPart.PARAMETER,
                        Code = parameter
                    });

                if (AllObjects)
                {
                    parts.Add(new OnpMethodPart()
                    {
                        Part = EOnpMethodPart.BRACKET,
                        Code = ")"
                    });
                }
            }
            // global method invoke
            else if (indexOfMethodInvoke < 0 && indexOfBracketStart >= 0 && indexOfBracketEnd >= 0)
            {
                String methodName = Code.Substring(0, indexOfBracketStart).Trim();

                IList<String> parameters = Linq2.ToArray(SplitMethodParameters(
                    //Code.Substring(indexOfBracketStart + 1, Code.Length - indexOfBracketStart - 2), AllObjects).
                    Code.Substring(indexOfBracketStart + 1, indexOfBracketEnd - indexOfBracketStart - 1), AllObjects));
                parameters = parameters;

                if (AllObjects)
                {
                    parts.Add(new OnpMethodPart()
                    {
                        Part = EOnpMethodPart.METHOD_NAME,
                        Code = methodName
                    });
                    parts.Add(new OnpMethodPart()
                    {
                        Part = EOnpMethodPart.BRACKET,
                        Code = "("
                    });
                }

                foreach (String parameter in parameters)
                    parts.Add(new OnpMethodPart()
                    {
                        Part = parameter == "," ? EOnpMethodPart.COMA : EOnpMethodPart.PARAMETER,
                        Code = parameter
                    });

                if (AllObjects)
                {
                    parts.Add(new OnpMethodPart()
                    {
                        Part = EOnpMethodPart.BRACKET,
                        Code = ")"
                    });
                }
            }

            return parts;
        }

        ////////////////////////////////////////////////////////

        public static IEnumerable<String> SplitMethodParameters(String Code)
        {
            return SplitMethodParameters(Code, false);
        }

        public static IEnumerable<String> SplitMethodParameters(String Code, Boolean AllObjects)
        {
            StringBuilder paramStr = new StringBuilder();
            Boolean isFirst = true;

            IList<String> tokens = Linq2.ToList(TokenGetter.
                GetStringTokens(Code.Trim()));

            foreach (String token in tokens)
            {
                if (token.EndsWith(","))
                {
                    if (!isFirst && AllObjects)
                        yield return ",";

                    isFirst = false;
                    paramStr.Append(token.Substring(0, token.Length - 1));
                    yield return paramStr.ToString();
                    paramStr.Remove(0, paramStr.Length);
                }
                else
                {
                    paramStr.Append(token);
                }
            }
            if (paramStr.Length > 0)
            {
                if (!isFirst && AllObjects)
                    yield return ",";

                yield return paramStr.ToString();
            }
        }

    }
}