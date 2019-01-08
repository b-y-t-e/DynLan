using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace DynLan.Classes
{
    [DataContract(IsReference = true)]
    public class EmptyObject 
    {
        public EmptyObject()
        {

        }

        public override bool Equals(object obj)
        {
            return obj is EmptyObject;
        }
    }
}
