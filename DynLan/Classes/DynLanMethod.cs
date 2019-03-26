using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Runtime.Serialization;

namespace DynLan.Classes
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class DynLanMethod : DynLanProgram
    {
#if !NET20
        [DataMember]
#endif
        public String Name { get; set; }

#if !NET20
        [DataMember]
#endif
        public List<String> Parameters { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanObject ParentObject { get; set; }

        //////////////////////////////////////////////////

        public DynLanMethod()
        {
            Name = "";
            Parameters = new List<String>();
            ContextType = DynLanContextType.METHOD;
        }

        //////////////////////////////////////////////////

        public override Object Clone()
        {
            DynLanMethod item = base.Clone() as DynLanMethod;
            if (item.Parameters != null)
                item.Parameters = new List<String>(item.Parameters);
            return item;
        }
    }
}
