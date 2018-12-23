using System;
using System.Collections.Generic;
using System.Linq;

namespace DSharpBotCore.Extensions
{
    public static class EnumExtensions
    {
        public static IEnumerable<TAttr> GetAttributes<TAttr>(this Enum value) where TAttr : Attribute
        {
            var enumType = value.GetType();
            var name = Enum.GetName(enumType, value);
            return enumType.GetField(name).GetCustomAttributes(typeof(TAttr), false).Cast<TAttr>();
        }
        public static TAttr GetAttribute<TAttr>(this Enum value) where TAttr : Attribute
        {
            return GetAttributes<TAttr>(value).First();
        }
    }
}
