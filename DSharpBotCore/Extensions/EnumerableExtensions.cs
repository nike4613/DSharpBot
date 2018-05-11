using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSharpBotCore.Extensions
{
    public static class EnumerableExtensions
    {
        public static string MakeReadableString<T>(this IEnumerable<T> self)
        {
            string str = "";
            int length = self.Count();

            if (length == 1)
                str = $"{self.First()}";
            else if (length == 2)
                str = $"{self.ElementAt(0)} and {self.ElementAt(1)}";
            else
                for (int i = 0; i < length; i++)
                    if (i == length - 1)
                        str += $"and {self.ElementAt(i)}";
                    else
                        str += $"{self.ElementAt(i)}, ";

            return str;
        }
    }
}
