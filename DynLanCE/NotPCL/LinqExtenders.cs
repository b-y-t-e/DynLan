using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Linq2
{
    public static class LinqExtenders
    {
        public static List<Char> ToList(
#if !NET20
            this 
#endif
             String Item)
        {
            if (Item == null)
                return null;

            List<Char> items = new List<Char>(Item.Length);
            foreach (Char ch in Item)
                items.Add(ch);
            return items;
        }

        public static Char[] ToArray(
#if !NET20
            this 
#endif
             String Item)
        {
            if (Item == null)
                return null;

            Char[] items = new Char[Item.Length];
            Int32 i = 0;
            foreach (Char ch in Item)
                items[i++] = ch;
            return items;
        }

        public static Char[] ToArray(
#if !NET20
            this 
#endif
             List<Char> Item)
        {
            if (Item == null)
                return null;

            Char[] items = new Char[Item.Count];
            Int32 i = 0;
            foreach (Char ch in Item)
                items[i++] = ch;
            return items;
        }
    }
}
