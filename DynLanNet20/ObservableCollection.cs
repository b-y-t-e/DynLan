using System;
using System.Collections.Generic;
using System.Text;

namespace System.Collections.Generic
{
    public class ObservableCollection<T> : List<T>
    {
        public T Peek(
              Int32 Index = 0)
        {
            return this.Count > 0 + Index ?
                this[this.Count - 1 - Index] :
                default(T);
        }

        public void Push(
             T Item)
        {
            this.Add(Item);
        }

        public T Pop()
        {
            if (this.Count == 0)
                return default(T);

            T item = this[this.Count - 1];
            this.RemoveAt(this.Count - 1);
            return item;
        }

        public void AddRange( IEnumerable<T> ItemsToAdd)
        {
            if (ItemsToAdd == null)
                return;

            foreach (T item in ItemsToAdd)
                this.Add(item);
        }

        public void Remove( IEnumerable<T> ItemsToRemove)
        {
            if (this == null || ItemsToRemove == null)
                return;

            foreach (T item in ItemsToRemove)
                this.Remove(item);
        }
    }
}
