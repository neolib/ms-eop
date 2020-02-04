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

        public static bool ContainsChar(this string self, char ch)
        {
            if (self == null) return false;
            return self.IndexOf(ch) >= 0;
        }

        public static bool ContainsAnyChar(this string self, params char[] chars)
        {
            if (self == null) return false;
            return self.IndexOfAny(chars) >= 0;
        }

        public static bool ContainsAnyChar(this string self, char[] chars, int index, int count = 0)
        {
            if (self == null) return false;
            return self.IndexOfAny(chars, index, count == 0 ? chars.Length : count) >= 0;
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
