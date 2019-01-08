using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Runtime.Serialization;

namespace DynLan.Classes
{
    [DataContract(IsReference = true)]
    public class DynLanProgram
    {
        [DataMember]
        public Guid ID { get; set; }

        //////////////////////////////////////////////

        [DataMember]
        public Int32 Depth { get; set; }

        [DataMember]
        public DynLanMethods Methods { get; set; }

        [DataMember]
        public DynLanClasses Classes { get; set; }

        [DataMember]
        public DynLanCodeLines Lines { get; set; }

        [DataMember]
        public DynLanContextType ContextType { get; set; }

        //////////////////////////////////////////////////

        [DataMember]
        public Object Tag { get; set; }

        [DataMember]
        public Object Tag1 { get; set; }

        [DataMember]
        public Object Tag2 { get; set; }

        //////////////////////////////////////////////////

        public DynLanProgram()
        {
            this.ID = Guid.NewGuid();
            this.Methods = new DynLanMethods();
            this.Classes = new DynLanClasses();
            this.Lines = new DynLanCodeLines();
            this.ContextType = DynLanContextType.GLOBAL;
        }

        //////////////////////////////////////////////////

        public virtual Object Clone()
        {
            DynLanProgram item = (DynLanProgram)this.MemberwiseClone();
            if (item.Lines != null)
                item.Lines = new DynLanCodeLines(item.Lines.Select(i => i.Clone()));
            if (item.Classes != null)
                item.Classes = new DynLanClasses(item.Classes.Select(i => i.Clone() as DynLanClass));
            if (item.Methods != null)
                item.Methods = new DynLanMethods(item.Methods.Select(i => i.Clone() as DynLanMethod));
            return item;
        }
    }
}
