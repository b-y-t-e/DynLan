using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace DynLan.OnpEngine.Models
{
    public class ParserSettings
    {
        public Boolean Validate { get; set; }

        public Boolean Optimize { get; set; }

        //////////////////////////////////////

        public ParserSettings()
        {
            Validate = true;
            Optimize = true;
        }
    }
}