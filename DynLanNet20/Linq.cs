using System;
using System.Collections.Generic;
using System.Text;

namespace System.Linq
{
    public static class Linq
    {
        public static T LastOrDefault<T>(IEnumerable<T> Items)
        {
            T lastItem = default(T);
            foreach (T item in Items)
                lastItem = item;
            return lastItem;
        }

        public static T LastOrDefault<T>(IList<T> Items)
        {
            T lastItem = default(T);
            if (Items != null && Items.Count > 0)
                lastItem = Items[Items.Count - 1];
            return lastItem;
        }

        public static T FirstOrDefault<T>(IEnumerable<T> Items)
        {
            foreach (T item in Items)
                return item;
            return default(T);
        }

        public static T FirstOrDefault<T>(IEnumerable<T> Items, Func<T, Boolean> Condition)
        {
            foreach (T item in Items)
                if (Condition(item))
                    return item;
            return default(T);
        }

        public static IEnumerable<R> Select<T, R>(IEnumerable<T> Items, Func<T, R> Condition)
        {
            foreach (T item in Items)
                yield return Condition(item);
        }

        public static IEnumerable<T> OrderBy<T, R>(IEnumerable<T> Items, Func<T, R> Condition)
            where R : IComparable<R>
        {
            List<T> list = new List<T>();
            foreach (T item in Items)
                list.Add(item);

            list.Sort((v1, v2) => { return Condition(v1).CompareTo(Condition(v2)); });

            return list;
        }

        public static IEnumerable<T> OrderByDescending<T, R>(IEnumerable<T> Items, Func<T, R> Condition)
            where R : IComparable<R>
        {
            List<T> list = new List<T>();
            foreach (T item in Items)
                list.Add(item);

            list.Sort((v1, v2) => { return -1 * Condition(v1).CompareTo(Condition(v2)); });

            return list;
        }
    }

    public delegate void Action<P1, P2>(P1 param1, P2 param2);

    public delegate void Action<P1, P2, P3>(P1 param1, P2 param2, P3 param3);

    public delegate T Func<T>();

    public delegate T Func<P1, T>(P1 param1);

    public delegate T Func<P1, P2, T>(P1 param1, P2 param2);

    public delegate T Func<P1, P2, P3, T>(P1 param1, P2 param2, P3 param3);
}
