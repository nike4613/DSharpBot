using System;
using System.Collections.Generic;
using System.Text;

namespace DSharpBotCore.Entities
{
    class EasyAddList<T> : List<T>
    {
        public EasyAddList() : base() { }

        public EasyAddList(List<T> old) : base(old) { }

        public EasyAddList(int cap) : base(cap) { }

        public static EasyAddList<T> operator +(EasyAddList<T> self, T item)
        {
            self.Add(item);
            return self;
        }

        public static EasyAddList<T> operator -(EasyAddList<T> self, T item)
        {
            self.Remove(item);
            return self;
        }
    }
}
