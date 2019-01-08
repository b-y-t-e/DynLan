using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Runtime.Serialization;

namespace DynLan.Classes
{
    [DataContract(IsReference = true)]
    public class DynLanMethod : DynLanProgram
    {
        [DataMember]
        public String Name { get; set; }

        [DataMember]
        public List<String> Parameters { get; set; }

        [DataMember]
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
