using DynLan.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace System.Linq
{
    public static class Linq
    {
        public static Linq<T> From<T>(IEnumerable<T> Items)
        {
            return new Linq<T>(Items);
        }

        public static Boolean Contains<T>(IEnumerable<T> Items, T Item)
        {
#if !NET20
            return Items.Contains(Item);
#else
            foreach (var item in Items)
                if (item.Equals(Item))
                    return true;
            return false;
#endif
        }

        public static Boolean Any<T>(IEnumerable<T> Items)
        {
#if !NET20
            return Items.Any();
#else
            foreach (var item in Items)
                return true;
            return false;
#endif
        }

        public static Dictionary<K, V> ToDictionary<T, K, V>(IEnumerable<T> Items, Func<T, K> KeyGetter, Func<T, V> ValueGetter)
        {
#if !NET20
            return Items.ToDictionary(KeyGetter, ValueGetter);
#else
            Dictionary<K, V> dict = new Dictionary<K, V>();
            foreach (var item in Items)
                dict[KeyGetter(item)] = ValueGetter(item);
            return dict;
#endif
        }

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
            where R : IComparable
        {
            List<T> list = new List<T>();
            foreach (T item in Items)
                list.Add(item);

            list.Sort((v1, v2) => { return -1 * Condition(v1).CompareTo(Condition(v2)); });

            return list;
        }
    }

    public class Linq<T>
    {
        public List<T> Items;

        public Linq()
        {
            this.Items = new List<T>();
        }

        public Linq(IEnumerable<T> Items)
        {
            this.Items = Items == null ? new List<T>() : new List<T>(Items);
        }

        public Linq(IEnumerable InItems)
        {
            this.Items = new List<T>();
            if (InItems != null)
                foreach (var item in InItems)
                    this.Items.Add((T)item);
        }

        public Linq<T> From(IEnumerable InItems)
        {
            this.Items = new List<T>();
            if (InItems != null)
                foreach (var item in InItems)
                    this.Items.Add((T)item);
            return this;
        }

        public Boolean Any(Func<T, Boolean> Condition)
        {
            if (Items == null || Condition == null)
                return false;

            foreach (T item in Items)
                if (Condition(item))
                    return true;

            return false;
        }

        public Boolean Contains(T Item)
        {
            if (Items == null)
                return false;

            foreach (T item in Items)
                if (item.Equals(Item))
                    return true;

            return false;
        }

        public int Count()
        {
            return Items.Count;
        }

        public T[] ToArray()
        {
            return Items.ToArray();
        }

        public List<T> ToList()
        {
            return new List<T>(Items);
        }

        public Linq<R> Select<R>(Func<T, R> Condition)
        {
            List<R> newItems = new List<R>();
            foreach (T item in this.Items)
                newItems.Add(Condition(item));

            Linq<R> newLinq = new Linq<R>(newItems);
            return newLinq;
        }

        public Dictionary<K, V> ToDictionary<K, V>(Func<T, K> KeyGetter, Func<T, V> ValueGetter)
        {
            Dictionary<K, V> dict = new Dictionary<K, V>();
            foreach (var item in Items)
                dict[KeyGetter(item)] = ValueGetter(item);
            return dict;
        }

        public Linq<R> SelectMany<R>(Func<T, IEnumerable<R>> Condition)
        {
            List<R> newItems = new List<R>();
            foreach (T item in this.Items)
                foreach (R innerItem in Condition(item))
                    newItems.Add(innerItem);

            Linq<R> newLinq = new Linq<R>(newItems);
            return newLinq;
        }

        public Linq<T> Until(Func<T, Boolean> Condition)
        {
            List<T> newItems = new List<T>();
            foreach (T item in this.Items)
            {
                if (Condition(item))
                    break;
                newItems.Add(item);
            }

            Linq<T> newLinq = new Linq<T>(newItems);
            return newLinq;
        }

        private Func<T, IComparable> orderByFunc;

        public Linq<T> OrderBy(Func<T, IComparable> Condition)
        {
            this.orderByFunc = Condition;
            if (this.orderByFunc != null)
                this.Items.Sort(Sort);

            return this;
        }

        public Linq<T> OrderByDescending(Func<T, IComparable> Condition)
        {
            this.orderByFunc = Condition;
            if (this.orderByFunc != null)
                this.Items.Sort(SortDesc);

            return this;
        }

        Int32 Sort(T i1, T t2)
        {
            return orderByFunc(i1).CompareTo(orderByFunc(t2));
        }

        Int32 SortDesc(T i1, T t2)
        {
            return -1 * orderByFunc(i1).CompareTo(orderByFunc(t2));
        }

        public T FirstOrDefault()
        {
            foreach (T item in Items)
                return item;

            return default(T);
        }

        public T FirstOrDefault(Func<T, Boolean> Condition)
        {
            if (Condition == null)
                return default(T);

            foreach (T item in Items)
                if (Condition(item))
                    return item;

            return default(T);
        }

        public T LastOrDefault()
        {
            T result = default(T);

            if (Items != null && Items.Count > 0)
                result = Items[Items.Count - 1];

            return result;
        }

        public T LastOrDefault(Func<T, Boolean> Condition)
        {
            if (Condition == null)
                return default(T);

            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (Condition(item))
                    return item;
            }

            return default(T);
        }

        public Linq<T> Where(Func<T, Boolean> Condition)
        {
            Linq<T> result = new Linq<T>();

            if (Condition == null)
                return result;

            foreach (T item in Items)
                if (Condition(item))
                    result.Items.Add(item);

            return result;
        }

        public Linq<T> Distinct()
        {
            Linq<T> result = new Linq<T>();

            foreach (T item in Items)
                if (!result.Items.Contains(item))
                    result.Items.Add(item);

            return result;
        }

        public String Concat(String Separator)
        {
            return Concat(Separator, null);
        }

        public String Concat(Func<T, String> ConcatFunction)
        {
            return Concat(",", ConcatFunction);
        }

        public String Concat(String Separator, Func<T, String> ConcatFunction)
        {
            StringBuilder str = new StringBuilder();

            var index = -1;
            foreach (T item in Items)
            {
                index++;
                if (index > 0) str.Append(Separator);

                if (ConcatFunction == null)
                    str.Append(UniConvert.ToString(item));
                else
                    str.Append(ConcatFunction(item));
            }

            return str.ToString();
        }
    }
}
