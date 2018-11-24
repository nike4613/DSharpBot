using System.Collections.Generic;
using System.Linq;

namespace DSharpBotCore.Extensions
{
    public static class EnumerableExtensions
    {
        public static string MakeReadableString<T>(this IEnumerable<T> self)
        {
            string str = "";
            var enumerable = self.ToList();
            int length = enumerable.Count();

            if (length == 1)
                str = $"{enumerable.First()}";
            else if (length == 2)
                str = $"{enumerable.ElementAt(0)} and {enumerable.ElementAt(1)}";
            else
                for (int i = 0; i < length; i++)
                    if (i == length - 1)
                        str += $"and {enumerable.ElementAt(i)}";
                    else
                        str += $"{enumerable.ElementAt(i)}, ";

            return str;
        }
    }
}
