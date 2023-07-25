using System.Text.RegularExpressions;

namespace SonarRepair
{
    internal static class Helper
    {
        public static string SplitCamelCase(this string str)
        {
            return Regex.Replace(
            Regex.Replace(
            str,
            @"(\P{Ll})(\P{Ll}\p{Ll})",
            "$1 $2"
            ),
            @"(\p{Ll})(\P{Ll})",
            "$1 $2"
            );
        }

        public static int GenerateNumber()
        {
            Random rnd = new Random();
            return rnd.Next(1, 100);
        }
    }
}
