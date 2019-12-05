using System;

namespace F5IPConfigValidator
{
    public static class ExtensionMethods
    {
        public static bool ContainsText(this string self, string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return self?.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public static bool IsSameTextAs(this string self, string text)
        {
            if (self == null) return false;
            if (text == null) return false;
            return string.Compare(self, text, true) == 0;
        }

        public static string ToCsvValue(this string self)
        {
            var s = self?.Replace("\"", "\"\"");
            return $"\"{s}\"";
        }
    }
}
