namespace DSharpBotCore.Extensions
{
    public static class OtherExtensions
    {
        public static string Sanitize(this string s) => s.Replace("\\", "\\\\").Replace("*", "\\*")
                                                         .Replace("]","\\]").Replace("~", "\\~")
                                                         .Replace("_", "\\_").Replace("`", "\\`");
    }
}
