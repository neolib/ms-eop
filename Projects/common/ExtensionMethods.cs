using System;

namespace Common
{
    public static class ExtensionMethods
    {
        public static bool StartsWithText(this string self, string text)
        {
            if (self == null) return false;
            if (string.IsNullOrEmpty(text)) return false;
            return self.StartsWith(text, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool ContainsText(this string self, string text)
        {
            if (self == null) return false;
            if (string.IsNullOrEmpty(text)) return false;
            return self.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public static bool IsSameTextAs(this string self, string text)
        {
            if (self == null) return false;
            if (text == null) return false;
            return string.Compare(self, text, true) == 0;
        }

        public static bool EndsWithText(this string self, string text)
        {
            if (self == null) return false;
            if (text == null) return false;
            return self.EndsWith(text, StringComparison.CurrentCultureIgnoreCase);
        }

        public static string[] SplitWithoutEmpty(this string self, char c)
        {
            return self.Split(new[] { c }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string[] SplitWithoutEmpty(this string self, char[] chars)
        {
            return self.Split(chars, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string ToCsvValue(this string self)
        {
            if (string.IsNullOrEmpty(self)) return self;
            var s = self.Replace("\"", "\"\"");
            return $"\"{s}\"";
        }
    }
}
