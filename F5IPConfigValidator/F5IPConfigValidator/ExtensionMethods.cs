using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindInvalidIP
{
    public static class ExtensionMethods
    {
        public static bool ContainsText(this string self, string text)
        {
            if (text == null) return false;
            return self?.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public static bool IsSameTextAs(this string self, string text)
        {
            if (self == null) return false;
            if (text == null) return false;
            return string.Compare(self, text, true) == 0;
        }
    }
}
