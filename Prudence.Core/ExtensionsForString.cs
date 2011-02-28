using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Prudence
{
    public static class ExtensionsForString
    {
        private static readonly HashSet<char> InvalidFileNameChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        private static readonly Regex InvalidFileNameCharFinder = new Regex("(" + String.Join("|", InvalidFileNameChars.Select(c => Regex.Escape(c.ToString()))) + ")", RegexOptions.Compiled);

        public static string ToSafeFilename(this string candidateFileName, string replaceInvalidCharictersWith = "-")
        {
            return InvalidFileNameCharFinder.Replace(candidateFileName, replaceInvalidCharictersWith);
        }
    }
}
