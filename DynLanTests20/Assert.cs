using System;
using System.Collections.Generic;
using System.Text;

public static class Assert
{
    public static bool AreEqual(Object V1, Object V2)
    {
        if (V1 == null && V2 != null)
            return false;

        if (V1 != null && V2 == null)
            return false;

        if (V1 == null && V2 == null)
            return true;

        if (V1.Equals(V2))
            return true;

        return false;
    }
}
