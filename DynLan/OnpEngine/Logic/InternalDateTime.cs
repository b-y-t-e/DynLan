using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace DynLan.OnpEngine.Logic
{
    public class InternalDateTime
    {
        public Int64 Ticks { get; set; }

        //////////////////////////////////

        public InternalDateTime(Int64 Ticks)
        {
            this.Ticks = Ticks;
        }

        public InternalDateTime(DateTime DateTime)
        {
            this.Ticks = DateTime.Ticks;
        }

        //////////////////////////////////

        public DateTime ToDateTime()
        {
            if (this.Ticks > 0)
                return new DateTime(this.Ticks);
            return new DateTime();
        }

        public Int32 CompareTo(InternalDateTime Other)
        {
            return this.Ticks.CompareTo(Other.Ticks);
        }
    }
}