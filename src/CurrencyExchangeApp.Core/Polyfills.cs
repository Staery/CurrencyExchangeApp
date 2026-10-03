// Language features used by this library that netstandard2.0 does not ship the supporting types for.

namespace System.Runtime.CompilerServices
{
    /// <summary>Enables <c>init</c> accessors and records.</summary>
    internal static class IsExternalInit
    {
    }
}

namespace CurrencyExchangeApp.Core
{
    internal static class Guard
    {
        public static T NotNull<T>(T? value, string name)
            where T : class => value ?? throw new ArgumentNullException(name);
    }

    internal static class StringExtensions
    {
        public static bool Contains(this string text, string value, StringComparison comparison) =>
            text.IndexOf(value, comparison) >= 0;
    }
}
