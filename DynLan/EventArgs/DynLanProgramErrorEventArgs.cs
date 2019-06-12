using DynLan.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.EventArgs
{
    public class DynLanProgramErrorEventArgs : System.EventArgs
    {
        public DynContext Context { get; set; }

        public DynLanProgram Program { get; set; }

        public DynLanState State { get; set; }
        
        ////////////////////////////////////////

        public void Clean()
        {
            Context = null;
            Program = null;
            State = null;
        }
    }
}
