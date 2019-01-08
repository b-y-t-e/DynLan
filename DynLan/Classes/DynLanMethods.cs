using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;

namespace DynLan.Classes
{
    public class DynLanMethods : List<DynLanMethod>
    {
        public DynLanMethods()
        {

        }

        public DynLanMethods(IEnumerable<DynLanMethod> Methods)
        {
            if (Methods == null)
                return;

            this.AddRange(Methods);
        }

        ////////////////////////////////////////

        public DynLanMethod By_ID(Guid ID)
        {
            return this.FirstOrDefault(i => i.ID == ID);
        }

        public DynLanMethod By_Name(String Name)
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
