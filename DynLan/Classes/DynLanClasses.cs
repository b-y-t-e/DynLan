using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;

namespace DynLan.Classes
{
    public class DynLanClasses : List<DynLanClass>
    {
        public DynLanClasses()
        {

        }

        public DynLanClasses(IEnumerable<DynLanClass> Items)
        {
            if (Items == null)
                return;

            this.AddRange(Items);
        }

        ////////////////////////////////////////

        public DynLanClass By_ID(Guid ID)
        {
#if !NET20
            return this.FirstOrDefault(i => i.ID == ID);
#else
            return Linq.FirstOrDefault(this, i => i.ID == ID);
#endif
        }

        public DynLanClass By_Name(String Name)
        {
#if !NET20
            return this.FirstOrDefault(i => i.Name == Name);
#else
            return Linq.FirstOrDefault(this, i => i.Name == Name);
#endif
        }

        public void Remove_by_Name(String Name)
        {
            for (var i = 0; i < this.Count; i++)
            {
                if (this[i].Name == Name)
                {
                    this.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
