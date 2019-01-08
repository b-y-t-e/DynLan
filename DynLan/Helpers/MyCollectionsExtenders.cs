#define NOT_CLASSIC_REFLECTION

using System;
using System.Collections;
using System.Reflection;
//using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if PCL
using System.Collections.ObjectModel2;
#else
using System.Collections.ObjectModel;
#endif

namespace DynLan.Helpers
{
    public static class MyCollectionsExtenders
    {
        public static T Peek<T>(this IList<T> Items, Int32 Index = 0)
        {
            return Items.Count > 0 + Index ?
                Items[Items.Count - 1 - Index] :
                default(T);
        }

        public static void Push<T>(this IList<T> Items, T Item)
        {
            Items.Add(Item);
        }

        public static T Pop<T>(this IList<T> Items)
        {
            if (Items.Count == 0)
                return default(T);

            T item = Items[Items.Count - 1];
            Items.RemoveAt(Items.Count - 1);
            return item;
        }

        public static void AddRange<T>(this ObservableCollection<T> Items, IEnumerable<T> ItemsToAdd)
        {
            if (ItemsToAdd == null)
                return;

            foreach (T item in ItemsToAdd)
                Items.Add(item);
        }

        public static void Remove<T>(this IList<T> Items, IEnumerable<T> ItemsToRemove)
        {
            if (Items == null || ItemsToRemove == null)
                return;

            foreach (T item in ItemsToRemove)
                Items.Remove(item);
        }
    }
}
