using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DynLan.Helpers
{
    public static class RefUnsensitiveHelper
    {
        private static RefHelperBase _i;

        private static Object lck = new Object();

        public static RefHelperBase I
        {
            get
            {
                if (_i == null)
                {
                    lock (lck)
                    {
                        if (_i == null)
                        {
                            _i = new RefHelperBase(true);
                        }
                    }
                }
                return _i;
            }
        }
    }
}
