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
            return this.FirstOrDefault(i => i.ID == ID);
        }

        public DynLanClass By_Name(String Name)
        {
            return this.FirstOrDefault(i => i.Name == Name);
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
