using DynLan.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.EventArgs
{
    public class DynLanProgramChangedEventArgs : System.EventArgs
    {
        public DynContext Context { get; set; }

        public DynLanProgram Program { get; set; }

        public DynLanState State { get; set; }

        public IList<DynLanMethodParam> Parameters { get; set; }

        public Exception Error { get; set; }

        ////////////////////////////////////////

        public void Clean()
        {
            Context = null;
            Program = null;
            State = null;
            Parameters = null;
            Error = null;
        }
    }

    public class DynLanMethodParam
    {
        public String Name { get; set; }

        public Object Value { get; set; }

        public DynLanMethodParam Clone()
        {
            return (DynLanMethodParam)this.MemberwiseClone();
        }
    }
}
