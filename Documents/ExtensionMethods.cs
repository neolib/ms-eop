// ---------------------------------------------------------------------------
// <copyright file="ExtensionMethods.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------
using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Office.Datacenter.Networking.EopWorkflows.F5Deployment
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Reads all text of a named resource in an assembly.
        /// </summary>
        /// <param name="self">Assembly instance which contains the named resource.</param>
        /// <param name="rcName">Resource name.</param>
        /// <returns>Resource content as string.</returns>
        public static string LoadResourceText(this Assembly self, string rcName)
        {
            using (var sr = new StreamReader(self.GetManifestResourceStream(rcName)))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// Checks if a string contains another string case-insensitive.
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="text">Target string.</param>
        /// <returns>Boolean value.</returns>
        /// <remarks>
        /// Builtin string Contains/IndexOf functions always assume any string
        /// contains an empty string; this logic is not useful at all. This extension
        /// function explicitly returns false if the search string is empty.
        /// </remarks>
        public static bool ContainsText(this string self, string text)
        {
            if (string.IsNullOrEmpty(self)) { return false; }
            if (string.IsNullOrEmpty(text)) { return false; }
            return self?.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        /// <summary>
        /// Shorthand of string.IndexOf(char).
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="ch">Char to search.</param>
        /// <returns>Boolean value.</returns>
        public static bool ContainsChar(this string self, char ch)
        {
            return self?.IndexOf(ch) >= 0;
        }

        /// <summary>
        /// Shorthand of string.IndexOfAny(char[]).
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="chars">Chars to search.</param>
        /// <returns>Boolean value.</returns>
        public static bool ContainsAnyChar(this string self, params char[] chars)
        {
            return self?.IndexOfAny(chars) >= 0;
        }

        /// <summary>
        /// Shorthand of string.IndexOfAny(char[] chars, int index, int count).
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="chars">Chars to search.</param>
        /// <param name="index">Start index of the char array.</param>
        /// <param name="count">Number of chars to search in the array.</param>
        /// <returns>Boolean value.</returns>
        public static bool ContainsAnyChar(this string self, char[] chars, int index, int count = 0)
        {
            return self?.IndexOfAny(chars, index, count == 0 ? chars.Length : count) >= 0;
        }

        /// <summary>
        /// Checks if a string starts with another string case-insensitive.
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="text">Target string.</param>
        /// <returns>Boolean value.</returns>
        public static bool StartsWithText(this string self, string text)
        {
            if (string.IsNullOrEmpty(self)) { return false; }
            if (string.IsNullOrEmpty(text)) { return false; }
            return self.StartsWith(text, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Checks if a string ends with another string case-insensitive.
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="text">Target string.</param>
        /// <returns>Boolean value.</returns>
        public static bool EndsWithText(this string self, string text)
        {
            if (string.IsNullOrEmpty(self)) { return false; }
            if (string.IsNullOrEmpty(text)) { return false; }
            return self.EndsWith(text, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// A shorthand extension method for Split with StringSplitOptions.RemoveEmptyEntries.
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="c">Separator character.</param>
        /// <returns>Array of strings.</returns>
        public static string[] SplitWithoutEmpty(this string self, char c)
        {
            return self?.Split(new[] { c }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// A shorthand extension method for Split with StringSplitOptions.RemoveEmptyEntries.
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="chars">Array of separator characters.</param>
        /// <returns>Array of strings.</returns>
        public static string[] SplitWithoutEmpty(this string self, char[] chars)
        {
            return self?.Split(chars, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Checks if a string has same text as another string case-insensitive.
        /// </summary>
        /// <param name="self">Source string.</param>
        /// <param name="text">Target string.</param>
        /// <returns>Boolean value.</returns>
        public static bool IsSameTextAs(this string self, string text)
        {
            if (self == null) { return false; }
            if (text == null) { return false; }
            return string.Compare(self, text, true) == 0;
        }
    }
}
