using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
#if PCL
using System.Collections.ObjectModel2;
#else
using System.Collections.ObjectModel;
#endif

namespace DynLan.Classes
{
    public class DynLanCodeLines : ObservableCollection<DynLanCodeLine>
    {
        public DynLanCodeLines()
        {

        }

        public DynLanCodeLines(IEnumerable<DynLanCodeLine> Items)
        {
            if (Items == null)
                return;

            foreach (DynLanCodeLine item in Items)
                this.Add(item);
        }

        ////////////////////////////////////////

        public DynLanCodeLine Get_by_ID(Guid ID)
        {
#if !NET20
            return this.FirstOrDefault(i => i.ID == ID);
#else
            return Linq2.FirstOrDefault(this, i => i.ID == ID);
#endif

        }
    }
}
