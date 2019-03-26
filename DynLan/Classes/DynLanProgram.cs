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
    public class DynLanProgram
    {
#if !NET20
        [DataMember]
#endif
        public Guid ID { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public Int32 Depth { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanMethods Methods { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanClasses Classes { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanCodeLines Lines { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanContextType ContextType { get; set; }

        //////////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public Object Tag { get; set; }

#if !NET20
        [DataMember]
#endif
        public Object Tag1 { get; set; }

#if !NET20
        [DataMember]
#endif
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
#if !NET20
            if (item.Lines != null)
                item.Lines = new DynLanCodeLines(item.Lines.Select(i => i.Clone()));
            if (item.Classes != null)
                item.Classes = new DynLanClasses(item.Classes.Select(i => i.Clone() as DynLanClass));
            if (item.Methods != null)
                item.Methods = new DynLanMethods(item.Methods.Select(i => i.Clone() as DynLanMethod));
      
#else
            if (item.Lines != null)
                item.Lines = new DynLanCodeLines(Linq.Select(item.Lines, i => i.Clone()));
            if (item.Classes != null)
                item.Classes = new DynLanClasses(Linq.Select(item.Classes, i => i.Clone() as DynLanClass));
            if (item.Methods != null)
                item.Methods = new DynLanMethods(Linq.Select(item.Methods, i => i.Clone() as DynLanMethod));

#endif

            return item;
        }
    }
}
