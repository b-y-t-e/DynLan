using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.Extenders
{
    public static class ObjectExtender
    {
        public static Boolean IfTrue(this Object result)
        {
            return !IfFalse(result);
        }

        public static Boolean IfFalse(this Object result)
        {
            return 
                null == result || 
                (0).Equals(result) ||
                (0M).Equals(result) ||
                (0.0f).Equals(result) ||
                (0.0D).Equals(result) || 
                ("").Equals(result) || 
                (false).Equals(result);
        }

        public static Boolean IsTrue(this Object result)
        {
            return IfTrue(result);
        }

        public static Boolean IsFalse(this Object result)
        {
            return IfFalse(result);
        }
    }
}
