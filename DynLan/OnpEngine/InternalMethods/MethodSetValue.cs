using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections;

using DynLan;
using DynLan.OnpEngine.Models;
using DynLan.Classes;
using DynLan.Helpers;

namespace DynLan.OnpEngine.InternalMethods
{
    public static class MethodSetValue
    {
        public static readonly String Name = "__MET_SET";

        public static readonly Char[] NameChars = Name.ToCharArray();

        ////////////////////////////////////////////////////////////////////////

        public static ExpressionMethodResult Execute(DynLanContext EvaluateContext, IList<Object> Parameters)
        {
            String variableName = UniConvert.ToUniString(Parameters != null && Parameters.Count > 0 ? Parameters[0] : null);
            Object value = Parameters != null && Parameters.Count > 1 ? Parameters[1] : null;

            if (EvaluateContext != null)
            {
                Boolean isValueSet = EvaluateContext.SetValue(
                    EvaluateContext,
                    variableName,
                    value);

                if (isValueSet)
                    return new ExpressionMethodResult(value);
            }
            return null;
        }
    }
}