using DynLan.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.OnpEngine.Logic
{
    public static class InternalTypeConverter
    {
        public static Object ToInner(Object Value)
        {
            if (Value != null && Value.GetType().IsDateTime())
            {
                return new InternalDateTime(((DateTime)Value).Ticks);
            }
            return Value;
        }

        public static Object ToOuter(Object Value)
        {
            if (Value != null && Value.GetType() == typeof(InternalDateTime))
            {
                return ((InternalDateTime)Value).ToDateTime();
            }
            return Value;
        }
    }
}