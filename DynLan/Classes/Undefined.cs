using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace DynLan.Classes
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class Undefined 
    {
        public Undefined()
        {

        }

        public override bool Equals(object obj)
        {
            return obj is Undefined;
        }
    }
}
