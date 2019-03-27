using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.Extenders
{
    public static class BooleanHelper
    {
        public static Boolean IfTrue(
#if !NET20
            this 
#endif
             Object result)
        {
            return !IfFalse(result);
        }

        public static Boolean IfFalse(
#if !NET20
            this 
#endif
             Object result)
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

        public static Boolean IsTrue(
#if !NET20
            this 
#endif
             Object result)
        {
            return IfTrue(result);
        }

        public static Boolean IsFalse(
#if !NET20
            this 
#endif
             Object result)
        {
            return IfFalse(result);
        }
    }
}
