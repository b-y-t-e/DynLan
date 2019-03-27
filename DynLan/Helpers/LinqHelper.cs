using DynLan.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace System.Linq
{
    public static class Linq2
    {
        public static Linq2<T> From<T>(IEnumerable<T> Items)
        {
            return new Linq2<T>(Items);
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

        public static Boolean Any<T>(IEnumerable<T> Items, Func<T, Boolean> Condition)
        {
#if !NET20
            return Items.Any(Condition);
#else
            foreach (var item in Items)
                if(Condition(item))
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

        public static List<T> ToList<T>(IEnumerable<T> Items)
        {
#if !NET20
            return new List<T>(Items);
#else
            return new List<T>(Items);
#endif
        }

        public static T[] ToArray<T>(IEnumerable<T> Items)
        {
#if !NET20
            return Items.ToArray();
#else
            int i=0;
            foreach( var item in Items)
                i++;
            var r = new T[i];
            i=0;
            foreach( var item in Items)            
                r[i++] = item;            
            return r;
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

    public class Linq2<T>
    {
        public List<T> Items;

        public Linq2()
        {
            this.Items = new List<T>();
        }

        public Linq2(IEnumerable<T> Items)
        {
            this.Items = Items == null ? new List<T>() : new List<T>(Items);
        }

        public Linq2(IEnumerable InItems)
        {
            this.Items = new List<T>();
            if (InItems != null)
                foreach (var item in InItems)
                    this.Items.Add((T)item);
        }

        public Linq2<T> From(IEnumerable InItems)
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

        public Linq2<R> Select<R>(Func<T, R> Condition)
        {
            List<R> newItems = new List<R>();
            foreach (T item in this.Items)
                newItems.Add(Condition(item));

            Linq2<R> newLinq = new Linq2<R>(newItems);
            return newLinq;
        }

        public Dictionary<K, V> ToDictionary<K, V>(Func<T, K> KeyGetter, Func<T, V> ValueGetter)
        {
            Dictionary<K, V> dict = new Dictionary<K, V>();
            foreach (var item in Items)
                dict[KeyGetter(item)] = ValueGetter(item);
            return dict;
        }

        public Linq2<R> SelectMany<R>(Func<T, IEnumerable<R>> Condition)
        {
            List<R> newItems = new List<R>();
            foreach (T item in this.Items)
                foreach (R innerItem in Condition(item))
                    newItems.Add(innerItem);

            Linq2<R> newLinq = new Linq2<R>(newItems);
            return newLinq;
        }

        public Linq2<T> Union(IEnumerable<T> OtherItems)
        {
            Linq2<T> newItems = new Linq2<T>();
            foreach (T item in this.Items)
                newItems.Items.Add(item);
            foreach (T item in OtherItems)
                newItems.Items.Add(item);
            return newItems;
        }

        public Linq2<T> Union(Linq2<T> OtherItems)
        {
            Linq2<T> newItems = new Linq2<T>();
            foreach (T item in this.Items)
                newItems.Items.Add(item);
            foreach (T item in OtherItems.Items)
                newItems.Items.Add(item);
            return newItems;
        }

        public Linq2<T> Until(Func<T, Boolean> Condition)
        {
            List<T> newItems = new List<T>();
            foreach (T item in this.Items)
            {
                if (Condition(item))
                    break;
                newItems.Add(item);
            }

            Linq2<T> newLinq = new Linq2<T>(newItems);
            return newLinq;
        }

        private Func<T, IComparable> orderByFunc;

        public Linq2<T> OrderBy(Func<T, IComparable> Condition)
        {
            this.orderByFunc = Condition;
            if (this.orderByFunc != null)
                this.Items.Sort(Sort);

            return this;
        }

        public Linq2<T> OrderByDescending(Func<T, IComparable> Condition)
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

        public Linq2<T> Where(Func<T, Boolean> Condition)
        {
            Linq2<T> result = new Linq2<T>();

            if (Condition == null)
                return result;

            foreach (T item in Items)
                if (Condition(item))
                    result.Items.Add(item);

            return result;
        }

        public Linq2<T> Distinct()
        {
            Linq2<T> result = new Linq2<T>();

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
