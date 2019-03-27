#define NOT_CLASSIC_REFLECTION

using System;
using System.Collections;
using System.Reflection;
//using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.ObjectModel;

namespace DynLan.Helpers
{
    public static class MyCollectionsExtenders
    {
        public static T Peek<T>(
#if !NET20
            this 
#endif
             IList<T> Items, Int32 Index)
        {
            return Items.Count > 0 + Index ?
                Items[Items.Count - 1 - Index] :
                default(T);
        }
        
        public static T Peek<T>(
#if !NET20
            this 
#endif
             IList<T> Items)
        {
            return Items.Count > 0 + 0 ?
                Items[Items.Count - 1 - 0] :
                default(T);
        }

        public static void Push<T>(
#if !NET20
            this 
#endif
             IList<T> Items, T Item)
        {
            Items.Add(Item);
        }

        public static T Pop<T>(
#if !NET20
            this 
#endif
             IList<T> Items)
        {
            if (Items.Count == 0)
                return default(T);

            T item = Items[Items.Count - 1];
            Items.RemoveAt(Items.Count - 1);
            return item;
        }

        /*public static void AddRange<T>(
#if !NET20
            this 
#endif
             ObservableCollection<T> Items, IEnumerable<T> ItemsToAdd)
        {
            if (ItemsToAdd == null)
                return;

            foreach (T item in ItemsToAdd)
                Items.Add(item);
        }

        public static void Remove<T>(
#if !NET20
            this 
#endif
             IList<T> Items, IEnumerable<T> ItemsToRemove)
        {
            if (Items == null || ItemsToRemove == null)
                return;

            foreach (T item in ItemsToRemove)
                Items.Remove(item);
        }*/
    }
}
