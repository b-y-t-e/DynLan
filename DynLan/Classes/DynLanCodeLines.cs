using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Collections.ObjectModel;

namespace DynLan.Classes
{
    public class DynLanCodeLines : ObservableCollection<DynLanCodeLine>
    {
        // buforowanie ID -> linia; kolekcja jest wypełniana raz podczas kompilacji
        // i później już nie modyfikowana, więc cache jest zawsze aktualny
        private Dictionary<Guid, DynLanCodeLine> _idCache;

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
            if (_idCache == null || _idCache.Count != this.Count)
            {
                Dictionary<Guid, DynLanCodeLine> cache = new Dictionary<Guid, DynLanCodeLine>();
                foreach (DynLanCodeLine item in this)
                    if (item != null && !cache.ContainsKey(item.ID))
                        cache[item.ID] = item;
                _idCache = cache;
            }

            DynLanCodeLine result = null;
            _idCache.TryGetValue(ID, out result);
            return result;
        }
    }
}
