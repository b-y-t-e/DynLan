using DynLan.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace System.Linq
{
    public delegate void Action<P1, P2>(P1 param1, P2 param2);

    public delegate void Action<P1, P2, P3>(P1 param1, P2 param2, P3 param3);

    public delegate T Func<T>();

    public delegate T Func<P1, T>(P1 param1);

    public delegate T Func<P1, P2, T>(P1 param1, P2 param2);

    public delegate T Func<P1, P2, P3, T>(P1 param1, P2 param2, P3 param3);
}
