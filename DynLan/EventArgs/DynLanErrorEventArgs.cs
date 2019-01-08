using DynLan.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.EventArgs
{
    public class DynLanErrorEventArgs : System.EventArgs
    {
        public DynLanContext Context { get; set; }

        public DynLanProgram Program { get; set; }

        public DynLanState State { get; set; }

        ////////////////////////////////////////

        public Exception Error { get; set; }

        public Boolean Handled { get; set; }
        
        ////////////////////////////////////////

        public void Clean()
        {
            Error = null;
            Context = null;
            Program = null;
            State = null;
        }
    }
}
