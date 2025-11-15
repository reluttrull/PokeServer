namespace PokeServer.Extensions
{
    static class StringExtensions
    {
        public static string TrimEnding(this string source, string value)
        {
            if (!source.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                return source;

            return source.Remove(source.LastIndexOf(value));
        }
    }
}
