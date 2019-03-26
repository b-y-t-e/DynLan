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
    public class DynLanClass : DynLanMethod
    {
        /*public String ClassName { get; set; }

        public List<String> Parameters { get; set; }*/

        //////////////////////////////////////////////////

        public DynLanClass()
            : base()
        {
            /*ClassName = "";
            Parameters = new List<String>();
            ContextType = DynLanLocalContextType.LOCAL;*/
            this.ParentObject = null;
        }

        //////////////////////////////////////////////////

    }
}
